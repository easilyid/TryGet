using System;

namespace TryGet
{
    /// <summary>
    /// 资源服务契约（V0.4，design.md §12 V0.4 Gate）。
    ///
    /// 设计原则：Core 层仅定义同步 Load + Release + Register/Unregister，**不依赖 YooAsset / Addressables / UniTask**。
    /// Unity 实际资源加载由 Adapters/YooAsset 等实现（V0.4 后续迭代）。
    ///
    /// Memory 实现（<see cref="MemoryResourceModule"/>）走 Dictionary&lt;string,object&gt;，
    /// 用于测试、程序化资源、Headless 服务端。Production Unity 用 YooAssetResourceModule 替换。
    ///
    /// 异步加载：V0.4 不在 Core 暴露 Task / UniTask（保持跨端干净）。
    /// 若需异步，由 Adapter 实现 IAsyncResourceModule（继承 IResourceModule + 加 Task<T> LoadAsync）。
    /// </summary>
    public interface IResourceModule : IModule
    {
        /// <summary>
        /// 同步加载资源。未注册时抛 <see cref="ResourceNotFoundException"/>。
        /// </summary>
        T Load<T>(string path) where T : class;

        /// <summary>
        /// 尝试加载。未注册时返回 false 而不抛。
        /// </summary>
        bool TryLoad<T>(string path, out T resource) where T : class;

        /// <summary>
        /// 释放资源。Memory 实现是 no-op；Adapter 实现可能减少引用计数 / 卸载 AssetBundle 等。
        /// </summary>
        void Release(object resource);

        /// <summary>
        /// 注册资源到此 Module（测试 / 程序化资源 / Adapter 内部）。
        /// 重复注册同 path 抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        void Register<T>(string path, T resource) where T : class;

        /// <summary>
        /// 移除注册。返回是否真的移除了。
        /// </summary>
        bool Unregister(string path);

        /// <summary>
        /// 已注册资源数量（诊断用）。
        /// </summary>
        int RegisteredCount { get; }
    }

    /// <summary>
    /// 资源未找到异常。
    /// </summary>
    public sealed class ResourceNotFoundException : InvalidOperationException
    {
        public string Path { get; }

        public ResourceNotFoundException(string path)
            : base($"Resource not found: '{path}'.")
        {
            Path = path;
        }
    }
}
