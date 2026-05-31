using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// <see cref="IKVStore"/> 的内存实现。
    ///
    /// 绕过 <see cref="ISerializer"/>，直接持 <c>Dictionary&lt;string, object&gt;</c>（zero-copy reference）。
    /// 仅测试 / Headless 用 — 重启数据丢失。
    /// </summary>
    public sealed class MemoryKVStore : IKVStore
    {
        private readonly Dictionary<string, object> _store = new Dictionary<string, object>();

        public int Priority => -450;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int Count => _store.Count;
        public IEnumerable<string> Keys => _store.Keys;

        public void OnInit(IModuleSystem host) { }
        public void Shutdown() { _store.Clear(); }

        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _store.ContainsKey(key);
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }
            if (_store.TryGetValue(key, out var obj))
            {
                if (obj is T typed)
                {
                    value = typed;
                    return true;
                }
                // null reference: 对 reference T 视为合法返回 (null)
                if (obj == null && !typeof(T).IsValueType)
                {
                    value = default;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public T Get<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            if (!_store.TryGetValue(key, out var obj))
                throw new KeyNotFoundException($"Key '{key}' not found in MemoryKVStore.");
            if (obj is T typed) return typed;
            if (obj == null && !typeof(T).IsValueType) return default;
            throw new InvalidCastException(
                $"Key '{key}' is of type {obj?.GetType().Name ?? "null"}, cannot cast to {typeof(T).Name}.");
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            _store[key] = value;
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _store.Remove(key);
        }

        public void Clear() { _store.Clear(); }
    }
}
