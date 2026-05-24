using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// 框架启动工具集（V0.7 起新增）。
    ///
    /// 用途：标准化 <see cref="ModuleHost"/> 创建 + 注册 Core 基础服务（<see cref="ILogger"/> +
    /// <see cref="IClock"/> + <see cref="ITGTaskScheduler"/>）的样板。业务调用 <see cref="CreateHost"/>
    /// 拿到预配置 host，再注册自己的 Module 和 <see cref="IModuleHost.Initialize"/>。
    ///
    /// **不定义 IEntry interface**：
    /// 参考 Fantasy <c>Platform.Unity.Entry</c> + <c>Platform.Console.Entry</c> / ET <c>Init.cs</c> +
    /// <c>Program.cs</c> / BigCat <c>GameLauncher</c> 实践，无任何商业 Unity 框架定义 IEntry 接口。
    /// 原因：Unity MonoBehaviour 入口（无返回，事件驱动）与 .NET <c>Main(string[])</c>（int 返回，主循环）
    /// 形态本质不同，强行抽象 interface 增加心智成本不带来灵活性。Bootstrap 只做"创建并预填基础 Module"。
    ///
    /// 平台 Entry 类各自实现（Unity 端：MonoBehaviour 启动；Net 端：<c>Samples/Net/Entry.cs</c> 静态类）。
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>
        /// 创建 ModuleHost 并注册 Core 基础三件套（<see cref="ILogger"/> + <see cref="IClock"/> +
        /// <see cref="ITGTaskScheduler"/>）。
        /// V0.9.5 起：调用 <see cref="AssemblyManifestRegistry.ApplyAll"/> 应用所有 Source Generator
        /// 自动生成的 Module 注册。业务调用后可继续手动 <see cref="IModuleHost.Register{T}"/> 自己的
        /// Module，最后调 <see cref="IModuleHost.Initialize"/>。
        /// </summary>
        /// <param name="options">可选配置，允许业务注入自定义 Logger/Clock/Scheduler。</param>
        public static IModuleHost CreateHost(BootstrapOptions options = null)
        {
            options ??= BootstrapOptions.Default;

            var host = new ModuleHost();
            host.Register<ILogger>(options.Logger ?? new ConsoleLogger { MinimumLevel = options.MinimumLogLevel });
            host.Register<IClock>(options.Clock ?? new SystemClock());
            host.Register<ITGTaskScheduler>(options.Scheduler ?? new TGTaskScheduler());

            // V0.9.5：应用 Source Generator 在 [ModuleInitializer] / [RuntimeInitializeOnLoadMethod]
            // 阶段累计的 [Module] 自动注册委托
            AssemblyManifestRegistry.ApplyAll(host);

            return host;
        }
    }
}
