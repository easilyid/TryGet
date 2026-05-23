using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// ILogModule 跨端默认实现：写到 System.Console（服务端友好，Unity 中也能用，但更建议用 UnityLogModule Adapter）。
    /// 单元测试中常通过 <see cref="GetCapturedEntries"/> 或自定义 <see cref="OnLog"/> 钩子检查输出。
    /// </summary>
    public sealed class ConsoleLogModule : ILogModule
    {
        private readonly List<(LogLevel level, string message)> _captured = new List<(LogLevel, string)>();
        private bool _shutdown;

        public int Priority => -1000;  // 极早初始化，让其他 Module 在 OnInit 中能用 log
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 可选钩子：每条日志输出后回调。测试或重定向用。
        /// </summary>
        public Action<LogLevel, string> OnLog { get; set; }

        /// <summary>
        /// 是否把每条日志保存到内存（便于测试断言）。默认 false。
        /// </summary>
        public bool CaptureToMemory { get; set; }

        public void OnInit(IModuleHost host) { _shutdown = false; }
        public void Shutdown() { _captured.Clear(); OnLog = null; _shutdown = true; }

        public void Debug(string message) => Write(LogLevel.Debug, message);
        public void Info(string message) => Write(LogLevel.Info, message);
        public void Warn(string message) => Write(LogLevel.Warn, message);
        public void Error(string message) => Write(LogLevel.Error, message);

        public void Error(string message, Exception exception)
        {
            Write(LogLevel.Error, exception == null ? message : $"{message} :: {exception}");
        }

        public IReadOnlyList<(LogLevel level, string message)> GetCapturedEntries() => _captured;

        private void Write(LogLevel level, string message)
        {
            // Shutdown 后静默：Module 已释放资源，再调用是误用，不应崩。
            if (_shutdown) return;

            if (level < MinimumLevel)
                return;

            if (CaptureToMemory)
                _captured.Add((level, message));

            Console.WriteLine($"[{level}] {message}");

            OnLog?.Invoke(level, message);
        }
    }
}
