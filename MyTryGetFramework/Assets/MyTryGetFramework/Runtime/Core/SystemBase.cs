using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 查询 Aspect 组合并执行跨 Entity 调度规则的运行单元。
    ///
    /// 执行契约 (ADR-0008)：
    /// - System 通过显式 API 注册到 World。
    /// - 每个 System 声明所属 SystemGroup 和 Phase。
    /// - System 可持有跨 Entity 状态（计时器/缓存），禁止持有单 Entity 能力状态。
    /// - Execute 接收 Query 匹配的 Entity 集合。
    /// </summary>
    public abstract class SystemBase
    {
        private World _world;
        private Query _query;

        /// <summary>
        /// 所属 World。由框架在注册时注入。
        /// </summary>
        protected World World => _world;

        /// <summary>
        /// World-level 事件总线。便捷访问。
        /// </summary>
        protected IWorldEventBus EventBus => _world?.EventBus;

        /// <summary>
        /// 定义此 System 的 Query。子类在构造时或 OnCreate 中构建。
        /// </summary>
        public Query Query
        {
            get => _query;
            protected set => _query = value;
        }

        /// <summary>
        /// System 创建时调用（注册到 World 后）。用于初始化 Query 和内部状态。
        /// </summary>
        protected internal virtual void OnCreate() { }

        /// <summary>
        /// System 销毁时调用（World 关闭或 System 移除）。用于清理。
        /// </summary>
        protected internal virtual void OnDestroy() { }

        /// <summary>
        /// 执行跨 Entity 的调度规则。
        /// </summary>
        /// <param name="entities">Query 匹配的 Entity 集合。</param>
        protected internal abstract void Execute(IReadOnlyList<Entity> entities);

        /// <summary>
        /// 框架内部：注入 World 引用。
        /// </summary>
        internal void SetWorld(World world)
        {
            _world = world;
        }

        /// <summary>
        /// 框架内部：清除 World 引用。
        /// </summary>
        internal void ClearWorld()
        {
            _world = null;
        }
    }
}
