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
        /// 实现：内部用池化的 <see cref="TGTaskCompletionSource"/>，timer 触发时 SetResult 并回收 tcs；
        /// body 由 await 路径的 GetResult 在消费侧自动归还池（V2.0 C11）。
        /// </summary>
        public static TGTask WaitAsync(this ITimerModule timer, float seconds)
        {
            if (timer == null) throw new ArgumentNullException(nameof(timer));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "seconds must be >= 0");

            var tcs = TGTaskCompletionSource.Rent();
            var task = tcs.Task;
            timer.Schedule(seconds, () =>
            {
                tcs.SetResult();
                TGTaskCompletionSource.Recycle(tcs);
            });
            return task;
        }

        /// <summary>
        /// 同 <see cref="WaitAsync"/> 但使用 unscaled deltaTime（不受 timeScale 影响）。
        /// </summary>
        public static TGTask WaitUnscaledAsync(this ITimerModule timer, float seconds)
        {
            if (timer == null) throw new ArgumentNullException(nameof(timer));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "seconds must be >= 0");

            var tcs = TGTaskCompletionSource.Rent();
            var task = tcs.Task;
            timer.ScheduleUnscaled(seconds, () =>
            {
                tcs.SetResult();
                TGTaskCompletionSource.Recycle(tcs);
            });
            return task;
        }
    }
}
