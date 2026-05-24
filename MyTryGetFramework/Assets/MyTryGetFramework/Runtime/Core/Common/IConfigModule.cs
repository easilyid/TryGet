using System;

namespace TryGet
{
    /// <summary>
    /// 配置服务契约（V0.5 Common Module，V0.3 deferred 补齐）。
    ///
    /// 设计原则：与 <see cref="IResourceModule"/> 区分——Resource 加载"运行时对象"
    /// （Prefab / AudioClip / Texture），Config 查询"业务表数据"（武器表 / 关卡表 / 怪物属性）。
    /// 实际数据来源（Luban 生成的 ScriptableObject / JSON / Excel）由 Adapter 决定，
    /// Core 只定义统一 KV 查询接口。
    ///
    /// Memory 实现（<see cref="MemoryConfigModule"/>）走 Dictionary&lt;string, object&gt;，
    /// 用于测试、Headless 服务端、Adapter 开发期 mock。
    ///
    /// 业务用法：
    /// <code>
    /// // 启动时 Adapter 批量注册
    /// config.Register("weapon.sword", new WeaponConfig { Atk = 100 });
    /// // 业务运行时查询
    /// var sword = config.Get&lt;WeaponConfig&gt;("weapon.sword");
    /// </code>
    /// </summary>
    [System.Obsolete("Use IConfigSource + ConfigLoader<T> (V0.8+). IConfigModule will be removed in V0.9. " +
        "Migration: split raw byte[] storage (IConfigSource) from typed deserialization (ConfigLoader<T>) — " +
        "aligns with Luban / Fantasy actual config workflow.")]
    public interface IConfigModule : IModule
    {
        /// <summary>
        /// 注册一份配置数据。重复注册同 key 抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        void Register<T>(string key, T config) where T : class;

        /// <summary>
        /// 获取配置。未注册抛 <see cref="ConfigNotFoundException"/>；类型不匹配抛
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        T Get<T>(string key) where T : class;

        /// <summary>
        /// 尝试获取配置。未注册或类型不匹配返回 false。
        /// </summary>
        bool TryGet<T>(string key, out T config) where T : class;

        /// <summary>
        /// 是否已注册该 key。
        /// </summary>
        bool Has(string key);

        /// <summary>
        /// 移除注册。返回是否真的移除了。
        /// </summary>
        bool Unregister(string key);

        /// <summary>
        /// 已注册的配置数量。
        /// </summary>
        int RegisteredCount { get; }
    }

    /// <summary>
    /// 配置未找到异常。
    /// </summary>
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
