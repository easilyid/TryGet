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
                return _idle.Count > 0 ? _idle.Pop() : _factory();
            }

            public void Return(T item)
            {
                if (item == null) return;
                _onReturn?.Invoke(item);
                _idle.Push(item);
            }
        }
    }
}
