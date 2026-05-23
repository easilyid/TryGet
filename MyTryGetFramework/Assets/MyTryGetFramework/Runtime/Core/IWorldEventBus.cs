using System;

namespace TryGet
{
    /// <summary>
    /// EntityWorld 级别局部事件总线（World-scope），与 <see cref="IEventBus"/>（Global-scope）并存。
    ///
    /// 设计意图（V0.5 澄清）：两个 EventBus 不是债，是合理的作用域隔离：
    /// - <see cref="IEventBus"/>（ModuleHost.EventBus）：跨 World 全局事件，Module/Adapter/业务共享
    /// - <see cref="IWorldEventBus"/>（EntityWorld.EventBus）：单个 World 内部事件，
    ///   Aspect/System 用于 Entity 间通信，**不会跨 World 污染**
    ///
    /// 接口签名 100% 复用 IEventBus，仅作为类型标识区分作用域。
    /// （V0.3 曾误标 Deprecated 计划合并，V0.5 撤销 — 见 design.md §13）
    /// </summary>
    public interface IWorldEventBus : IEventBus
    {
    }
}
