using System;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：标记一个 <c>static</c> 方法为 EventBus handler，让 Source Generator 自动 Subscribe。
    ///
    /// 使用：
    /// <code>
    /// public static class GameplayHandlers
    /// {
    ///     [EventHandler]
    ///     public static void OnDamage(DamageEvent evt) { ... }
    /// }
    /// </code>
    ///
    /// Generator 扫描所有标记方法，按 assembly 分组生成 <c>__EventHandlerManifest_&lt;asm&gt;</c>，
    /// 该类在 <c>[ModuleInitializer]</c> / <c>[RuntimeInitializeOnLoadMethod]</c> 触发时
    /// 把所有 handler 注册到 <see cref="EventHandlerRegistry"/>。<see cref="Bootstrap.CreateHost"/>
    /// 内调 <see cref="EventHandlerRegistry.ApplyAll"/> 把所有 handler Subscribe 到 host.EventBus。
    ///
    /// 约束：
    /// - 方法必须 <c>public static</c>
    /// - 方法签名必须是 <c>void M(TEvent evt)</c>，其中 TEvent 是 struct（IEventBus 约束）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EventHandlerAttribute : Attribute { }
}
