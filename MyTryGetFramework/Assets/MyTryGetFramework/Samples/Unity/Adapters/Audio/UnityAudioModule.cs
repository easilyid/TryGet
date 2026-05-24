using System;
using System.Collections.Generic;
using UnityEngine;

namespace TryGet.Unity
{
    /// <summary>
    /// IAudioModule 的 Unity Adapter。基于 AudioSource 池实现。
    ///
    /// 设计权衡：
    /// - **不耦合 IResourceModule**：AudioClip 由调用方通过 <see cref="RegisterClip"/> 传入，
    ///   Adapter 保持薄。Production 业务可在启动时通过 ResourceModule 加载 AudioClip 后 RegisterClip。
    /// - AudioSource 池：默认 16 个 GameObject + AudioSource 挂在 root 下，溢出策略 = 复用最旧（停掉再用）。
    /// - 帧调度：依赖 AudioSource 自身的播放生命周期；本类不实现 IUpdateModule。
    /// - 与 <see cref="MemoryAudioModule"/> 行为契约对齐：同一组 Play/Stop/Pause 序列应输出一致状态。
    ///
    /// 使用：
    /// <code>
    /// var ua = new UnityAudioModule(poolSize: 16);
    /// host.Register&lt;IAudioModule&gt;(ua);
    /// host.Initialize();
    /// // 业务启动时把 AudioClip 注入 Adapter
    /// ua.RegisterClip("bgm/main", mainBgmClip);
    /// host.Get&lt;IAudioModule&gt;().Play("bgm/main", AudioCategory.BGM);
    /// </code>
    /// </summary>
    public sealed class UnityAudioModule : IAudioModule
    {
        private const int DefaultPoolSize = 16;

        private readonly int _poolSize;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, PlayingSlot> _playing = new Dictionary<string, PlayingSlot>();
        // 池中所有 AudioSource，按创建顺序，溢出时按"最早播放"复用
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        // FIFO 记录每个池槽位的"上次开始播放"时序，用于溢出策略
        private readonly Queue<int> _playOrder = new Queue<int>();

        private GameObject _root;
        private float _masterVolume = 1f;
        private readonly float[] _categoryVolumes = new float[4];

        private struct PlayingSlot
        {
            public int PoolIndex;        // AudioSource 在 _pool 中的位置
            public AudioCategory Category;
            public bool Paused;
        }

        public int Priority => -380;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int PlayingCount => _playing.Count;
        public float MasterVolume => _masterVolume;

        public UnityAudioModule(int poolSize = DefaultPoolSize)
        {
            if (poolSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(poolSize), "Pool size must be > 0");
            _poolSize = poolSize;
            for (int i = 0; i < _categoryVolumes.Length; i++)
                _categoryVolumes[i] = 1f;
        }

        public void OnInit(IModuleHost host)
        {
            _root = new GameObject("[UnityAudioModule]");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(_root.transform, worldPositionStays: false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool.Add(src);
            }
        }

        public void Shutdown()
        {
            _playing.Clear();
            _playOrder.Clear();
            _clips.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            _pool.Clear();
            _masterVolume = 1f;
            for (int i = 0; i < _categoryVolumes.Length; i++)
                _categoryVolumes[i] = 1f;
        }

        /// <summary>
        /// 注册 cue → AudioClip 映射。重复注册同 cue 覆盖。
        /// </summary>
        public void RegisterClip(string cue, AudioClip clip)
        {
            if (string.IsNullOrEmpty(cue))
                throw new ArgumentException("Cue must be non-empty.", nameof(cue));
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            _clips[cue] = clip;
        }

        /// <summary>
        /// 取消注册 cue 映射。如果 cue 在播放，会同时 Stop。返回是否真的取消了。
        /// </summary>
        public bool UnregisterClip(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return false;
            if (_playing.ContainsKey(cue)) Stop(cue);
            return _clips.Remove(cue);
        }

        public void Play(string cue, AudioCategory category = AudioCategory.SFX)
        {
            if (string.IsNullOrEmpty(cue))
                throw new ArgumentException("Cue must be non-empty.", nameof(cue));
            if (!_clips.TryGetValue(cue, out var clip))
                throw new InvalidOperationException(
                    $"Cue '{cue}' not registered. Call RegisterClip first.");

            // 已经在播放：复用同一 slot（Stop 当前 + 重新播）
            int slot;
            if (_playing.TryGetValue(cue, out var existing))
            {
                slot = existing.PoolIndex;
                _pool[slot].Stop();
            }
            else
            {
                slot = AcquireSlot();
            }

            var src = _pool[slot];
            src.clip = clip;
            src.volume = ComputeEffectiveVolume(category);
            src.Play();

            _playing[cue] = new PlayingSlot
            {
                PoolIndex = slot,
                Category = category,
                Paused = false,
            };
            _playOrder.Enqueue(slot);
        }

