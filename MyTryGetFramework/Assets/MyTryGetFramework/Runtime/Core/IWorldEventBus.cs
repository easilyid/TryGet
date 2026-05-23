using System;

namespace TryGet
{
    /// <summary>
    /// World-level 事件派发接口。
    /// System 和其他 World 内的订阅者通过此接口收发 World 范围的事件。
    /// </summary>
    public interface IWorldEventBus
    {
        /// <summary>
        /// 发布一个 World-level 事件。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="evt">事件实例。</param>
        void Publish<T>(T evt) where T : struct;

        /// <summary>
        /// 订阅 World-level 事件。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="handler">事件处理回调。</param>
        void Subscribe<T>(Action<T> handler) where T : struct;

        /// <summary>
        /// 取消订阅 World-level 事件。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="handler">之前注册的回调。</param>
        void Unsubscribe<T>(Action<T> handler) where T : struct;
    }
}
