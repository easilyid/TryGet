using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IPoolModule 默认实现。每个 Type 一个池实例，存于内部字典。
    /// </summary>
    public sealed class PoolModule : IPoolModule
    {
        private readonly Dictionary<Type, object> _pools = new Dictionary<Type, object>();

        public int Priority => -500;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public void OnInit(IModuleSystem host) { }

        public void Shutdown()
        {
            _pools.Clear();
        }

        public IObjectPool<T> GetOrCreatePool<T>(Func<T> factory, Action<T> onReturn = null, int initialSize = 0)
            where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (initialSize < 0)
                throw new ArgumentOutOfRangeException(nameof(initialSize));

            if (_pools.TryGetValue(typeof(T), out var existing))
                return (IObjectPool<T>)existing;

            var pool = new ObjectPool<T>(factory, onReturn, initialSize);
            _pools[typeof(T)] = pool;
            return pool;
        }

        public bool DestroyPool<T>() where T : class
        {
            return _pools.Remove(typeof(T));
        }

        private sealed class ObjectPool<T> : IObjectPool<T> where T : class
        {
            private readonly Stack<T> _idle;
            private readonly Func<T> _factory;
            private readonly Action<T> _onReturn;

            // C9：诊断计数器
            private long _totalRented;
            private long _totalReturned;
            private int _peakActive;
            private long _hitCount;
            private long _missCount;

#if UNITY_ASSERTIONS || DEBUG
            // C9：DEBUG 或 UNITY_ASSERTIONS 模式下检测重复 Return。
            // 用引用相等比较器：池对象可能重写 Equals/GetHashCode 为值相等，默认 comparer 会把
            // 不同实例误判为同一对象，导致双释放检测误报/漏报。
            private readonly HashSet<T> _activeSet = new HashSet<T>(ReferenceComparer.Instance);

            /// <summary>
            /// 按引用判等的比较器。netstandard2.1 无内置 ReferenceEqualityComparer，故自定义；
            /// 用 RuntimeHelpers.GetHashCode 取与对象重写无关的标识哈希。
            /// </summary>
            private sealed class ReferenceComparer : IEqualityComparer<T>
            {
                public static readonly ReferenceComparer Instance = new ReferenceComparer();
                public bool Equals(T x, T y) => ReferenceEquals(x, y);
                public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
#endif

            public ObjectPool(Func<T> factory, Action<T> onReturn, int initialSize)
            {
                _factory = factory;
                _onReturn = onReturn;
                _idle = new Stack<T>(initialSize > 0 ? initialSize : 4);

                for (int i = 0; i < initialSize; i++)
                    _idle.Push(_factory());
            }

            public int IdleCount => _idle.Count;

            public T Rent()
            {
                T item;
                if (_idle.Count > 0)
                {
                    item = _idle.Pop();
                    _hitCount++;
                }
                else
                {
                    item = _factory();
                    _missCount++;
                }

                _totalRented++;
                int currentActive = (int)(_totalRented - _totalReturned);
                if (currentActive > _peakActive)
                    _peakActive = currentActive;

#if UNITY_ASSERTIONS || DEBUG
                _activeSet.Add(item);
#endif

                return item;
            }

            public void Return(T item)
            {
                if (item == null) return;

#if UNITY_ASSERTIONS || DEBUG
                // C9：DEBUG 或 UNITY_ASSERTIONS 模式下检测重复 Return
                if (!_activeSet.Remove(item))
                    throw new InvalidOperationException(
                        $"Attempted to return an object of type {typeof(T).Name} that was not rented from this pool, or was already returned. " +
                        "This indicates a double-release bug.");
#endif

                try
                {
                    _onReturn?.Invoke(item);
                }
                catch (Exception)
                {
                    // onReturn is a cleanup hook; isolate failures so ownership accounting cannot leak.
                }
                _idle.Push(item);
                _totalReturned++;
            }

            public PoolDiagnostics GetDiagnostics()
            {
                int currentActive = (int)(_totalRented - _totalReturned);
                return new PoolDiagnostics(
                    _totalRented,
                    _totalReturned,
                    currentActive,
                    _idle.Count,
                    _peakActive,
                    _hitCount,
                    _missCount
                );
            }
        }
    }
}
