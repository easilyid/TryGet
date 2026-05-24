using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 把已有的 <see cref="ISaveModule"/>（弱类型 4 件套 GetString/GetInt/GetFloat/GetBool）
    /// 桥接为 <see cref="IKVStore"/> 强类型 Get&lt;T&gt;/Set&lt;T&gt;。
    ///
    /// 类型映射：
    /// - <c>T = string</c> → <c>ISaveModule.GetString</c> / <c>SetString</c>
    /// - <c>T = int</c> → GetInt / SetInt
    /// - <c>T = float</c> → GetFloat / SetFloat
    /// - <c>T = bool</c> → GetBool / SetBool
    /// - 其他 T → 抛 <see cref="NotSupportedException"/>（业务应迁到原生 <see cref="IKVStore"/> 实现）
    ///
    /// 迁移建议：V0.9 起删除 ISaveModule + 本 Adapter，业务直接 <c>host.Register&lt;IKVStore&gt;(new MemoryKVStore())</c>。
    ///
    /// <see cref="Keys"/> / <see cref="Count"/>：ISaveModule 无 enumerate API，本 Adapter 维护一个"已 Set 过的 key set"
    /// 提供近似视图（删除 / Clear 时同步）。
    /// </summary>
#pragma warning disable CS0618 // 合法 bridge 用法 — Adapter 必须引用 Obsolete 的 ISaveModule
    public sealed class SaveModuleAdapter : IKVStore
    {
        private readonly ISaveModule _inner;
        private readonly HashSet<string> _knownKeys = new HashSet<string>();

        public int Priority => _inner.Priority;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public SaveModuleAdapter(ISaveModule inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void OnInit(IModuleHost host) { }
        public void Shutdown() { _knownKeys.Clear(); }

        public IEnumerable<string> Keys => _knownKeys;
        public int Count => _knownKeys.Count;

        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _inner.HasKey(key);
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (string.IsNullOrEmpty(key) || !_inner.HasKey(key))
            {
                value = default;
                return false;
            }
            try
            {
                value = Get<T>(key);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        public T Get<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            if (!_inner.HasKey(key))
                throw new KeyNotFoundException($"Key '{key}' not found in SaveModuleAdapter.");

            object result = TypeDispatchGet(typeof(T), key);
            return (T)result;
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            TypeDispatchSet(typeof(T), key, value);
            _knownKeys.Add(key);
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            _knownKeys.Remove(key);
            return _inner.DeleteKey(key);
        }

        public void Clear()
        {
            _knownKeys.Clear();
            _inner.DeleteAll();
        }

        private object TypeDispatchGet(Type t, string key)
        {
            if (t == typeof(string)) return _inner.GetString(key);
            if (t == typeof(int)) return _inner.GetInt(key);
            if (t == typeof(float)) return _inner.GetFloat(key);
            if (t == typeof(bool)) return _inner.GetBool(key);
            throw new NotSupportedException(
                $"SaveModuleAdapter only supports string/int/float/bool. Type {t.Name} requires direct IKVStore implementation.");
        }

        private void TypeDispatchSet(Type t, string key, object value)
        {
            if (t == typeof(string)) { _inner.SetString(key, (string)value); return; }
            if (t == typeof(int)) { _inner.SetInt(key, (int)value); return; }
            if (t == typeof(float)) { _inner.SetFloat(key, (float)value); return; }
            if (t == typeof(bool)) { _inner.SetBool(key, (bool)value); return; }
            throw new NotSupportedException(
                $"SaveModuleAdapter only supports string/int/float/bool. Type {t.Name} requires direct IKVStore implementation.");
        }
    }
#pragma warning restore CS0618
}
