using System;

namespace TryGet
{
    /// <summary>
    /// 定时器服务契约（Common Module）。基于 ModuleSystem.Update 累计 deltaTime 驱动，跨端可用。
    ///
    /// 取消：cancel 已触发的 handle 返回 false，cancel 未触发的 handle 返回 true。
    /// 时间基准：默认走 scaled deltaTime（受 timeScale 影响），可通过 <see cref="ScheduleUnscaled"/> 走 unscaled。
    ///
    /// 支持一次性、周期性、暂停和恢复定时器。
    /// </summary>
    public interface ITimerModule : IModule
    {
        /// <summary>
        /// 延迟 <paramref name="seconds"/> 秒后执行 <paramref name="callback"/>。
        /// 使用 scaled deltaTime（受 timeScale 影响）。
        /// </summary>
        TimerHandle Schedule(float seconds, Action callback);

        /// <summary>
        /// 延迟 <paramref name="seconds"/> 秒后执行 <paramref name="callback"/>。
        /// 使用 unscaled deltaTime（不受 timeScale 影响）。
        /// </summary>
        TimerHandle ScheduleUnscaled(float seconds, Action callback);

        /// <summary>
        /// 周期性触发：每隔 <paramref name="intervalSeconds"/> 秒执行一次，直到 Cancel。
        /// 第一次触发在 intervalSeconds 后（不立即触发）。使用 scaled deltaTime。
        /// </summary>
        /// <param name="maxCatchUp">
        /// 长帧补偿策略：当单帧 deltaTime 超过多个 interval 时，最多补偿触发的次数。
        /// 0 = 不补偿（默认，保持当前行为）；int.MaxValue = 完全补偿所有丢失触发。
        /// </param>
        TimerHandle ScheduleRepeat(float intervalSeconds, Action callback, int maxCatchUp = 0);

        /// <summary>
        /// 取消定时器（一次性或周期性）。已触发并删除的一次性 handle 返回 false。
        /// </summary>
        bool Cancel(TimerHandle handle);

        /// <summary>
        /// 暂停定时器（剩余时间冻结，Update 不推进）。未找到/已暂停返回 false。
        /// </summary>
        bool Pause(TimerHandle handle);

        /// <summary>
        /// 恢复定时器。未找到/未暂停返回 false。
        /// </summary>
        bool Resume(TimerHandle handle);

        /// <summary>
        /// 是否已暂停。未找到返回 false。
        /// </summary>
        bool IsPaused(TimerHandle handle);

        /// <summary>
        /// 当前未触发的定时器数量（用于诊断/测试）。包括已暂停的。
        /// </summary>
        int PendingCount { get; }
    }
}
