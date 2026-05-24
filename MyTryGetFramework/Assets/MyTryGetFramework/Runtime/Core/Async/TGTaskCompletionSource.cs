using System;

namespace TryGet.Async
{
    /// <summary>
    /// 用户手动控制完成的 TGTask 生产者。对标 .NET <see cref="System.Threading.Tasks.TaskCompletionSource{TResult}"/>
    /// 和 UniTask.UniTaskCompletionSource。
    ///
    /// 典型用法（用于把"非 async 函数"包装成异步等待源）：
    /// <code>
    /// var tcs = new TGTaskCompletionSource();
    /// scheduler.OnSomeEvent += (sender, e) => tcs.SetResult();
    /// await tcs.Task;
    /// </code>
    ///
    /// 设计要点：
    /// - 内部持 <see cref="TGTaskBody"/>（class，从 Pool Rent）+ <see cref="_version"/> 快照。
    /// - <see cref="Task"/> 返回 Manual 类型的 <see cref="TGTask"/>，与 builder 创建的区分开。
    /// - <see cref="SetResult"/> / <see cref="SetException"/> / <see cref="SetCanceled"/> 仅能调用一次（二次调用静默忽略，对齐 .NET TrySet 语义）。
    /// - <see cref="Return"/> 显式归还 body 到 Pool；不调也不漏（body 由 GC 回收）。
    /// </summary>
    public sealed class TGTaskCompletionSource
    {
        private TGTaskBody _body;
        private readonly int _version;

        public TGTaskCompletionSource()
        {
            _body = TGTaskPool.Rent();
            _version = _body.Version;
        }

        /// <summary>
        /// 关联的 TGTask（Manual 类型）。用户 await 此 Task，等待 SetResult/SetException/SetCanceled 触发完成。
        /// </summary>
        public TGTask Task => new TGTask(_body, _version, TGTaskType.Manual);

        public void SetResult()
        {
            EnsureNotExpired();
            _body.SetResult();
        }

        public void SetException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            EnsureNotExpired();
            _body.SetException(ex);
        }

        public void SetCanceled()
        {
            EnsureNotExpired();
            _body.SetException(new OperationCanceledException());
        }

        /// <summary>
        /// 把 body 归还到 Pool。完成后调用，让 body 可被复用。
        /// 不调用也不漏（body 会被 GC），仅影响性能。调用后此 tcs 不可再使用。
        /// </summary>
        public void Return()
        {
            if (_body == null) return;
            TGTaskPool.Return(_body);
            _body = null;
        }

        private void EnsureNotExpired()
        {
            if (_body == null)
                throw new TGTaskExpiredException("TGTaskCompletionSource has been returned and is no longer usable.");
            if (_version != _body.Version)
                throw new TGTaskExpiredException("TGTaskCompletionSource's body has been reset externally.");
        }
    }

    /// <summary>
    /// 带返回值的版本。
    /// </summary>
    public sealed class TGTaskCompletionSource<T>
    {
        private TGTaskBody<T> _body;
        private readonly int _version;

        public TGTaskCompletionSource()
        {
            _body = TGTaskPool.Rent<T>();
            _version = _body.Version;
        }

        public TGTask<T> Task => new TGTask<T>(_body, _version, TGTaskType.Manual);

        public void SetResult(T value)
        {
            EnsureNotExpired();
            _body.SetResult(value);
        }

        public void SetException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            EnsureNotExpired();
            _body.SetException(ex);
        }

        public void SetCanceled()
        {
            EnsureNotExpired();
            _body.SetException(new OperationCanceledException());
        }

        public void Return()
        {
            if (_body == null) return;
            TGTaskPool.Return(_body);
            _body = null;
        }

        private void EnsureNotExpired()
        {
            if (_body == null)
                throw new TGTaskExpiredException("TGTaskCompletionSource<T> has been returned and is no longer usable.");
            if (_version != _body.Version)
                throw new TGTaskExpiredException("TGTaskCompletionSource<T>'s body has been reset externally.");
        }
    }
}
