namespace TryGet
{
    /// <summary>
    /// 日志级别。Trace 最详尽，Error 最严重。
    ///
    /// <see cref="Trace"/>（值 -1）低于 Debug，适合最详细诊断日志。
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
