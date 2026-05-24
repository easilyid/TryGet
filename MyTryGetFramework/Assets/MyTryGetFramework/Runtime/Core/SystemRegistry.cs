using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：Source Generator 自动注册 System 的运行时收集器（与 <see cref="AssemblyManifestRegistry"/> 分开，
    /// 因 System 注册到 <see cref="EntityWorld"/> 而非 <see cref="IModuleHost"/>）。
    ///
    /// 工作流：
    /// 1. 生成的 <c>__SystemManifest_&lt;asm&gt;</c> 类在 <c>[ModuleInitializer]</c> /
    ///    <c>[RuntimeInitializeOnLoadMethod]</c> 触发时调 <see cref="Register"/> 注册一个委托
    /// 2. 业务在创建 <see cref="EntityWorld"/> 后调 <see cref="ApplyAll"/> 应用所有委托
    /// 3. 业务可继续手动 <see cref="EntityWorld.RegisterSystem"/> 自己的 System
    ///
    /// **与 AssemblyManifestRegistry 的差异**：
    /// - AssemblyManifestRegistry: 注册到 IModuleHost（单一 host），Bootstrap.CreateHost 内部自动 ApplyAll
    /// - SystemRegistry: 注册到 EntityWorld（可能多 world 实例），业务显式选择 world 应用
    ///
    /// 设计避免：framework 自动 hook 进 EntityWorld 构造器会导致多 world 场景注册歧义，且测试隔离困难。
    /// </summary>
    public static class SystemRegistry
    {
        private static readonly List<Action<EntityWorld>> _registrations = new List<Action<EntityWorld>>();

        /// <summary>
        /// 注册一个 System-注册委托。生成代码（<c>__SystemManifest_&lt;asm&gt;</c>）调此方法。
        /// 业务一般不应直接调；业务使用 <see cref="SystemRegisterAttribute"/> 让 Generator 自动生成。
        /// </summary>
        public static void Register(Action<EntityWorld> registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            _registrations.Add(registration);
        }

        /// <summary>
        /// 对指定 EntityWorld 应用所有已注册的委托。业务在 <see cref="EntityWorld"/> 构造后、注册到 host 前调用。
        /// </summary>
        public static void ApplyAll(EntityWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            for (int i = 0; i < _registrations.Count; i++)
                _registrations[i](world);
        }

        /// <summary>已注册的委托数量（诊断用）。</summary>
        public static int Count => _registrations.Count;

        /// <summary>测试用：清空所有注册（生产代码不应调用）。</summary>
        internal static void ClearForTests()
        {
            _registrations.Clear();
        }
    }
}
