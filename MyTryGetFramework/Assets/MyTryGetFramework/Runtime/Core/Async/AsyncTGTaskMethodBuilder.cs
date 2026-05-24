using System;
using System.Runtime.CompilerServices;

namespace TryGet.Async
{
    /// <summary>
    /// C# async 状态机的 Builder 驱动器（无返回值版本）。
    ///
    /// 由 C# 编译器在编译 <c>async TGTask MyMethod()</c> 时自动生成实例并调用此 builder 的协议方法。
    /// 用户**不应**直接 new 或调用本类型。
    ///
    /// 协议方法：<see cref="Create"/> / <see cref="Start{TStateMachine}"/> /
    /// <see cref="SetStateMachine"/> / <see cref="SetResult"/> / <see cref="SetException"/> /
    /// <see cref="AwaitOnCompleted{TAwaiter, TStateMachine}"/> / <see cref="AwaitUnsafeOnCompleted{TAwaiter, TStateMachine}"/>。
    /// </summary>
    public struct AsyncTGTaskMethodBuilder
    {
        private TGTaskBody _body;

        public static AsyncTGTaskMethodBuilder Create()
            => new AsyncTGTaskMethodBuilder { _body = TGTaskPool.Rent() };

        public TGTask Task => new TGTask(_body, _body?.Version ?? 0, TGTaskType.Builder);

        public void SetResult() => _body?.SetResult();

        public void SetException(Exception ex) => _body?.SetException(ex);

        public void Start<TStateMachine>(ref TStateMachine sm) where TStateMachine : IAsyncStateMachine
        {
            sm.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine sm)
        {
            // V0.6 Iter 1：body 是 class，无须 box state machine；保留协议方法签名。
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(sm.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.UnsafeOnCompleted(sm.MoveNext);
        }
    }

    /// <summary>
    /// 带返回值的版本。
    /// </summary>
    public struct AsyncTGTaskMethodBuilder<T>
    {
        private TGTaskBody<T> _body;

        public static AsyncTGTaskMethodBuilder<T> Create()
            => new AsyncTGTaskMethodBuilder<T> { _body = TGTaskPool.Rent<T>() };

        public TGTask<T> Task => new TGTask<T>(_body, _body?.Version ?? 0, TGTaskType.Builder);

        public void SetResult(T value) => _body?.SetResult(value);

        public void SetException(Exception ex) => _body?.SetException(ex);

        public void Start<TStateMachine>(ref TStateMachine sm) where TStateMachine : IAsyncStateMachine
        {
            sm.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine sm)
        {
            // V0.6 Iter 1：body 是 class，无须 box state machine；保留协议方法签名。
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(sm.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.UnsafeOnCompleted(sm.MoveNext);
        }
    }
}
