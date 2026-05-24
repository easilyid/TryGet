using System;

namespace TryGet
{
    /// <summary>
    /// <see cref="Entity"/> 的事件 scope 扩展（V0.7 Iter 4）。
    /// 现有 <see cref="Entity.Subscribe{T}"/> 保留共存（无破坏）。
    /// </summary>
    public static class EntityEventScopeExtensions
    {
        /// <summary>
        /// 订阅 Entity 事件并把"解绑动作"注册到 scope。
        /// scope.Dispose 时自动调 <see cref="Entity.Unsubscribe{T}"/>。
        ///
        /// 注意：Entity 销毁时 EventDispatcher 会主动 Clear 所有 handler，
        /// 但 scope-level 解绑保证"业务对象先于 Entity 销毁"场景下 handler 也能正确释放。
        /// </summary>
        public static void Subscribe<T>(this Entity entity, Action<T> handler, EventScope scope) where T : struct
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            entity.Subscribe(handler);
            scope.Register(() =>
            {
                // Entity 已销毁时 Unsubscribe 仍可安全调用（EventDispatcher.Clear 已 reset，Unsubscribe no-op）
                if (!entity.IsDestroyed)
                    entity.Unsubscribe(handler);
            });
        }
    }
}
