using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// <see cref="GameLauncher.CreateHost"/> 的可选参数。
    /// 默认值见 <see cref="Default"/>；业务通过属性赋值定制注入实例。
    ///
    /// 注意：注入实例不是 null 时，<see cref="GameLauncher.CreateHost"/> 使用注入实例本身；
    /// null 时使用默认实现（<see cref="ConsoleLogger"/> / <see cref="SystemClock"/> /
    /// <see cref="TGTaskScheduler"/>）。
    /// </summary>
    public sealed class GameLauncherOptions
    {
        /// <summary>不带任何注入的默认配置。</summary>
        public static GameLauncherOptions Default => new GameLauncherOptions();

        public ILogger Logger { get; set; }
        public IClock Clock { get; set; }
        public ITGTaskScheduler Scheduler { get; set; }

        /// <summary>默认 <see cref="ConsoleLogger"/> 的初始 MinimumLevel。仅在 <see cref="Logger"/> 为 null 时生效。</summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
    }
}
