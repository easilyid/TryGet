using System;
using System.Collections.Generic;
using UnityEngine;

namespace TryGet.Unity
{
    /// <summary>
    /// ISaveModule 的 Unity Adapter，接 <see cref="PlayerPrefs"/>。
    ///
    /// 设计权衡：
    /// - PlayerPrefs 原生只支持 string / int / float 三类。bool 编码为 int (0=false, 1=true)，
    ///   存储时透明转换，与 <see cref="MemorySaveModule"/> bool API 行为一致。
    /// - PlayerPrefs.HasKey 不区分类型；与 Memory 实现"同 key 跨类型互斥（覆盖）"语义对齐。
    /// - <see cref="Save"/> 真正落盘（PlayerPrefs.Save），调用方需自决调用时机
    ///   （高频调用有 I/O 代价，业务可在场景切换 / 退出时调用）。
    /// - <see cref="Shutdown"/> 不清除 PlayerPrefs 数据（与 Adapter 持久化语义一致：
    ///   Adapter Shutdown 不应擦盘）。仅清空内部 _keys 缓存。
    /// - KeyCount 通过内部 _keys 集合维护（PlayerPrefs 原生不提供 key 枚举），
    ///   Set 时记入、Delete 时移除、DeleteAll 时清空。
    ///
    /// 跨进程一致性：本 Adapter 内部 _keys 缓存只反映**本进程内 Set 过的 key**，
    /// 不会读 PlayerPrefs 已存在的历史 key。若需读取上次启动遗留的存档，业务在 OnInit
    /// 后用 HasKey/Get 直查 PlayerPrefs（_keys 缓存仅供 KeyCount 诊断）。
    /// </summary>
    public sealed class PlayerPrefsSaveModule : ISaveModule
    {
        // 内部 key 集合（用于 KeyCount 诊断；PlayerPrefs 原生不暴露 key 枚举）
        private readonly HashSet<string> _keys = new HashSet<string>();

        public int Priority => -450;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int KeyCount => _keys.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            // 不清 PlayerPrefs（持久化数据不应被 Adapter Shutdown 擦除）
            _keys.Clear();
        }

        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return PlayerPrefs.HasKey(key);
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            if (!PlayerPrefs.HasKey(key)) return defaultValue;
            // bool 编码为 int (0/1)
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        public void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            PlayerPrefs.SetString(key, value);
            _keys.Add(key);
        }

        public void SetInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            PlayerPrefs.SetInt(key, value);
            _keys.Add(key);
        }

        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            PlayerPrefs.SetFloat(key, value);
            _keys.Add(key);
        }

        public void SetBool(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            _keys.Add(key);
        }

        public bool DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!PlayerPrefs.HasKey(key)) return false;
            PlayerPrefs.DeleteKey(key);
            _keys.Remove(key);
            return true;
        }

        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            _keys.Clear();
        }

        public void Save()
        {
            // 真正落盘（PlayerPrefs 默认是退出时 flush，显式 Save 立即同步到磁盘）
            PlayerPrefs.Save();
        }
    }
}
