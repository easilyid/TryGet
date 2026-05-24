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
        private struct PlayingEntry
        {
            public AudioCategory Category;
            public bool Paused;
        }

        // cue → entry：扩展为 struct 以同时存 category 和 paused 状态
        private readonly Dictionary<string, PlayingEntry> _playing = new Dictionary<string, PlayingEntry>();
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

            // 同 cue 重复 Play：更新 category，清 paused（重新播放隐含恢复）
            _playing[cue] = new PlayingEntry { Category = category, Paused = false };
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
                if (kv.Value.Category == category)
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

        // V0.5: Pause/Resume

        public bool Pause(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return false;
            if (_playing.TryGetValue(cue, out var entry) && !entry.Paused)
            {
                entry.Paused = true;
                _playing[cue] = entry;
                return true;
            }
            return false;
        }

        public bool Resume(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return false;
            if (_playing.TryGetValue(cue, out var entry) && entry.Paused)
            {
                entry.Paused = false;
                _playing[cue] = entry;
                return true;
            }
            return false;
        }

        public bool IsPaused(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return false;
            return _playing.TryGetValue(cue, out var entry) && entry.Paused;
        }

        public void PauseAll(AudioCategory category)
        {
            // 改 Dictionary value 需重新赋值（PlayingEntry 是 struct）
            // 收集 key 后批量更新
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
                for (int i = 0; i < toUpdate.Count; i++)
                {
                    var entry = _playing[toUpdate[i]];
                    entry.Paused = true;
                    _playing[toUpdate[i]] = entry;
                }
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
                for (int i = 0; i < toUpdate.Count; i++)
                {
                    var entry = _playing[toUpdate[i]];
                    entry.Paused = false;
                    _playing[toUpdate[i]] = entry;
                }
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
