using System;

namespace TryGet
{
    /// <summary>
    /// 日志服务契约。
    ///
    /// 命名对齐 .NET 现代标准 <c>Microsoft.Extensions.Logging.ILogger</c>，但**故意保持简化**：
    /// - 无 structured logging
    /// - 无泛型 <c>ILogger&lt;T&gt;</c> + factory 模式
    /// - 单接口 + 一组方法（Trace/Debug/Info/Warn/Error）
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
