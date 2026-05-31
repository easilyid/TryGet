using System;

namespace TryGet
{
    /// <summary>
    /// <see cref="IEventModule"/> 的 scope 扩展（V0.7 Iter 4）。
    /// 现有不带 scope 的 <see cref="IEventModule.Subscribe{T}"/> 保留共存（无破坏）。
    /// </summary>
    public static class EventModuleScopeExtensions
    {
        /// <summary>
        /// 创建新的 <see cref="EventScope"/>（fluent helper）。
        /// 此方法不与 bus 状态绑定 — scope 是独立对象，仅在订阅时被 Subscribe overload 关联。
        /// </summary>
        public static EventScope CreateScope(this IEventModule _) => new EventScope();

        /// <summary>
        /// 订阅事件并把"解绑动作"注册到 scope。scope.Dispose 时自动解绑此 handler。
        /// </summary>
        public static void Subscribe<T>(this IEventModule bus, Action<T> handler, EventScope scope) where T : struct
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            bus.Subscribe(handler);
            scope.Register(() => bus.Unsubscribe(handler));
        }
    }
}
