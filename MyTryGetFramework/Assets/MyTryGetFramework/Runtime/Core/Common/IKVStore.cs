using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// Key-Value 存储契约（V0.8 起，替代 <see cref="ISaveModule"/>）。
    ///
    /// 强类型 Get/Set，统一 KV 抽象 — 与 V0.7 弱类型四件套（GetString/GetInt/GetFloat/GetBool）相比：
    /// - 业务可存任意类型（reference type / value type / 自定义 struct）
    /// - 序列化由 Adapter 注入 <see cref="ISerializer"/> 处理（<see cref="MemoryKVStore"/> 绕过）
    /// - 与 ET / Fantasy 服务端 MongoDB 强类型路径一致
    ///
    /// **不支持嵌套事务 / 索引 / 查询**（IKVStore 不是 DB；真需要走 V1.1+ MongoDb / Sqlite Adapter）。
    ///
    /// 迁移路径（V0.8 → V0.9）：
    /// - V0.8：<see cref="IKVStore"/> 与 <see cref="ISaveModule"/> 共存；<see cref="SaveModuleAdapter"/> 桥接
    /// - V0.9：删 <see cref="ISaveModule"/> + <see cref="MemorySaveModule"/> + <see cref="SaveModuleAdapter"/>
    /// </summary>
    public interface IKVStore : IModule
    {
        /// <summary>是否存在该 key（任意类型）。</summary>
        bool ContainsKey(string key);

        /// <summary>软取：key 不存在或类型不匹配时返 false，value=default。</summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>硬取：key 不存在抛 <see cref="KeyNotFoundException"/>；类型不匹配抛 <see cref="InvalidCastException"/>。</summary>
        T Get<T>(string key);

        /// <summary>写入。已存在的 key 直接覆盖（类型变化也允许）。</summary>
        void Set<T>(string key, T value);

        /// <summary>删除 key。不存在返 false。</summary>
        bool Remove(string key);

        /// <summary>清空全部数据。</summary>
        void Clear();

        /// <summary>所有 key 的快照视图。</summary>
        IEnumerable<string> Keys { get; }

        /// <summary>已存储 key 数。</summary>
        int Count { get; }
    }
}
