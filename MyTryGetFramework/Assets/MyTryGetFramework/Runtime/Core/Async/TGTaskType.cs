namespace TryGet.Async
{
    /// <summary>
    /// TGTask 的内部状态机来源：
    /// - <see cref="Builder"/>：由 <see cref="AsyncTGTaskMethodBuilder"/> 创建（即 <c>async TGTask MyMethod()</c> 的返回值）。
    /// - <see cref="Manual"/>：由 <see cref="TGTaskCompletionSource"/> 手动创建。
    ///
    /// 区分目的：禁止用户对 Builder 类型的 TGTask 调用 SetResult/SetException —— 那应该由状态机
    /// 控制，用户介入会让 await 链状态错乱（参考 hsenl HTask:64-67 的 _taskType 限制）。
    /// </summary>
    internal enum TGTaskType : byte
    {
        Builder = 0,
        Manual = 1,
    }
}
