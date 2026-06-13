using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 全局事件总线契约。
    /// 由 ModuleSystem 持有，所有 Module / 业务代码通过此发布或订阅事件。
    /// </summary>
    public interface IEventModule
    {
        /// <summary>
        /// handler 抛出异常时触发（每个被隔离的异常一次），参数为事件类型与异常。
        /// 异常不会中断其余 handler 的派发；本钩子自身抛出的异常会被吞掉以防级联。
        /// 业务通常在启动时订阅一次，统一路由到 ILogger。
        /// </summary>
        event Action<Type, Exception> HandlerException;

        /// <summary>
        /// 发布事件。订阅者按订阅顺序同步调用。
        /// 嵌套发布（handler 内再 Publish）深度超过 <c>32</c> 时抛 <see cref="InvalidOperationException"/>，
        /// 防止事件互相触发形成无限递归导致栈溢出。
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

        /// <summary>
        /// 获取指定事件类型当前订阅者数量。
        /// </summary>
        int GetSubscriberCount<T>() where T : struct;

        /// <summary>
        /// 获取当前存在订阅者的事件类型列表。
        /// </summary>
        IReadOnlyList<Type> GetEventTypes();
    }
}
