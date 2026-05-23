using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// ILocalizationModule 的内存实现（跨端，零外部依赖）。
    ///
    /// 与 MemoryUIModule/MemorySaveModule 不同，这是**production-ready** 的实现而不是 stub：
    /// 本地化逻辑本身就是纯 KV 查询，Memory 实现就是最终形态。
    /// Adapter 层通常只提供"从 Excel/CSV 加载 + RegisterTable"的工厂，运行时仍用此类。
    /// </summary>
    public sealed class MemoryLocalizationModule : ILocalizationModule
    {
        // 语言代码 → (key → 译文) 的双层表
        private readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>();
        // 保 RegisterTable 顺序（OpenedUIs 同理）
        private readonly List<string> _languages = new List<string>();
        private string _currentLanguage;
        private Dictionary<string, string> _currentTable;

        // 介于 Save (-450) 与 Resource (-400) 之间：
        // Localization 可能依赖 Save 读取用户语言偏好（Save 在前），
        // 业务 / UI 后续依赖 Localization 翻译界面文案（在 UI -300 / 业务 0 之前）。
        public int Priority => -420;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string CurrentLanguage => _currentLanguage;
        public IReadOnlyList<string> AvailableLanguages => _languages;
        public int RegisteredCount => _currentTable?.Count ?? 0;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _tables.Clear();
            _languages.Clear();
            _currentLanguage = null;
            _currentTable = null;
        }

        public void RegisterTable(string language, IReadOnlyDictionary<string, string> table)
        {
            if (string.IsNullOrEmpty(language))
                throw new ArgumentException("Language must be non-empty.", nameof(language));
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            // 浅拷贝防外部污染（外部修改原 table 不影响内部状态）
            var copy = new Dictionary<string, string>(table.Count);
            foreach (var kv in table)
            {
                if (string.IsNullOrEmpty(kv.Key))
                    throw new ArgumentException(
                        $"Translation table for '{language}' contains null/empty key.", nameof(table));
                if (kv.Value == null)
                    throw new ArgumentException(
                        $"Translation table for '{language}' has null value for key '{kv.Key}'.", nameof(table));
                copy[kv.Key] = kv.Value;
            }

            // 重复注册：覆盖旧表，AvailableLanguages 保留首次顺序
            if (!_tables.ContainsKey(language))
                _languages.Add(language);
            _tables[language] = copy;

            // 若覆盖的是 current，刷新 _currentTable 引用
            if (_currentLanguage == language)
                _currentTable = copy;
        }

        public void SetLanguage(string language)
        {
            if (string.IsNullOrEmpty(language))
                throw new ArgumentException("Language must be non-empty.", nameof(language));
            if (!_tables.TryGetValue(language, out var table))
                throw new InvalidOperationException(
                    $"Language '{language}' is not registered. Call RegisterTable first.");

            _currentLanguage = language;
            _currentTable = table;
        }

        public string T(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;
            if (_currentTable != null && _currentTable.TryGetValue(key, out var value))
                return value;
            // 漏译 fallback：返回 key 本身（让漏译在 UI 上立即可见）
            return key;
        }

        public string T(string key, string defaultValue)
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;
            if (_currentTable != null && _currentTable.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        public bool TryGet(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key))
                return false;
            if (_currentTable != null && _currentTable.TryGetValue(key, out value))
                return true;
            value = null;
            return false;
        }
    }
}
