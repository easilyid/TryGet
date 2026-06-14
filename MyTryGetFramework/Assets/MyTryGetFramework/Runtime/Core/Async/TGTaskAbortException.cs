using System;

namespace TryGet.Async
{
    /// <summary>
    /// TGTask 被取消时抛出（owner-scope 取消 / <see cref="TGCancelToken"/> / <see cref="TGTask.Abort"/>）。
    ///
    /// 继承 <see cref="OperationCanceledException"/>：与 .NET 取消语义一致（await 处 rethrow，async 链自然展开），
    /// 同时作为"类型化取消标记"，让 <see cref="TGTask.Forget"/> 与 <c>AsyncTGTaskMethodBuilder</c> 把它识别为
    /// **预期取消**而不上报 <see cref="TGTaskScheduler.UnobservedException"/>（ADR-0021 D5）。对标 hsenl <c>HTaskAborter</c>。
    /// </summary>
    public sealed class TGTaskAbortException : OperationCanceledException
    {
        public TGTaskAbortException()
            : base("TGTask was canceled (owner-scope cancel / TGCancelToken / Abort).")
        {
        }

        public TGTaskAbortException(string message) : base(message) { }
    }
}
