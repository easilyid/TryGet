using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TryGet.Async
{
    /// <summary>
    /// TryGet 跨端异步原语（V0.6 起，自研路线）。命名采用 "TG" 品牌前缀（与 ET/F/H 单/双字母前缀一致），
    /// 避免与 <see cref="System.Threading.Tasks.Task"/> 重名造成 using 冲突。
    ///
    /// 设计要点（参考 hsenl HTask）：
    /// - <see langword="struct"/> 形态，按值传递零分配（不含 body）。
    /// - 内部持 <see cref="ITGTaskBody"/> 状态机体（class，由 <see cref="TGTaskPool"/> 复用）。
    /// - <c>_version</c> 字段防止 struct 被复制后误 await 已回收的 body。
    /// - 仅支持 await 一次；多次 await 同一 TGTask = 用户错误。
    ///
    /// 使用模式：
    /// <code>
    /// async TGTask MyMethod() { await timer.WaitAsync(1f); }
    /// </code>
    ///
    /// 静态工厂（对标 .NET Task.FromResult / UniTask.CompletedTask）：
    /// - <see cref="CompletedTask"/>
    /// - <see cref="FromException(Exception)"/>
    /// - <see cref="FromCanceled()"/>
    ///
    /// 单线程模型：<see cref="Awaiter.OnCompleted"/> 同步执行 continuation 或入队 <see cref="ITGTaskScheduler"/>。
    /// V0.6 不引入 ThreadPool。
    ///
    /// 设计取舍详见 <c>docs/design/V0.6-ITask.md</c>（PRD）和 <c>docs/design/V0.6-Iter8-naming-review.md</c>（命名审视）。
    /// </summary>
    [AsyncMethodBuilder(typeof(AsyncTGTaskMethodBuilder))]
    [StructLayout(LayoutKind.Auto)]
    public readonly partial struct TGTask
    {
        internal readonly ITGTaskBody Body;
        internal readonly int Version;
        internal readonly TGTaskType TaskType;

        internal TGTask(ITGTaskBody body, int version, TGTaskType taskType)
        {
            Body = body;
            Version = version;
            TaskType = taskType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Awaiter GetAwaiter() => new Awaiter(this);

        /// <summary>
        /// 任务是否已完成。body 已被消费归还（version 不匹配）的过期句柄视为已完成；
        /// 对过期句柄继续 GetResult 会抛 <see cref="TGTaskExpiredException"/>。
        /// </summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Body == null) return true;
                if (Version != Body.Version) return true; // 过期 = 已被消费 = 已完成
                return Body.IsCompleted;
            }
        }

        /// <summary>
        /// 显式 fire-and-forget：放弃 await，未来若发生异常会进入
        /// <see cref="TGTaskScheduler.UnobservedException"/> 全局钩子，不会无声吞掉。
        /// 对标 UniTask.Forget()。
        /// </summary>
        public void Forget()
        {
            if (Body == null) return;
            if (Version != Body.Version) return; // 已 Reset，不再追踪

            // 给 body 挂一个"异常上报"continuation，让最终 SetException 时进入全局钩子
            if (Body.IsCompleted)
            {
                // 已完成路径：手动 GetResult 检查异常（GetResult 自身吞 result，但会 rethrow exception）
                try { Body.GetResult(); }
                catch (Exception ex) { TGTaskScheduler.RaiseUnobservedException(ex); }
                if (Body is TGTaskBody tb)
                    TGTaskPool.Return(tb);
            }
            else
            {
                var capturedBody = Body;
                var capturedVersion = Version;
                Body.OnCompleted(() =>
                {
                    if (capturedVersion != capturedBody.Version) return;
                    try { capturedBody.GetResult(); }
                    catch (Exception ex) { TGTaskScheduler.RaiseUnobservedException(ex); }
                    if (capturedBody is TGTaskBody tb2)
                        TGTaskPool.Return(tb2);
                });
            }
        }

        // ----- 静态工厂方法（对标 .NET Task / UniTask） -----

        /// <summary>已完成的空 TGTask 单例。语义等价 <c>default(TGTask)</c> 但意图更明确。</summary>
        public static TGTask CompletedTask => default;

        /// <summary>立即完成、抛指定异常的 TGTask。</summary>
        public static TGTask FromException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            var body = TGTaskPool.Rent();
            body.SetException(exception);
            return new TGTask(body, body.Version, TGTaskType.Manual);
        }

        /// <summary>立即完成、已取消的 TGTask（抛 OperationCanceledException）。</summary>
        public static TGTask FromCanceled()
        {
            var body = TGTaskPool.Rent();
            body.SetException(new OperationCanceledException());
            return new TGTask(body, body.Version, TGTaskType.Manual);
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly TGTask _task;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Awaiter(TGTask task) => _task = task;

            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (_task.Body == null) return true;
                    if (_task.Version != _task.Body.Version) return true; // 过期 = 已消费
                    return _task.Body.IsCompleted;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult()
            {
                if (_task.Body == null) return;
                if (_task.Version != _task.Body.Version)
                    throw new TGTaskExpiredException();

                var body = _task.Body;
                try
                {
                    body.GetResult();
                }
                finally
                {
                    // V2.0 C11：消费侧统一归还 —— Builder 与 Manual body 均在此处回池。
                    // 双重归还由进入 try 前的 version 检查防护（第二个 struct 副本直接抛 Expired）。
                    if (body is TGTaskBody tb)
                        TGTaskPool.Return(tb);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnCompleted(Action continuation)
            {
                if (_task.Body == null) { continuation?.Invoke(); return; }
                if (_task.Version != _task.Body.Version)
                    throw new TGTaskExpiredException();
                _task.Body.OnCompleted(continuation);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void UnsafeOnCompleted(Action continuation)
            {
                if (_task.Body == null) { continuation?.Invoke(); return; }
                if (_task.Version != _task.Body.Version)
                    throw new TGTaskExpiredException();
                _task.Body.UnsafeOnCompleted(continuation);
            }
        }
    }

    /// <summary>
    /// 带返回值的异步原语。<see cref="TGTask"/> 的泛型版本。
    /// </summary>
    [AsyncMethodBuilder(typeof(AsyncTGTaskMethodBuilder<>))]
    [StructLayout(LayoutKind.Auto)]
    public readonly partial struct TGTask<T>
    {
        internal readonly ITGTaskBody<T> Body;
        internal readonly int Version;
        internal readonly TGTaskType TaskType;

        internal TGTask(ITGTaskBody<T> body, int version, TGTaskType taskType)
        {
            Body = body;
            Version = version;
            TaskType = taskType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Awaiter GetAwaiter() => new Awaiter(this);

        /// <summary>语义同 <see cref="TGTask.IsCompleted"/>：过期句柄视为已完成。</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Body == null) return true;
                if (Version != Body.Version) return true; // 过期 = 已被消费 = 已完成
                return Body.IsCompleted;
            }
        }

        /// <summary>对标 <see cref="TGTask.Forget"/>。</summary>
        public void Forget()
        {
            if (Body == null) return;
            if (Version != Body.Version) return;

            if (Body.IsCompleted)
            {
                try { Body.GetResult(); }
                catch (Exception ex) { TGTaskScheduler.RaiseUnobservedException(ex); }
                if (Body is TGTaskBody<T> tb)
                    TGTaskPool.Return(tb);
            }
            else
            {
                var capturedBody = Body;
                var capturedVersion = Version;
                Body.OnCompleted(() =>
                {
                    if (capturedVersion != capturedBody.Version) return;
                    try { capturedBody.GetResult(); }
                    catch (Exception ex) { TGTaskScheduler.RaiseUnobservedException(ex); }
                    if (capturedBody is TGTaskBody<T> tb2)
                        TGTaskPool.Return(tb2);
                });
            }
        }

        // ----- 静态工厂 -----
        public static TGTask<T> FromResult(T value)
        {
            var body = TGTaskPool.Rent<T>();
            body.SetResult(value);
            return new TGTask<T>(body, body.Version, TGTaskType.Manual);
        }

        public static TGTask<T> FromException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            var body = TGTaskPool.Rent<T>();
            body.SetException(exception);
            return new TGTask<T>(body, body.Version, TGTaskType.Manual);
        }

        public static TGTask<T> FromCanceled()
        {
            var body = TGTaskPool.Rent<T>();
            body.SetException(new OperationCanceledException());
            return new TGTask<T>(body, body.Version, TGTaskType.Manual);
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly TGTask<T> _task;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Awaiter(TGTask<T> task) => _task = task;

            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (_task.Body == null) return true;
                    if (_task.Version != _task.Body.Version) return true; // 过期 = 已消费
                    return _task.Body.IsCompleted;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetResult()
            {
                if (_task.Body == null) return default;
                if (_task.Version != _task.Body.Version)
                    throw new TGTaskExpiredException();

                var body = _task.Body;
                try
                {
                    return body.GetResult();
                }
                finally
                {
                    // V2.0 C11：消费侧统一归还（Builder 与 Manual），version 检查防双重归还
                    if (body is TGTaskBody<T> tb)
                        TGTaskPool.Return(tb);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnCompleted(Action continuation)
            {
                if (_task.Body == null) { continuation?.Invoke(); return; }
                if (_task.Version != _task.Body.Version)
                    throw new TGTaskExpiredException();
                _task.Body.OnCompleted(continuation);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void UnsafeOnCompleted(Action continuation)
            {
                if (_task.Body == null) { continuation?.Invoke(); return; }
                if (_task.Version != _task.Body.Version)
                    throw new TGTaskExpiredException();
                _task.Body.UnsafeOnCompleted(continuation);
            }
        }
    }
}
