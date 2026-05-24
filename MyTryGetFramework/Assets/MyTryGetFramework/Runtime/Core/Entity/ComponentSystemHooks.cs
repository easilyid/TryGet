using System;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 Iter 6 起：每个 <see cref="IPureComponent"/> 类型对应的全局 hook 表。
    /// Generator 扫描 <see cref="IComponentSystem{TComponent}"/> 实现，在 dual-trigger init
    /// 时通过 <c>ComponentSystemHooks&lt;TComponent&gt;.AttachHook += sys.OnAttach</c> 挂钩；
    /// <see cref="EntityPureComponentExtensions.AddComponent{T}"/> / <see cref="EntityPureComponentExtensions.RemoveComponent{T}"/>
    /// 内部触发对应 hook。
    ///
    /// 关键好处：
    /// - 业务调 <c>entity.AddComponent(hc)</c> 即触发 HealthSystem.OnAttach，无需手动 dispatch
    /// - 多 System 监听同 Component（multi-cast delegate）
    /// - struct Component 不装箱（call-site 泛型 T 是静态类型）
    ///
    /// 线程安全：仅主线程调用（与 ModuleHost / EntityWorld 一致）。
    /// </summary>
    public static class ComponentSystemHooks<TComponent> where TComponent : IPureComponent
    {
        /// <summary>Component 被 attach 到 Entity 时触发的 hook（multi-cast）。</summary>
        public static event Action<Entity, TComponent> AttachHook;

        /// <summary>Component 从 Entity detach 时触发的 hook（multi-cast）。</summary>
        public static event Action<Entity, TComponent> DetachHook;

        /// <summary>框架内部：触发 AttachHook。</summary>
        internal static void InvokeAttach(Entity entity, TComponent component)
            => AttachHook?.Invoke(entity, component);

        /// <summary>框架内部：触发 DetachHook。</summary>
        internal static void InvokeDetach(Entity entity, TComponent component)
            => DetachHook?.Invoke(entity, component);

        /// <summary>测试用：清空所有 hook（生产代码不应调用）。</summary>
        internal static void ClearForTests()
        {
            AttachHook = null;
            DetachHook = null;
        }
    }
}
