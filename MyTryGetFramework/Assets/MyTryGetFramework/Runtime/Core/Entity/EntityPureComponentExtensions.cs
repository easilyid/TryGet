using System;

namespace TryGet
{
    /// <summary>
    /// <see cref="IPureComponent"/> ECS 二级方案的 <see cref="Entity"/> 扩展（V0.9 起）。
    ///
    /// 与 <see cref="Entity.Attach"/> / <see cref="Entity.GetAspect{T}"/> 的 Aspect 通道完全独立 —
    /// 两条 ECS 路线并存，互不干扰（见 ADR-0017）。
    ///
    /// 设计要点：
    /// - 使用扩展方法而非 Entity 直接成员，表达 PureComponent 是"二级方案"
    /// - V0.9 不引入自动 OnAttach/OnDetach 调度：业务显式调 <see cref="IComponentSystem{T}"/>
    /// - struct Component 走 boxed 存储（Dictionary 装箱）；V0.9.5 Source Gen 后评估泛型化
    /// </summary>
    public static class EntityPureComponentExtensions
    {
        /// <summary>
        /// 添加 PureComponent 到 Entity。
        /// 同类型重复添加抛 <see cref="InvalidOperationException"/>；Entity 已销毁抛 <see cref="InvalidOperationException"/>。
        /// V0.9.5 起：成功添加后触发 <see cref="ComponentSystemHooks{T}.AttachHook"/>（若有 [IComponentSystem] 自动注册）。
        /// </summary>
        public static void AddComponent<T>(this Entity entity, T component) where T : IPureComponent
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (component == null) throw new ArgumentNullException(nameof(component));
            entity.AddPureComponentInternal(typeof(T), component);
            ComponentSystemHooks<T>.InvokeAttach(entity, component);
        }

        /// <summary>
        /// 获取 PureComponent。未注册时返回 <c>default(T)</c>（class 为 null，struct 为零结构）。
        /// 若 T 为 struct 类型，调用前请先用 <see cref="HasComponent{T}"/> 判定，以区分"零值 component"与"未注册"。
        /// </summary>
        public static T GetComponent<T>(this Entity entity) where T : IPureComponent
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.TryGetPureComponentInternal(typeof(T), out var component))
                return (T)component;
            return default;
        }

        /// <summary>判断 Entity 是否含指定类型的 PureComponent。</summary>
        public static bool HasComponent<T>(this Entity entity) where T : IPureComponent
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return entity.HasPureComponentInternal(typeof(T));
        }

        /// <summary>
        /// 移除 PureComponent。Entity 已销毁抛 <see cref="InvalidOperationException"/>。
        /// V0.9.5 起：成功移除后触发 <see cref="ComponentSystemHooks{T}.DetachHook"/>。
        /// </summary>
        /// <returns>是否真的移除了。</returns>
        public static bool RemoveComponent<T>(this Entity entity) where T : IPureComponent
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // 先取到将被移除的 component，方便触发 DetachHook
            if (!entity.TryGetPureComponentInternal(typeof(T), out var existing))
                return false;

            bool removed = entity.RemovePureComponentInternal(typeof(T));
            if (removed)
                ComponentSystemHooks<T>.InvokeDetach(entity, (T)existing);
            return removed;
        }

        /// <summary>当前 PureComponent 数量。</summary>
        public static int ComponentCount(this Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return entity.PureComponentCountInternal;
        }
    }
}
