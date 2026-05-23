using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 本地化服务契约（V0.4 Common Module）。
    ///
    /// 设计原则：Core 层只提供 KV 翻译查询 + 语言切换，**不指定翻译表来源**
    /// （Excel / CSV / JSON / Google Sheets / 在线 CMS 都由 Adapter 解析后 RegisterTable）。
    ///
    /// 与 Unity Localization Package / i18next 共识：
    /// - 翻译表按语言隔离（每个语言独立 KV 表）
    /// - <see cref="T(string)"/> 找不到 key 时返回 key 本身（便于在 UI 上立刻发现漏译）
    /// - <see cref="T(string,string)"/> 可显式指定 fallback
    ///
    /// 不在 Core 内置：复数形式、ICU 占位符、富文本、字体切换（这些属 UGUI 渲染层，由 Adapter 扩展）。
    /// </summary>
    public interface ILocalizationModule : IModule
    {
        /// <summary>
        /// 当前激活的语言代码（如 "zh-CN" / "en-US"）。未 SetLanguage 时为 null。
        /// </summary>
        string CurrentLanguage { get; }

        /// <summary>
        /// 切换当前语言。该语言必须已 RegisterTable，否则抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        void SetLanguage(string language);

        /// <summary>
        /// 注册某语言的翻译表（key → 译文）。重复注册同语言会覆盖旧表。
        /// 参数 table 会被浅拷贝（防止外部后续修改污染内部状态）。
        /// </summary>
        void RegisterTable(string language, IReadOnlyDictionary<string, string> table);

        /// <summary>
        /// 翻译。找不到 key（或未 SetLanguage）时返回 key 本身（便于 UI 立即暴露漏译）。
        /// </summary>
        string T(string key);

        /// <summary>
        /// 翻译并指定 fallback。找不到 key（或未 SetLanguage）时返回 defaultValue。
        /// </summary>
        string T(string key, string defaultValue);

        /// <summary>
        /// 尝试取译文。找到返回 true 并写入 value；否则 false 且 value=null。
        /// </summary>
        bool TryGet(string key, out string value);

        /// <summary>
        /// 已注册的语言列表（顺序按 RegisterTable 调用顺序）。
        /// </summary>
        IReadOnlyList<string> AvailableLanguages { get; }

        /// <summary>
        /// 当前语言下的译文数量（诊断用）。未 SetLanguage 返回 0。
        /// </summary>
        int RegisteredCount { get; }
    }
}
