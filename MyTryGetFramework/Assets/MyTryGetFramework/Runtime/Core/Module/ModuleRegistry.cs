using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：Source Generator 自动注册 Module 的运行时收集器。
    ///
    /// 工作流：
    /// 1. 生成的 <c>__AssemblyManifest_&lt;asm&gt;</c> 类在 <c>[ModuleInitializer]</c> /
    ///    <c>[RuntimeInitializeOnLoadMethod]</c> 触发时调 <see cref="Register"/> 注册一个委托
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
        /// 测试用：清空所有注册（生产代码不应调用）。
        /// </summary>
        internal static void ClearForTests()
        {
            _registrations.Clear();
        }
    }
}
