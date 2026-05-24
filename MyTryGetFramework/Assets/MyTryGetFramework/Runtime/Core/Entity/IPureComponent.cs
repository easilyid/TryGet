namespace TryGet
{
    /// <summary>
    /// 纯数据 Component 标记接口（V0.9 起，ECS 二级方案，参考 ET）。
    ///
    /// 与 <see cref="Aspect"/> 对比：
    /// - <see cref="Aspect"/>：OO 风格，行为内聚（OnAttach/OnDetach + 自定义方法），数据 + 行为同实例
    /// - <see cref="IPureComponent"/>：ET 风格，行为外置到 <see cref="IComponentSystem{TComponent}"/>，数据/行为分离
    ///
    /// 双轨并存（见 ADR-0017）：业务按场景选 — 行为复杂用 Aspect，数据简单用 PureComponent + System。
    ///
    /// 行为约束（编译期无强制，仅文档纪律）：
    /// - 不应有方法 / 属性 setter（业务用 IComponentSystem 操作）
    /// - 应是 POCO struct 或 class（V0.9 struct 走 boxed 存储；V0.9.5 Source Gen 后评估泛型化）
    /// - 不持引用外部 Aspect / Module
    /// </summary>
    public interface IPureComponent { }

    /// <summary>
    /// 外置 System 操作 <see cref="IPureComponent"/>（参考 ET <c>IEventSystem&lt;C,E&gt;</c>）。
    /// 业务实现 <c>IComponentSystem&lt;MyComponent&gt;</c> 提供 OnAttach/OnDetach 等行为。
    ///
    /// V0.9：framework 不自动调度，业务显式调 <c>system.OnAttach(entity, component)</c> 触发。
    /// V0.9.5：Source Generator 后将提供自动调度（路线图 ADR-0017 §Future Work）。
    /// </summary>
    public interface IComponentSystem<TComponent> where TComponent : IPureComponent
    {
        /// <summary>Component 被 attach 到 Entity 时业务触发。</summary>
        void OnAttach(Entity entity, TComponent component);

        /// <summary>Component 从 Entity detach 时业务触发。</summary>
        void OnDetach(Entity entity, TComponent component);
    }
}
