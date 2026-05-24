namespace TryGet
{
    /// <summary>
    /// 日志服务契约（Common Module）。跨端可用，不依赖 Unity API。
    /// 默认实现 <c>ConsoleLogModule</c>；Unity 侧可通过 Adapter 替换为 <c>UnityLogModule</c>（写 UnityEngine.Debug）。
    /// </summary>
    [System.Obsolete("Use ILogger (V0.7+). ILogModule will be removed in V0.8. Migration: replace " +
        "host.Register<ILogModule>(new ConsoleLogModule()) with host.Register<ILogger>(new ConsoleLogger()), " +
        "or use LogModuleAdapter to bridge existing ILogModule.")]
    public interface ILogModule : IModule
    {
        /// <summary>
        /// 最低输出级别。低于此级别的日志被丢弃。运行时可改。
        /// </summary>
        LogLevel MinimumLevel { get; set; }

        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);

        /// <summary>
        /// 带异常的错误日志。
        /// </summary>
        void Error(string message, System.Exception exception);
    }
}
