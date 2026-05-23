using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 为每个 Aspect/Tag 类型分配一个稳定的 int index（0..<see cref="BitArray256.Capacity"/>-1）。
    ///
    /// 设计要点：
    /// - 第一次访问 <see cref="TypeIndex{T}.Index"/> 时通过 <see cref="TypeRegistry"/> 分配 index 并缓存到 static field
    /// - 同一类型 T 多次访问返回相同 index，零字典查找开销
    /// - 跨 AppDomain 不保证 index 一致；序列化场景不应依赖 index 数值
    ///
    /// 超过 <see cref="BitArray256.Capacity"/> 类型时 <see cref="TypeRegistry.GetOrAllocate"/> 抛
    /// <see cref="TypeIndexOverflowException"/>。
    /// </summary>
    /// <typeparam name="T">Aspect 或 Tag 类型。</typeparam>
    public static class TypeIndex<T>
    {
        /// <summary>
        /// 类型 T 的稳定 index。Lazy 初始化：static field initializer 在首次访问时运行。
        /// </summary>
        public static readonly int Index = TypeRegistry.GetOrAllocate(typeof(T));
    }

    /// <summary>
    /// 类型 → 稳定 int index 的注册中心。所有公开 API 均通过 lock 串行化，跨线程安全。
    /// </summary>
    public static class TypeRegistry
    {
        private static readonly Dictionary<Type, int> _indices = new Dictionary<Type, int>(64);
        private static readonly object _lock = new object();
        private static int _next = 0;

        /// <summary>
        /// 已分配的 index 总数（用于诊断）。
        /// </summary>
        public static int AllocatedCount
        {
            get { lock (_lock) return _next; }
        }

        /// <summary>
        /// 为类型 <paramref name="type"/> 分配或返回已分配的 index。
        /// 超过 <see cref="BitArray256.Capacity"/> 抛 <see cref="TypeIndexOverflowException"/>。
        /// 线程安全（lock 串行化）。
        /// </summary>
        public static int GetOrAllocate(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            lock (_lock)
            {
                if (_indices.TryGetValue(type, out int idx))
                    return idx;

                if (_next >= BitArray256.Capacity)
                    throw new TypeIndexOverflowException(type, _next);

                idx = _next++;
                _indices[type] = idx;
                return idx;
            }
        }

        /// <summary>
        /// 尝试查询类型已分配的 index（不会触发新分配）。
        /// </summary>
        public static bool TryGet(Type type, out int index)
        {
            lock (_lock)
            {
                return _indices.TryGetValue(type, out index);
            }
        }

        /// <summary>
        /// 仅供测试：清空注册中心。生产代码禁止调用。
        /// </summary>
        internal static void ResetForTests()
        {
            lock (_lock)
            {
                _indices.Clear();
                _next = 0;
            }
        }
    }

    /// <summary>
    /// 类型分配的 index 超过 <see cref="BitArray256.Capacity"/> 时抛出。
    /// </summary>
    public sealed class TypeIndexOverflowException : InvalidOperationException
    {
        public Type OverflowingType { get; }
        public int CurrentCount { get; }

        public TypeIndexOverflowException(Type type, int currentCount)
            : base($"TypeRegistry capacity exceeded: cannot allocate index for {type.Name} (already allocated {currentCount}, limit {BitArray256.Capacity}).")
        {
            OverflowingType = type;
            CurrentCount = currentCount;
        }
    }
}
