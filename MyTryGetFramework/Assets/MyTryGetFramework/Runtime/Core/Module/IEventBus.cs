using System;

namespace TryGet
{
    /// <summary>
    /// 全局事件总线契约（ADR-0010 + ADR-0011）。
    /// 由 ModuleHost 持有，所有 Module / Aspect / 业务代码通过此发布或订阅全局事件。
    ///
    /// 替代 V0.1 的 <see cref="IWorldEventBus"/>——后者保留作为别名，新代码请用 IEventBus。
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 发布事件。订阅者按订阅顺序同步调用。
        /// </summary>
        void Publish<T>(T evt) where T : struct;

        /// <summary>
        /// 订阅事件。
        /// </summary>
        void Subscribe<T>(Action<T> handler) where T : struct;

        /// <summary>
        /// 取消订阅。未订阅过的 handler 静默返回，不抛异常。
        /// </summary>
        void Unsubscribe<T>(Action<T> handler) where T : struct;
    }
}
