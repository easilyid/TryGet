using System;

namespace TryGet
{
    /// <summary>
    /// 存档服务契约（V0.4 Common Module）。
    ///
    /// 设计原则：Core 层只定义 KV 读写接口（string / int / float / bool），**不指定后端**
    /// （PlayerPrefs / 本地文件 / 云存档 / Headless 内存）。
    ///
    /// Memory 实现（<see cref="MemorySaveModule"/>）走 Dictionary，<see cref="Save"/> 为 no-op，
    /// 用于单元测试、Headless 服务端、Procedure 流程逻辑测试（不依赖 Unity）。
    ///
    /// Production Unity 由 Adapters/Unity 层的 <c>PlayerPrefsSaveModule</c> 或
    /// <c>FileBasedSaveModule</c> 替换（V0.4 后续迭代）。
    ///
    /// 类型契约：与 PlayerPrefs 对齐——string / int / float 三类，外加 bool（业务高频用，
    /// 避免到处写 GetInt("flag") != 0）。不内置序列化复杂对象，业务自行 JSON / 二进制后
    /// 用 SetString 存。
    /// </summary>
    [System.Obsolete("Use IKVStore (V0.8+). ISaveModule will be removed in V0.9. " +
        "Migration: host.Register<IKVStore>(new MemoryKVStore()) — strongly typed Get<T>/Set<T>. " +
        "For legacy bridging: host.Register<IKVStore>(new SaveModuleAdapter(existingSaveModule)).")]
    public interface ISaveModule : IModule
    {
        /// <summary>
        /// 是否存在该 key（任意类型）。
        /// </summary>
        bool HasKey(string key);

        string GetString(string key, string defaultValue = "");
        int GetInt(string key, int defaultValue = 0);
        float GetFloat(string key, float defaultValue = 0f);
        bool GetBool(string key, bool defaultValue = false);

        void SetString(string key, string value);
        void SetInt(string key, int value);
        void SetFloat(string key, float value);
        void SetBool(string key, bool value);

        /// <summary>
        /// 删除单个 key（任意类型）。返回是否真的删了。
        /// </summary>
        bool DeleteKey(string key);

        /// <summary>
        /// 删除全部存档。
        /// </summary>
        void DeleteAll();

        /// <summary>
        /// 持久化到后端。Memory 实现是 no-op；Adapter 实现可能写 PlayerPrefs / 文件 / 云。
        /// 高频调用代价由 Adapter 自己平衡（如批写 / 异步落盘）。
        /// </summary>
        void Save();

        /// <summary>
        /// 已存储的 key 数量（所有类型合计，诊断用）。
        /// </summary>
        int KeyCount { get; }
    }
}
