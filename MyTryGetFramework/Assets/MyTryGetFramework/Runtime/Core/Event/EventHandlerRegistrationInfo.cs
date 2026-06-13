using System;

namespace TryGet
{
    /// <summary>
    /// C5 — EventHandlerRegistry 诊断快照条目。
    ///
    /// 替代当前 <see cref="EventHandlerRegistry.Snapshot"/> 返回不透明 <c>Action&lt;IEventModule&gt;</c> 的设计，
    /// 改为返回结构化信息，支持调试时查看"注册了哪些 handler、监听什么事件、来自哪个 assembly"。
    /// </summary>
    public readonly struct EventHandlerRegistrationInfo
    {
        /// <summary>Handler 方法签名（例 "GameplayHandlers.OnTickEvent"）。</summary>
        public readonly string HandlerSignature;

        /// <summary>事件类型（例 typeof(TickEvent)）。</summary>
        public readonly Type EventType;

        /// <summary>
        /// 来源 assembly 名称（生成的 manifest 类所在 assembly）。
        /// null 表示未知来源（手动注册或旧生成代码）。
        /// </summary>
        public readonly string SourceAssembly;

        public EventHandlerRegistrationInfo(string handlerSignature, Type eventType, string sourceAssembly)
        {
            HandlerSignature = handlerSignature;
            EventType = eventType;
            SourceAssembly = sourceAssembly;
        }

        public override string ToString() =>
            $"{HandlerSignature} handles {EventType?.Name} (from {SourceAssembly ?? "unknown"})";
    }
}
