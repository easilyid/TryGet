using System;

namespace TryGet
{
    /// <summary>
    /// 挂载到 Entity 上、封装自身状态和局部行为的组合能力切片。
    ///
    /// 行为边界 (ADR-0007)：
    /// - 允许：操作自身字段的验证、计算、状态转换方法。
    /// - 允许：通过 EventDispatcher 发布 Entity-level Event。
    /// - 禁止：访问其他 Aspect、Entity、World 或外部服务。
    /// </summary>
    public abstract class Aspect
    {
        private Entity _owner;

        /// <summary>
        /// 所属 Entity。由框架在 Attach 时注入，Aspect 不应暴露此引用给外部。
        /// </summary>
        protected Entity Owner => _owner;

        /// <summary>
        /// Entity-level 事件派发器。由框架在 Attach 时注入。
        /// Aspect 可通过此发布事件，但不能直接访问 Entity 或 World。
        /// </summary>
        protected IEntityEventDispatcher EventDispatcher { get; private set; }

        /// <summary>
        /// 当 Aspect 被挂载到 Entity 时由框架调用。
        /// </summary>
        protected internal virtual void OnAttach() { }

        /// <summary>
        /// 当 Aspect 从 Entity 卸载时由框架调用。
        /// </summary>
        protected internal virtual void OnDetach() { }

        /// <summary>
        /// 框架内部：设置 Owner 引用。
        /// </summary>
        internal void SetOwner(Entity owner, IEntityEventDispatcher dispatcher)
        {
            _owner = owner;
            EventDispatcher = dispatcher;
        }

        /// <summary>
        /// 框架内部：清除 Owner 引用。
        /// </summary>
        internal void ClearOwner()
        {
            _owner = null;
            EventDispatcher = null;
        }

        /// <summary>
        /// 返回此 Aspect 的类型标识，用于 Query 匹配和唯一性约束。
        /// 默认使用运行时类型。
        /// </summary>
        public virtual Type AspectType => GetType();
    }
}
