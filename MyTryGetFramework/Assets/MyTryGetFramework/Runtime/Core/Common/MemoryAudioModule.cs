using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IAudioModule 的内存实现（跨端，零外部依赖）。
    ///
    /// 用途：单元测试、Headless 服务端、Procedure 流程逻辑测试、Adapter 开发期 mock。
    /// **不实际发声**。只记录 cue 播放状态 + 音量值。
    /// Production Unity 由 Adapters/Unity 层的 UnityAudioModule（接 AudioSource）替换。
    /// </summary>
    public sealed class MemoryAudioModule : IAudioModule
    {
        // cue → category 映射：用 Dictionary 同时支持 IsPlaying O(1) 与按 category 过滤 StopAll
        private readonly Dictionary<string, AudioCategory> _playing = new Dictionary<string, AudioCategory>();
        private float _masterVolume = 1f;
        private readonly float[] _categoryVolumes = new float[4]; // BGM/SFX/UI/Voice

        public int Priority => -380; // 在 UI (-300) 之前、Resource (-400) 之后；Audio cue 实际可能由 Resource 加载
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int PlayingCount => _playing.Count;
        public float MasterVolume => _masterVolume;

        public MemoryAudioModule()
        {
            // 默认所有分类 1.0
            for (int i = 0; i < _categoryVolumes.Length; i++)
                _categoryVolumes[i] = 1f;
        }

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _playing.Clear();
            _masterVolume = 1f;
            for (int i = 0; i < _categoryVolumes.Length; i++)
                _categoryVolumes[i] = 1f;
        }

        public void Play(string cue, AudioCategory category = AudioCategory.SFX)
        {
            if (string.IsNullOrEmpty(cue))
                throw new ArgumentException("Cue must be non-empty.", nameof(cue));

            // 同 cue 重复 Play：更新 category（业务可能用不同 category 重播同一资源）
            _playing[cue] = category;
        }

        public void Stop(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return;
            _playing.Remove(cue);
        }

        public void StopAll(AudioCategory category)
        {
            // 收集后删除，避免迭代过程中修改
            List<string> toRemove = null;
            foreach (var kv in _playing)
            {
                if (kv.Value == category)
                {
                    toRemove ??= new List<string>();
                    toRemove.Add(kv.Key);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                    _playing.Remove(toRemove[i]);
            }
        }

        public void StopAllSounds()
        {
            _playing.Clear();
        }

        public bool IsPlaying(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return false;
            return _playing.ContainsKey(cue);
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Clamp01(volume);
        }

        public float GetCategoryVolume(AudioCategory category)
        {
            return _categoryVolumes[(int)category];
        }

        public void SetCategoryVolume(AudioCategory category, float volume)
        {
            _categoryVolumes[(int)category] = Clamp01(volume);
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
