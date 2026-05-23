using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// EntityWorld 服务契约（V0.3 之后）。
    ///
    /// 设计哲学（ADR-0011 / ADR-0001 / ADR-0008）：
    /// - EntityWorld 是「玩法层根」：拥有 Entity 生命周期、System 调度、Phase 推进。
    /// - EntityWorld 本身是 IModule：可挂到 ModuleHost 上参与统一生命周期编排。
    /// - 全局事件由 ModuleHost.EventBus（IEventBus）承担，EntityWorld 不再持有事件总线。
    /// - 不再依赖 V0.1 的 IWorldAdapter；Unity 侧由 WorldProxy 驱动 ModuleHost，
    ///   非 Unity 场景直接 new ModuleHost + Register&lt;IEntityWorld&gt;。
    /// </summary>
    public interface IEntityWorld : IModule, IUpdateModule
    {
        /// <summary>
        /// EntityWorld 名字（多 World 场景识别）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 运行状态（Created → Entering → Running → Exiting → Shutdown）。
        /// </summary>
        EntityWorldState State { get; }

        /// <summary>
        /// 当前活跃 Entity 列表。
        /// </summary>
        IReadOnlyList<Entity> Entities { get; }

        /// <summary>
        /// 创建 Entity。
        /// </summary>
        Entity CreateEntity();

        /// <summary>
        /// 销毁 Entity（级联销毁子树，叶子优先）。
        /// </summary>
        void DestroyEntity(Entity entity);

        /// <summary>
        /// 通过 EntityId 获取 Entity。已销毁或不存在则返回 null。
        /// </summary>
        Entity GetEntity(EntityId id);

        /// <summary>
        /// 注册 System 到指定 Phase + SystemGroup。
        /// </summary>
        void RegisterSystem(SystemBase system, Phase phase, SystemGroup group = null);

        /// <summary>
        /// 添加 SystemGroup 到指定 Phase（控制 Group 之间的执行顺序）。
        /// </summary>
        void AddSystemGroup(SystemGroup group, Phase phase);

        /// <summary>
        /// 直接 API：执行 Enter Phase（不通过 ModuleHost 时使用）。
        /// 通过 ModuleHost 时由 <see cref="IModule.OnInit"/> 自动触发。
        /// </summary>
        void Start();
    }

    /// <summary>
    /// EntityWorld 运行状态。
    /// </summary>
    public enum EntityWorldState
    {
        /// <summary>创建后、尚未启动。</summary>
        Created,
        /// <summary>正在执行 Enter Phase。</summary>
        Entering,
        /// <summary>运行中（Update Phase 循环）。</summary>
        Running,
        /// <summary>正在执行 Exit Phase。</summary>
        Exiting,
        /// <summary>已关闭。</summary>
        Shutdown,
    }
}
