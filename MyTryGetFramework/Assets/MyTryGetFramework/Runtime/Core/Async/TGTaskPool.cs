using System.Collections.Generic;

namespace TryGet.Async
{
    /// <summary>
    /// TGTask 状态机 body 的全局池。对标 UniTask.TaskPool 设计。
    ///
    /// 设计要点：
    /// - 单线程模型（V0.6 不支持多线程；测试通过 ThrowIfNotMainThread 检测留 V0.6 后续 Iter 加）。
    /// - 每个 body 类型（非泛型 <see cref="TGTaskBody"/> + 每个泛型实例 <c>TGTaskBody&lt;T&gt;</c>）一个独立 Stack。
    /// - Pool 满时不再池化（直接 GC），防止内存膨胀。
    /// - Rent / Return internal，仅由 builder / tcs 调用；公开诊断 API <see cref="PooledCount"/> / <see cref="PooledCountOf{T}"/>。
    ///
    /// 用户使用模式：通常不直接接触 TGTaskPool。Builder 创建的 body 在 await 完成时自动归还；
    /// Manual（tcs 创建）body 需用户调 <see cref="TGTaskCompletionSource.Return"/>。
    /// </summary>
    public static class TGTaskPool
    {
        /// <summary>
        /// 每种 body 类型的最大池容量。超过此值的 body 直接 GC（不入池）。
        /// 默认 64 —— 对游戏典型场景（同帧 N 个并发协程）足够，且内存可控。
        /// </summary>
        public static int MaxPoolSize { get; set; } = 64;

        private static readonly Stack<TGTaskBody> _bodies = new Stack<TGTaskBody>();

        internal static TGTaskBody Rent()
        {
            if (_bodies.Count > 0)
                return _bodies.Pop();
            return new TGTaskBody();
        }

        internal static void Return(TGTaskBody body)
        {
            if (body == null) return;
            if (_bodies.Count >= MaxPoolSize) return;
            body.Reset();
            _bodies.Push(body);
        }

        /// <summary>非泛型池中已缓存的 body 数（诊断用）。</summary>
        public static int PooledCount => _bodies.Count;

        // ----- 泛型版本：每个 T 一个独立 Stack -----

        private static class TypedPool<T>
        {
            public static readonly Stack<TGTaskBody<T>> Bodies = new Stack<TGTaskBody<T>>();
        }

        internal static TGTaskBody<T> Rent<T>()
        {
            if (TypedPool<T>.Bodies.Count > 0)
                return TypedPool<T>.Bodies.Pop();
            return new TGTaskBody<T>();
        }

        internal static void Return<T>(TGTaskBody<T> body)
        {
            if (body == null) return;
            if (TypedPool<T>.Bodies.Count >= MaxPoolSize) return;
            body.Reset();
            TypedPool<T>.Bodies.Push(body);
        }

        public static int PooledCountOf<T>() => TypedPool<T>.Bodies.Count;

        /// <summary>清空所有池（测试 / Shutdown 用）。</summary>
        public static void ClearAll()
        {
            _bodies.Clear();
        }

        /// <summary>清空指定泛型池（测试用）。</summary>
        public static void ClearGeneric<T>()
        {
            TypedPool<T>.Bodies.Clear();
        }
    }
}
