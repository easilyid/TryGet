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
    /// - V2.0 C11：body 由 await 路径的 <c>Awaiter.GetResult</c> 在消费侧自动归还；
    ///   <see cref="Return"/> 仅在「任务从未被 await」时手动归还有效（带 version 守卫防双重归还）。
    /// - 框架内部热路径（TGTaskScheduler / TimerModuleAsyncExtensions）通过 <see cref="Rent"/> /
    ///   <see cref="Recycle"/> 复用 tcs 对象本身，消除每次调度的堆分配。
    /// </summary>
    public sealed class TGTaskCompletionSource
    {
        /// <summary>tcs 对象池最大容量，超出直接 GC。</summary>
        internal const int MaxSourcePoolSize = 64;

        private static readonly System.Collections.Generic.Stack<TGTaskCompletionSource> _sourcePool =
            new System.Collections.Generic.Stack<TGTaskCompletionSource>();

        private TGTaskBody _body;
        private int _version;

        public TGTaskCompletionSource()
        {
            Arm();
        }

        /// <summary>从池中取一个就绪的 tcs（body 已 Rent）。框架内部热路径用；业务可直接 new。</summary>
        internal static TGTaskCompletionSource Rent()
        {
            if (_sourcePool.Count > 0)
            {
                var tcs = _sourcePool.Pop();
                tcs.Arm();
                return tcs;
            }
            return new TGTaskCompletionSource();
        }

        /// <summary>
        /// 把 tcs 对象本身归还池（不归还 body —— body 由消费侧 await 路径回池）。
        /// 仅在完成（SetResult/SetException/SetCanceled）之后调用；调用后此 tcs 不可再使用。
        /// </summary>
        internal static void Recycle(TGTaskCompletionSource tcs)
        {
            if (tcs == null || tcs._body == null) return;
            tcs._body = null;
            if (_sourcePool.Count >= MaxSourcePoolSize) return;
            _sourcePool.Push(tcs);
        }

        /// <summary>池中缓存的 tcs 对象数（诊断/测试用）。</summary>
        internal static int PooledSourceCount => _sourcePool.Count;

        /// <summary>清空 tcs 池（测试用）。</summary>
        internal static void ClearSourcePool()
        {
            _sourcePool.Clear();
        }

        private void Arm()
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
        /// 手动把 body 归还到 Pool（仅当任务从未被 await 消费时需要；带 version 守卫，
        /// body 已被消费侧归还时本调用为 no-op）。调用后此 tcs 不可再使用。
        /// </summary>
        public void Return()
        {
            if (_body == null) return;
            if (_version == _body.Version)
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
    /// 带返回值的版本。body 同样由消费侧 await 路径自动归还；<see cref="Return"/> 带 version 守卫。
    /// （泛型版暂不做 tcs 对象池化——框架内部热路径仅使用非泛型版。）
    /// </summary>
    public sealed class TGTaskCompletionSource<T>
    {
        private TGTaskBody<T> _body;
        private int _version; // 修复：去掉 readonly，与非泛型版一致（避免 copy-paste bug）

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

        /// <summary>
        /// 手动把 body 归还到 Pool（仅当任务从未被 await 消费时需要；带 version 守卫，
        /// body 已被消费侧归还时本调用为 no-op）。调用后此 tcs 不可再使用。
        /// </summary>
        public void Return()
        {
            if (_body == null) return;
            if (_version == _body.Version)
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
