using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 配置数据源契约（V0.8 起）。
    ///
    /// 把"类型化 KV 查询"拆成两层：
    /// - 本接口 <see cref="IConfigSource"/>：仅暴露 byte[] 数据访问
    /// - <see cref="ConfigLoader{T}"/>：业务层包装 + 缓存反序列化结果
    /// </summary>
    public interface IConfigSource : IModule
    {
        bool Has(string configId);
        byte[] GetRaw(string configId);
        bool TryGetRaw(string configId, out byte[] data);
        IEnumerable<string> ConfigIds { get; }
        int Count { get; }
    }

    public sealed class ConfigNotFoundException : InvalidOperationException
    {
        public string Key { get; }

        public ConfigNotFoundException(string key)
            : base($"Config not found: '{key}'.")
        {
            Key = key;
        }
    }
}
