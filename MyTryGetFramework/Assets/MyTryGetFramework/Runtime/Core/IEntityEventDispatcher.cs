using System;

namespace TryGet
{
    /// <summary>
    /// Entity-level 事件派发接口。
    /// Aspect 通过此接口发布事件，由所属 Entity 分发给其他 Aspect 观察者。
    /// </summary>
    public interface IEntityEventDispatcher
    {
        /// <summary>
        /// 发布一个 Entity-level 事件。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="evt">事件实例。</param>
        void Publish<T>(T evt) where T : struct;
    }
}
