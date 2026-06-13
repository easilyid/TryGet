using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：Source Generator 自动注册 Module 的运行时收集器。
    ///
    /// 工作流：
    /// 1. 生成的 <c>__AssemblyManifest_&lt;asm&gt;</c> 类在 <c>[ModuleInitializer]</c> /
    ///    <c>[RuntimeInitializeOnLoadMethod]</c> 触发时调 <see cref="RegisterWithMetadata"/> +
    ///    <see cref="Register"/> 注册元数据 + 执行委托（双轨：新生成器生成双调用、旧生成代码只调 Register 仍可用）
    /// 2. <see cref="GameLauncher.CreateHost"/> 在 host 基础服务注册完成后调 <see cref="ApplyAll"/>
    ///    把累计的所有委托应用到新 host 上
    /// 3. 业务可继续手动 <see cref="IModuleSystem.Register{T}"/> 自己的 Module
    /// 4. 最终调 <see cref="IModuleSystem.Initialize"/>
    ///
    /// **线程安全**：仅在主线程调用（与 ModuleSystem 一致）。<c>[ModuleInitializer]</c> 在
    /// 单 host 进程下都是 main-thread；<c>[RuntimeInitializeOnLoadMethod]</c> 在 Unity 主线程。
    ///
    /// **限制**：仅看到在 <see cref="ApplyAll"/> 调用之前已 static-init 的 assemblies 的注册。
    /// 后加载的 assembly 注册不会回填到已构造的 host。
    /// </summary>
    public static class ModuleRegistry
    {
        private static readonly List<Action<IModuleSystem>> _registrations = new List<Action<IModuleSystem>>();
        private static readonly List<ModuleRegistrationInfo> _metadata = new List<ModuleRegistrationInfo>();

        /// <summary>
        /// C5：注册 Module 的结构化元数据（类型/服务接口/来源 assembly）。
        /// 生成代码在调 <see cref="Register"/> 前先调此方法，让 <see cref="Snapshot"/> 可返回结构化信息。
        /// 手动调用（业务代码）可选——不调只会让该注册在 Snapshot 中显示为 unknown 来源，不影响功能。
        /// </summary>
        public static void RegisterWithMetadata(Type implementationType, Type serviceType, string sourceAssembly)
        {
            if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            _metadata.Add(new ModuleRegistrationInfo(implementationType, serviceType, sourceAssembly));
        }

        /// <summary>
        /// 注册一个 Module-注册委托。生成代码（<c>__AssemblyManifest_&lt;asm&gt;</c>）调此方法。
        /// 业务一般不应直接调；业务使用 <see cref="ModuleAttribute"/> 让 Generator 自动生成。
        /// </summary>
        public static void Register(Action<IModuleSystem> registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            _registrations.Add(registration);
        }

        /// <summary>
        /// 对指定 host 应用所有已注册的委托。<see cref="GameLauncher.CreateHost"/> 内部调用。
        /// </summary>
        public static void ApplyAll(IModuleSystem host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            for (int i = 0; i < _registrations.Count; i++)
                _registrations[i](host);
        }

        /// <summary>
        /// 已注册的委托数量（诊断用）。
        /// </summary>
        public static int Count => _registrations.Count;

        /// <summary>
        /// C5：获取当前注册的结构化快照（类型/服务接口/来源 assembly）。
        /// 旧生成代码（只调 <see cref="Register"/>、未调 <see cref="RegisterWithMetadata"/>）的注册
        /// 会显示为 implementationType/serviceType=null、sourceAssembly="unknown"。
        /// </summary>
        public static IReadOnlyList<ModuleRegistrationInfo> Snapshot()
        {
            // _metadata.Count 可能 < _registrations.Count（旧生成代码或手动注册未调 RegisterWithMetadata）
            var result = new ModuleRegistrationInfo[_registrations.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (i < _metadata.Count)
                    result[i] = _metadata[i];
                else
                    result[i] = new ModuleRegistrationInfo(null, null, "unknown");
            }
            return result;
        }

        /// <summary>
        /// 测试用：清空所有注册（生产代码不应调用）。
        /// </summary>
        internal static void ClearForTests()
        {
            _registrations.Clear();
            _metadata.Clear();
        }
    }
}