        public void Stop(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return;
            if (_playing.TryGetValue(cue, out var slot))
            {
                _pool[slot.PoolIndex].Stop();
                _pool[slot.PoolIndex].clip = null;
                _playing.Remove(cue);
            }
        }

        public void StopAll(AudioCategory category)
        {
            List<string> toRemove = null;
            foreach (var kv in _playing)
            {
                if (kv.Value.Category == category)
                {
                    toRemove ??= new List<string>();
                    toRemove.Add(kv.Key);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                    Stop(toRemove[i]);
            }
        }

        public void StopAllSounds()
        {
            foreach (var kv in _playing)
            {
                _pool[kv.Value.PoolIndex].Stop();
                _pool[kv.Value.PoolIndex].clip = null;
            }
            _playing.Clear();
        }

        public bool IsPlaying(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return false;
            return _playing.ContainsKey(cue);
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Clamp01(volume);
            RefreshAllVolumes();
        }

        public float GetCategoryVolume(AudioCategory category)
        {
            return _categoryVolumes[(int)category];
        }

        public void SetCategoryVolume(AudioCategory category, float volume)
        {
            _categoryVolumes[(int)category] = Clamp01(volume);
            RefreshAllVolumes();
        }

        public bool Pause(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return false;
            if (_playing.TryGetValue(cue, out var slot) && !slot.Paused)
            {
                _pool[slot.PoolIndex].Pause();
                slot.Paused = true;
                _playing[cue] = slot;
                return true;
            }
            return false;
        }

        public bool Resume(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return false;
            if (_playing.TryGetValue(cue, out var slot) && slot.Paused)
            {
                _pool[slot.PoolIndex].UnPause();
                slot.Paused = false;
                _playing[cue] = slot;
                return true;
            }
            return false;
        }

        public bool IsPaused(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return false;
            return _playing.TryGetValue(cue, out var slot) && slot.Paused;
        }

        public void PauseAll(AudioCategory category)
        {
            List<string> toUpdate = null;
            foreach (var kv in _playing)
            {
                if (kv.Value.Category == category && !kv.Value.Paused)
                {
                    toUpdate ??= new List<string>();
                    toUpdate.Add(kv.Key);
                }
            }
            if (toUpdate != null)
            {
                for (int i = 0; i < toUpdate.Count; i++) Pause(toUpdate[i]);
            }
        }

        public void ResumeAll(AudioCategory category)
        {
            List<string> toUpdate = null;
            foreach (var kv in _playing)
            {
                if (kv.Value.Category == category && kv.Value.Paused)
                {
                    toUpdate ??= new List<string>();
                    toUpdate.Add(kv.Key);
                }
            }
            if (toUpdate != null)
            {
                for (int i = 0; i < toUpdate.Count; i++) Resume(toUpdate[i]);
            }
        }

        // —— 私有 ——

        private int AcquireSlot()
        {
            // 找空闲 slot（_pool 中某 AudioSource 没在播放）
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!IsSlotOccupied(i))
                    return i;
            }
            // 池满：复用最旧（按 _playOrder 出队，找还在 _playing 中的最早 cue 把它 Stop 让出槽位）
            while (_playOrder.Count > 0)
            {
                int oldest = _playOrder.Dequeue();
                // 这个 slot 是否仍被某 cue 占着？
                string cueAtSlot = null;
                foreach (var kv in _playing)
                {
                    if (kv.Value.PoolIndex == oldest) { cueAtSlot = kv.Key; break; }
                }
                if (cueAtSlot != null)
                {
                    Stop(cueAtSlot);
                    return oldest;
                }
                // 已被 stop 掉了，继续找下一个
            }
            // 兜底：第 0 个
            return 0;
        }

        private bool IsSlotOccupied(int slot)
        {
            foreach (var kv in _playing)
                if (kv.Value.PoolIndex == slot) return true;
            return false;
        }

        private float ComputeEffectiveVolume(AudioCategory category)
        {
            return _masterVolume * _categoryVolumes[(int)category];
        }

        private void RefreshAllVolumes()
        {
            foreach (var kv in _playing)
            {
                _pool[kv.Value.PoolIndex].volume = ComputeEffectiveVolume(kv.Value.Category);
            }
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
