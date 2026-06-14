using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：Source Generator 自动 Subscribe EventModule handler 的运行时收集器。
    ///
    /// 工作流：
    /// 1. 生成的 <c>__EventHandlerManifest_&lt;asm&gt;</c> 类在 <c>[ModuleInitializer]</c> /
    ///    <c>[RuntimeInitializeOnLoadMethod]</c> 触发时调 <see cref="RegisterWithMetadata"/> +
    ///    <see cref="Register"/> 注册元数据 + 执行委托（双轨：新生成器生成双调用、旧生成代码只调 Register 仍可用）
    /// 2. <see cref="GameLauncher.CreateHost"/> 在 host 创建后调 <see cref="ApplyAll"/> 把
    ///    所有 handler Subscribe 到 host.EventModule
    /// 3. 业务可继续手动 <see cref="IEventModule.Subscribe{T}"/> 自己的 handler
    /// </summary>
    public static class EventHandlerRegistry
    {
        private static readonly List<Action<IEventModule>> _registrations = new List<Action<IEventModule>>();
        private static readonly List<EventHandlerRegistrationInfo> _metadata = new List<EventHandlerRegistrationInfo>();
        private static int _claimedMetadataCount;
        private static int _legacyRegistrationCount;

        /// <summary>
        /// C5：注册 EventHandler 的结构化元数据（handler 签名/事件类型/来源 assembly）。
        /// 生成代码在调 <see cref="Register"/> 前先调此方法，让 <see cref="Snapshot"/> 可返回结构化信息。
        /// </summary>
        public static void RegisterWithMetadata(string handlerSignature, Type eventType, string sourceAssembly)
        {
            if (handlerSignature == null) throw new ArgumentNullException(nameof(handlerSignature));
            if (eventType == null) throw new ArgumentNullException(nameof(eventType));
            _metadata.Add(new EventHandlerRegistrationInfo(handlerSignature, eventType, sourceAssembly));
        }

        /// <summary>
        /// 注册一个 Subscribe 委托。生成代码（<c>__EventHandlerManifest_&lt;asm&gt;</c>）调此方法。
        /// 业务一般不应直接调；业务使用 <see cref="EventHandlerAttribute"/> 让 Generator 自动生成。
        /// </summary>
        public static void Register(Action<IEventModule> registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            _registrations.Add(registration);

            int pendingMetadataCount = _metadata.Count - _claimedMetadataCount;
            if (pendingMetadataCount > 0)
                _claimedMetadataCount = _metadata.Count;
            else
                _legacyRegistrationCount++;
        }

        /// <summary>
        /// 对指定 EventModule 应用所有已注册的委托。<see cref="GameLauncher.CreateHost"/> 内部调用。
        /// </summary>
        public static void ApplyAll(IEventModule bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            for (int i = 0; i < _registrations.Count; i++)
                _registrations[i](bus);
        }

        /// <summary>已注册的委托数量（诊断用）。</summary>
        public static int Count => _registrations.Count;

        /// <summary>
        /// C5：获取当前注册的结构化快照（handler 签名/事件类型/来源 assembly）。
        /// 旧生成代码（只调 <see cref="Register"/>、未调 <see cref="RegisterWithMetadata"/>）的注册
        /// 会显示为 handlerSignature="unknown"、eventType=null、sourceAssembly="unknown"。
        /// </summary>
        public static IReadOnlyList<EventHandlerRegistrationInfo> Snapshot()
        {
            // 新生成器对单个程序集发 N 条 RegisterWithMetadata（每 [EventHandler] 一条）+ 1 个 Register 委托，
            // 因此 _metadata.Count 才是 handler 数，_registrations.Count 只是「程序集/批次数」。
            // 历史实现按 _registrations.Count 分配，会在「单程序集多 handler」时把多出的 metadata 截断丢弃。
            // Register 时记录没有对应 metadata 的 legacy 委托，避免「N metadata + 1 新委托 + legacy 委托」
            // 被 _registrations.Count - _metadata.Count 错算。
            int legacyCount = _legacyRegistrationCount;
            var result = new EventHandlerRegistrationInfo[_metadata.Count + legacyCount];
            for (int i = 0; i < _metadata.Count; i++)
                result[i] = _metadata[i];
            for (int i = 0; i < legacyCount; i++)
                result[_metadata.Count + i] = new EventHandlerRegistrationInfo("unknown", null, "unknown");
            return result;
        }

        /// <summary>测试用：清空所有注册（生产代码不应调用）。</summary>
        internal static void ClearForTests()
        {
            _registrations.Clear();
            _metadata.Clear();
            _claimedMetadataCount = 0;
            _legacyRegistrationCount = 0;
        }
    }
}
