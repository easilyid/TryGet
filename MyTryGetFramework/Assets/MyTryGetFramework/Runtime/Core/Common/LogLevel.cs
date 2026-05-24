namespace TryGet
{
    /// <summary>
    /// 日志级别。Trace 最详尽，Error 最严重。
    ///
    /// V0.7 起新增 <see cref="Trace"/>（值 -1，在 Debug 之下），供新 <see cref="ILogger"/> 使用。
    /// 现有 <c>ILogModule</c> 不实现 Trace 方法（用户应迁移到 <see cref="ILogger"/>）。
    /// </summary>
    public enum LogLevel
    {
        Trace = -1,
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }
}
