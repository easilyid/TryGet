using System;

namespace TryGet.Async
{
    /// <summary>
    /// TGTask 的内部状态机体（class 形态，由 <see cref="TGTaskPool"/> 复用）。
    /// 内部访问可见，外部用户不应直接接触此接口。
    ///
    /// V0.6 Iter 1：仅 SetResult/SetException + 单个 continuation，Version 固定为 0。
    /// V0.6 Iter 2：与 <see cref="TGTaskCompletionSource"/> 联动。
    /// V0.6 Iter 3：启用 <see cref="Version"/> 防过期 + <see cref="Reset"/> 重置语义。
    /// V0.6 Iter 4：加入 Pool 复用（Pool 调用 Reset 让 version++）。
    /// </summary>
    internal interface ITGTaskBody
    {
        int Version { get; }
        bool IsCompleted { get; }
        void GetResult();
        void SetResult();
        void SetException(Exception ex);
        void OnCompleted(Action continuation);
        void UnsafeOnCompleted(Action continuation);

        /// <summary>
        /// 重置 body 状态以便复用。version++，清空 continuation 和 exception。
        /// V0.6 Iter 4 由 <see cref="TGTaskPool"/> 在 Return 时调用。
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// 带返回值的版本。
    /// </summary>
    internal interface ITGTaskBody<T>
    {
        int Version { get; }
        bool IsCompleted { get; }
        T GetResult();
        void SetResult(T value);
        void SetException(Exception ex);
        void OnCompleted(Action continuation);
        void UnsafeOnCompleted(Action continuation);
        void Reset();
    }

    /// <summary>
    /// V0.6 Iter 1-3 实现：
    /// - 单 continuation（多次 await 同一 task = 用户错误，OnCompleted 抛 InvalidOperationException）。
    /// - <see cref="Version"/> 真实启用（Iter 3）：<see cref="Reset"/> 时 ++。
    /// - 池化（Iter 4 由 <see cref="TGTaskPool"/> Reset 调用归还）。
    /// </summary>
    internal sealed class TGTaskBody : ITGTaskBody
    {
        /// <summary>
        /// 接近 int.MaxValue 时让 version 回绕，避免 int 溢出引发的 ABA 风险。
        /// 数值参考 hsenl HTask:16 的 MaxVersion = int.MaxValue - 2。
        /// </summary>
        internal const int MaxVersion = int.MaxValue - 2;
        internal const int MinVersion = int.MinValue;

        private int _version;
        private bool _completed;
        private Exception _exception;
        private Action _continuation;

        public int Version => _version;

        public bool IsCompleted => _completed;

        public void GetResult()
        {
            if (_exception != null)
                throw _exception;
        }

        public void SetResult()
        {
            if (_completed)
                return;
            _completed = true;
            var c = _continuation;
            _continuation = null;
            c?.Invoke();
        }

        public void SetException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            if (_completed)
                return;
            _completed = true;
            _exception = ex;
            var c = _continuation;
            _continuation = null;
            c?.Invoke();
        }

        public void OnCompleted(Action continuation)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            if (_completed)
            {
                continuation();
                return;
            }
            if (_continuation != null)
                throw new InvalidOperationException(
                    "TGTask only supports a single continuation. Awaiting the same task twice is not supported.");
            _continuation = continuation;
        }

        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

        public void Reset()
        {
            _completed = false;
            _exception = null;
            _continuation = null;
            // version 回绕：到达 MaxVersion 时回到 MinVersion，确保旧 struct 实例（持任意旧 version）几乎不可能撞上
            _version = _version >= MaxVersion ? MinVersion : _version + 1;
        }
    }

    /// <summary>
    /// 带返回值版本。语义与 <see cref="TGTaskBody"/> 同。
    /// </summary>
    internal sealed class TGTaskBody<T> : ITGTaskBody<T>
    {
        private int _version;
        private bool _completed;
        private Exception _exception;
        private T _result;
        private Action _continuation;

        public int Version => _version;

        public bool IsCompleted => _completed;

        public T GetResult()
        {
            if (_exception != null)
                throw _exception;
            return _result;
        }

        public void SetResult(T value)
        {
            if (_completed)
                return;
            _completed = true;
            _result = value;
            var c = _continuation;
            _continuation = null;
            c?.Invoke();
        }

        public void SetException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            if (_completed)
                return;
            _completed = true;
            _exception = ex;
            var c = _continuation;
            _continuation = null;
            c?.Invoke();
        }

        public void OnCompleted(Action continuation)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            if (_completed)
            {
                continuation();
                return;
            }
            if (_continuation != null)
                throw new InvalidOperationException(
                    "TGTask<T> only supports a single continuation. Awaiting the same task twice is not supported.");
            _continuation = continuation;
        }

        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

        public void Reset()
        {
            _completed = false;
            _exception = null;
            _result = default;
            _continuation = null;
            _version = _version >= TGTaskBody.MaxVersion ? TGTaskBody.MinVersion : _version + 1;
        }
    }
}
