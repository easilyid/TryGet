using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 事件订阅作用域（V0.7 起新增）。
    ///
    /// <see cref="IDisposable.Dispose"/> 时批量解绑通过此 scope 注册的所有事件 handler。
    /// 防止订阅者销毁后 handler 仍持引用导致泄漏。
    ///
    /// 创新点：所有参考 Unity 框架（Fantasy/ET/BigCat/TEngine/hsenl）均未实现此机制，订阅泄漏需手动 Unsubscribe。
    /// 灵感来源 <c>Microsoft.Extensions.Logging.ILogger.BeginScope</c>（logging 上下文），TryGet 把此模式扩展到 EventBus。
    ///
    /// 跨 bus：同一 scope 可同时关联 <see cref="IEventBus"/> 和 <see cref="Entity"/> 的订阅 —
    /// scope.Dispose 一次性解绑所有该 scope 内的 handler。
    ///
    /// 用法：
    /// <code>
    /// using (var scope = bus.CreateScope())
    /// {
    ///     bus.Subscribe&lt;MyEvent&gt;(OnMyEvent, scope);
    ///     entity.Subscribe&lt;Hit&gt;(OnHit, scope);
    ///     // ... 业务逻辑
    /// } // scope.Dispose 自动解绑 OnMyEvent + OnHit
    /// </code>
    /// </summary>
    public interface IEventScope : IDisposable
    {
        /// <summary>scope 是否已被 Dispose。</summary>
        bool IsDisposed { get; }
    }

    /// <summary>
    /// <see cref="IEventScope"/> 默认实现。
    /// 内部维护"解绑动作"列表，Dispose 时倒序执行（与订阅顺序逆序对齐）。
    /// 重复 Dispose 静默 no-op。
    /// </summary>
    public sealed class EventScope : IEventScope
    {
        private readonly List<Action> _unsubscribers = new List<Action>();

        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 框架内部：在订阅时注册"解绑动作"。<see cref="IEventBus"/> / <see cref="Entity"/> 的 scope 扩展方法调用。
        /// 业务不应直接调用。
        /// </summary>
        public void Register(Action unsubscriber)
        {
            if (unsubscriber == null) throw new ArgumentNullException(nameof(unsubscriber));
            if (IsDisposed)
            {
                // scope 已 Dispose 后还要订阅 → 立即执行 unsubscribe（让 handler 不残留）
                try { unsubscriber(); } catch { /* 吞 */ }
                return;
            }
            _unsubscribers.Add(unsubscriber);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            for (int i = _unsubscribers.Count - 1; i >= 0; i--)
            {
                try { _unsubscribers[i](); }
                catch { /* 吞 unsubscribe 异常，保证后续 handler 继续清理 */ }
            }
            _unsubscribers.Clear();
        }

        /// <summary>当前持有的解绑动作数（诊断 / 测试用）。</summary>
        public int RegisteredCount => _unsubscribers.Count;
    }
}
