using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// ISaveModule 的内存实现（跨端，零外部依赖）。
    ///
    /// 用途：单元测试、Headless 服务端、Procedure 流程逻辑测试、Adapter 开发期 mock。
    /// Production Unity 由 PlayerPrefsSaveModule 或 FileBasedSaveModule 替换（V0.4 后续迭代）。
    ///
    /// 行为约定（与 PlayerPrefs 对齐）：
    /// - 同 key 跨类型 Set 会**覆盖**（旧值丢失，类型重置）
    /// - Get 时 key 不存在或类型不匹配 → 返回 defaultValue（容错，不抛）
    /// - <see cref="Save"/> 为 no-op，数据仅存于进程内存，重启即丢
    /// </summary>
    [System.Obsolete("Use MemoryKVStore (V0.8+). MemorySaveModule will be removed in V0.9.")]
    public sealed class MemorySaveModule : ISaveModule
    {
        // 单字典 object 装箱：保证 KeyCount 准确，且同 key 跨类型互斥（覆盖语义）
        private readonly Dictionary<string, object> _store = new Dictionary<string, object>();

        // 在 Pool/Timer (-500) 与 Resource (-400) 之间：存档是基础数据服务，
        // 业务 Module / Procedure 都可能在 OnInit / OnEnter 期间读它。
        public int Priority => -450;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int KeyCount => _store.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _store.Clear();
        }

        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return _store.ContainsKey(key);
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;
            if (_store.TryGetValue(key, out var v) && v is string s)
                return s;
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;
            if (_store.TryGetValue(key, out var v) && v is int i)
                return i;
            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;
            if (_store.TryGetValue(key, out var v) && v is float f)
                return f;
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;
            if (_store.TryGetValue(key, out var v) && v is bool b)
                return b;
            return defaultValue;
        }

        public void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            // value 允许 null？PlayerPrefs 不允许；我们也禁止
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            _store[key] = value;
        }

        public void SetInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            _store[key] = value;
        }

        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            _store[key] = value;
        }

        public void SetBool(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            _store[key] = value;
        }

        public bool DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return _store.Remove(key);
        }

        public void DeleteAll()
        {
            _store.Clear();
        }

        public void Save()
        {
            // Memory 实现：no-op。Adapter 实现可写 PlayerPrefs / 文件 / 云存档。
        }
    }
}
