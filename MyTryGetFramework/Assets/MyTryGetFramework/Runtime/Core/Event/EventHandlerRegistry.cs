using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：Source Generator 自动 Subscribe EventModule handler 的运行时收集器。
    ///
    /// 工作流：
    /// 1. 生成的 <c>__EventHandlerManifest_&lt;asm&gt;</c> 类在 <c>[ModuleInitializer]</c> /
    ///    <c>[RuntimeInitializeOnLoadMethod]</c> 触发时调 <see cref="Register"/> 注册一个委托
    /// 2. <see cref="GameLauncher.CreateHost"/> 在 host 创建后调 <see cref="ApplyAll"/> 把
    ///    所有 handler Subscribe 到 host.EventModule
    /// 3. 业务可继续手动 <see cref="IEventModule.Subscribe{T}"/> 自己的 handler
    /// </summary>
    public static class EventHandlerRegistry
    {
        private static readonly List<Action<IEventModule>> _registrations = new List<Action<IEventModule>>();

        /// <summary>
        /// 注册一个 Subscribe 委托。生成代码（<c>__EventHandlerManifest_&lt;asm&gt;</c>）调此方法。
        /// 业务一般不应直接调；业务使用 <see cref="EventHandlerAttribute"/> 让 Generator 自动生成。
        /// </summary>
        public static void Register(Action<IEventModule> registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            _registrations.Add(registration);
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

        /// <summary>获取当前注册委托快照（诊断用）。</summary>
        public static IReadOnlyList<Action<IEventModule>> Snapshot()
        {
            return _registrations.ToArray();
        }

        /// <summary>测试用：清空所有注册（生产代码不应调用）。</summary>
        internal static void ClearForTests()
        {
            _registrations.Clear();
        }
    }
}
