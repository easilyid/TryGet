using System;

namespace TryGet
{
    /// <summary>
    /// 日志服务契约（V0.7 起新增，替代 <see cref="ILogModule"/>）。
    ///
    /// 命名对齐 .NET 现代标准 <c>Microsoft.Extensions.Logging.ILogger</c>，但**故意保持简化**：
    /// - 无 structured logging（V1.0+ 评估升级）
    /// - 无泛型 <c>ILogger&lt;T&gt;</c> + factory 模式（避免心智复杂度）
    /// - 单接口 + 一组方法（Trace/Debug/Info/Warn/Error）
    ///
    /// 迁移路径（V0.7 → V0.8）：
    /// - V0.7：<see cref="ILogger"/> 与 <see cref="ILogModule"/> 共存；<see cref="LogModuleAdapter"/> 把 ILogModule 桥接为 ILogger
    /// - V0.8：删除 ILogModule + ConsoleLogModule + LogModuleAdapter
    ///
    /// 对标：Fantasy/hsenl `ILog` / TEngine `ILogHelper` / .NET `ILogger`。
    /// 选择 .NET `ILogger` 命名是因为这是更宽泛的现代标准。
    /// </summary>
    public interface ILogger : IModule
    {
        /// <summary>最低输出级别。低于此级别的日志被丢弃。运行时可改。</summary>
        LogLevel MinimumLevel { get; set; }

        void Trace(string message);
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);

        /// <summary>带异常的错误日志。</summary>
        void Error(string message, Exception exception);
    }
}
