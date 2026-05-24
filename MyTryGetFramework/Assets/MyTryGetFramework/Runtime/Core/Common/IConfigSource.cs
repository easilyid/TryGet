using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 配置数据源契约（V0.8 起，替代 <see cref="IConfigModule"/>）。
    ///
    /// 与 V0.5 <see cref="IConfigModule"/> 对比 —— 把"类型化 KV 查询"拆成两层：
    /// - 本接口 <see cref="IConfigSource"/>：仅暴露 byte[] 数据访问（"哪个 id 对应什么二进制"）
    /// - <see cref="ConfigLoader{T}"/>：业务层包装 + 缓存反序列化结果（"id 转 T 类型"）
    ///
    /// 这一拆分对齐 Luban 真实工作流：
    /// 1. Luban 生成二进制数据（embed 在 .bytes 文件）+ C# 类（如 Tables.WeaponConfigMgr）
    /// 2. 运行时业务用 <c>new Tables(buf)</c> 反序列化 — 反序列化逻辑由业务 / Luban 生成
    /// 3. IConfigSource 只需提供"id → byte[]"映射，反序列化函数由业务通过 ConfigLoader 注入
    ///
    /// Production Adapter（V1.1+）：<c>LubanConfigSource</c> 把 Luban 输出加载为 Dictionary&lt;string, byte[]&gt;。
    /// Core 只提供 <see cref="MemoryConfigSource"/> 测试 mock。
    /// </summary>
    public interface IConfigSource : IModule
    {
        /// <summary>是否存在该 configId 对应的数据。</summary>
        bool Has(string configId);

        /// <summary>取原始 byte[]。不存在抛 <see cref="ConfigNotFoundException"/>。</summary>
        byte[] GetRaw(string configId);

        /// <summary>尝试取原始 byte[]。不存在返 false。</summary>
        bool TryGetRaw(string configId, out byte[] data);

        /// <summary>已存储的 configId 集合。</summary>
        IEnumerable<string> ConfigIds { get; }

        /// <summary>已存储数量。</summary>
        int Count { get; }
    }
}
