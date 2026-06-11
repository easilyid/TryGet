using System;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// V0.6 Iter 6：为 <see cref="ITimerModule"/> 提供 TGTask 集成的扩展方法。
    ///
    /// 命名空间故意放在 <c>TryGet</c>（与 ITimerModule 一致），让业务 <c>using TryGet;</c>
    /// 即可同时获得 timer 接口和 await 扩展，不必再加 <c>using TryGet.Async;</c>。
    /// </summary>
    public static class TimerModuleAsyncExtensions
    {
        /// <summary>
        /// 把延迟 N 秒后触发的回调包装成可 await 的 TGTask（使用 scaled deltaTime）。
        ///
        /// 实现：内部用 <see cref="TGTaskCompletionSource"/>，timer 触发时 SetResult。
        /// body 生命周期由 await 路径的 GetResult 自动管理，Manual 类型不归还池（由 GC 回收）。
        ///
        /// 注意：调用本方法即占用一个 TGTaskBody（自动池化）；不要在 hot loop 反复构造但不 await。
        /// </summary>
        public static TGTask WaitAsync(this ITimerModule timer, float seconds)
        {
            if (timer == null) throw new ArgumentNullException(nameof(timer));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "seconds must be >= 0");

            var tcs = new TGTaskCompletionSource();
            timer.Schedule(seconds, () => tcs.SetResult());
            return tcs.Task;
        }

        /// <summary>
        /// 同 <see cref="WaitAsync"/> 但使用 unscaled deltaTime（不受 timeScale 影响）。
        /// </summary>
        public static TGTask WaitUnscaledAsync(this ITimerModule timer, float seconds)
        {
            if (timer == null) throw new ArgumentNullException(nameof(timer));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "seconds must be >= 0");

            var tcs = new TGTaskCompletionSource();
            timer.ScheduleUnscaled(seconds, () => tcs.SetResult());
            return tcs.Task;
        }
    }
}
