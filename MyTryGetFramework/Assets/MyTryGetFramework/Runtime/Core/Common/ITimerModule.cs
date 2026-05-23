using System;

namespace TryGet
{
    /// <summary>
    /// 定时器服务契约（Common Module）。基于 ModuleHost.Update 累计 deltaTime 驱动，跨端可用。
    ///
    /// 取消：cancel 已触发的 handle 返回 false，cancel 未触发的 handle 返回 true。
    /// 时间基准：默认走 scaled deltaTime（受 timeScale 影响），可通过 <see cref="ScheduleUnscaled"/> 走 unscaled。
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
        /// 取消未触发的定时器。已触发或已取消返回 false。
        /// </summary>
        bool Cancel(TimerHandle handle);

        /// <summary>
        /// 当前未触发的定时器数量（用于诊断/测试）。
        /// </summary>
        int PendingCount { get; }
    }
}
