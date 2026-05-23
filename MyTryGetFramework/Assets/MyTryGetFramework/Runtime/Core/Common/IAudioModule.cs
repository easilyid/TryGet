using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 音频分类。用于按类别批量调音量 / 停止。
    /// </summary>
    public enum AudioCategory
    {
        /// <summary>背景音乐（通常单音轨循环）</summary>
        BGM,
        /// <summary>音效（短促、可同时多个）</summary>
        SFX,
        /// <summary>UI 反馈声（按钮点击、提示）</summary>
        UI,
        /// <summary>角色 / NPC 语音</summary>
        Voice,
    }

    /// <summary>
    /// 音频服务契约（V0.4 Common Module）。
    ///
    /// 设计原则：Core 层只定义"逻辑层"API（Play/Stop/Volume/Category），
    /// **不依赖 UnityEngine.AudioClip / AudioSource / AudioMixer**。
    /// 实际 Unity 音频由 Adapters/Unity 层的 <c>UnityAudioModule</c> 接 AudioSource 实现。
    ///
    /// Memory 实现（<see cref="MemoryAudioModule"/>）只记录"哪些 cue 在播放、当前音量是多少"
    /// 的状态机，用于单元测试、Headless 服务端、Procedure 流程逻辑测试。
    ///
    /// Cue 设计：用 string 标识音效（如 "sfx/explosion" / "bgm/main_theme"），
    /// Adapter 内部按需映射到 AudioClip 资源（通常通过 IResourceModule 加载）。
    ///
    /// 音量范围 [0, 1]，超界 clamp（Unity AudioSource 一致）。
    /// </summary>
    public interface IAudioModule : IModule
    {
        /// <summary>
        /// 播放一个音效 cue。同 cue 重复 Play 视为"已在播放"，幂等不抛。
        /// 实际 Adapter 实现可能按类别决定"单实例"（BGM）或"多实例"（SFX）。
        /// </summary>
        void Play(string cue, AudioCategory category = AudioCategory.SFX);

        /// <summary>
        /// 停止某 cue。未播放时静默（幂等不抛）。
        /// </summary>
        void Stop(string cue);

        /// <summary>
        /// 停止某分类的全部音效。
        /// </summary>
        void StopAll(AudioCategory category);

        /// <summary>
        /// 停止所有正在播放的音效。
        /// </summary>
        void StopAllSounds();

        /// <summary>
        /// 是否正在播放某 cue。
        /// </summary>
        bool IsPlaying(string cue);

        /// <summary>
        /// 主音量（影响所有分类）。范围 [0,1]，超界 clamp。
        /// </summary>
        float MasterVolume { get; }

        /// <summary>
        /// 设置主音量。
        /// </summary>
        void SetMasterVolume(float volume);

        /// <summary>
        /// 获取某分类的音量。
        /// </summary>
        float GetCategoryVolume(AudioCategory category);

        /// <summary>
        /// 设置某分类的音量。
        /// </summary>
        void SetCategoryVolume(AudioCategory category, float volume);

        /// <summary>
        /// 当前正在播放的 cue 总数。
        /// </summary>
        int PlayingCount { get; }
    }
}
