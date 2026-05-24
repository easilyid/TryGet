using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 类型化配置加载器（V0.8 起）。包装 <see cref="IConfigSource"/> + 业务定义的反序列化函数，
    /// 提供 <c>Get(id) : T</c> 类型化访问 + 缓存（重复 Get 不重复反序列化）。
    ///
    /// 业务模式：
    /// <code>
    /// // 启动时（业务 / Adapter 注册）
    /// var loader = new ConfigLoader&lt;WeaponConfig&gt;(source, bytes => MyDeserializer.Deserialize&lt;WeaponConfig&gt;(bytes));
    /// // 运行时（业务查询）
    /// var sword = loader.Get("weapon.sword");
    /// </code>
    ///
    /// 热重载：业务可调 <see cref="InvalidateCache"/> 强制下次 Get 重新反序列化（默认 V0.8 不主动监听数据源变化）。
    ///
    /// 缓存语义：Get(id) 第一次反序列化并缓存；同 id 二次 Get 返回缓存 reference（不再调 deserializer）。
    /// 业务若需"每次新实例"应调 <see cref="InvalidateCache"/> 后再 Get。
    /// </summary>
    public sealed class ConfigLoader<T>
    {
        private readonly IConfigSource _source;
        private readonly Func<byte[], T> _deserializer;
        private readonly Dictionary<string, T> _cache = new Dictionary<string, T>();

        public ConfigLoader(IConfigSource source, Func<byte[], T> deserializer)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <summary>查询此 id 是否存在（不一定已缓存）。</summary>
        public bool Has(string configId) => _source.Has(configId);

        /// <summary>取配置。未找到抛 <see cref="ConfigNotFoundException"/>。</summary>
        public T Get(string configId)
        {
            if (string.IsNullOrEmpty(configId))
                throw new ArgumentException("configId must be non-empty.", nameof(configId));

            if (_cache.TryGetValue(configId, out var cached))
                return cached;

            if (!_source.TryGetRaw(configId, out var raw))
                throw new ConfigNotFoundException(configId);

            T value = _deserializer(raw);
            _cache[configId] = value;
            return value;
        }

        /// <summary>尝试取配置。未找到 / 反序列化抛异常时返 false。</summary>
        public bool TryGet(string configId, out T value)
        {
            try
            {
                value = Get(configId);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>清空缓存。下次 Get 会重新反序列化。</summary>
        public void InvalidateCache() => _cache.Clear();

        /// <summary>清空单个 id 的缓存。</summary>
        public void InvalidateCache(string configId)
        {
            if (!string.IsNullOrEmpty(configId)) _cache.Remove(configId);
        }

        /// <summary>当前缓存的 id 数量（诊断用）。</summary>
        public int CachedCount => _cache.Count;
    }
}
