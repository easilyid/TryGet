using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 把已有的 <see cref="ILogModule"/> 包装成 <see cref="ILogger"/>。
    ///
    /// 用途：V0.7 期间业务可能仍持 ILogModule 注册（无破坏），但新代码用 ILogger 接口。
    /// 此 Adapter 让业务在同一 ModuleHost 里同时注册 ILogModule（已有）和 ILogger（adapter 桥接）。
    ///
    /// 迁移建议：V0.8 起业务应直接 <c>host.Register&lt;ILogger&gt;(new ConsoleLogger())</c>，删 Adapter。
    ///
    /// <see cref="LogLevel.Trace"/> 路径：<see cref="ILogModule"/> 无 Trace 方法 → 此 Adapter 转 <see cref="ILogModule.Debug"/>
    /// （前缀 "[TRACE] " 让用户识别）。
    /// </summary>
#pragma warning disable CS0618 // Adapter 的存在意义就是引用 Obsolete 的 ILogModule；压制告警避免无谓噪音
    public sealed class LogModuleAdapter : ILogger
    {
        private readonly ILogModule _inner;

        public int Priority => _inner.Priority;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public LogModuleAdapter(ILogModule inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public LogLevel MinimumLevel
        {
            get => _inner.MinimumLevel;
            set => _inner.MinimumLevel = value;
        }

        public void OnInit(IModuleHost host) { /* 内部 ILogModule 已被注册路径独立 OnInit */ }
        public void Shutdown() { /* 同上 */ }

        public void Trace(string message) => _inner.Debug("[TRACE] " + message);
        public void Debug(string message) => _inner.Debug(message);
        public void Info(string message) => _inner.Info(message);
        public void Warn(string message) => _inner.Warn(message);
        public void Error(string message) => _inner.Error(message);
        public void Error(string message, Exception exception) => _inner.Error(message, exception);
    }
#pragma warning restore CS0618
}
