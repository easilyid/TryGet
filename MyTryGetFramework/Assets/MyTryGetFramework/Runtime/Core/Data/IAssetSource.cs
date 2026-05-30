using System;
using System.Collections.Generic;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// 资源加载契约。
    ///
    /// Core 仅定义 IAssetSource 接口；真实 Unity 资源系统由后续 Adapter 实现。
    /// Memory 实现（<see cref="MemoryAssetSource"/>）走 Dictionary&lt;string, object&gt; + <see cref="TGTask{T}.FromResult"/>，
    /// 同步返回 — 用于测试 / Headless / Adapter 开发期 mock。
    ///
    /// 路径不存在 → <see cref="LoadAsync{T}"/> 返回的 TGTask 抛 <see cref="AssetNotFoundException"/>
    /// （由 <c>TGTask&lt;T&gt;.FromException(...)</c> 包装）。
    /// </summary>
    public interface IAssetSource : IModule
    {
        /// <summary>是否已加载（缓存 / 注册）该 path。</summary>
        bool IsLoaded(string path);

        /// <summary>
        /// 异步加载资源。Memory 实现立即完成；YooAsset Adapter 实际异步。
        /// 路径不存在抛 <see cref="AssetNotFoundException"/>。
        /// </summary>
        TGTask<T> LoadAsync<T>(string path) where T : class;

        /// <summary>
        /// 同步取已加载资源。未加载抛 <see cref="AssetNotFoundException"/>。
        /// 同步加载场景（Memory mock）下可直接用。
        /// </summary>
        T Get<T>(string path) where T : class;

        /// <summary>尝试取已加载资源。未加载返 false。</summary>
        bool TryGet<T>(string path, out T asset) where T : class;

        /// <summary>卸载资源。Memory 实现移 dict；Adapter 减引用计数 / unload bundle。</summary>
        void Unload(string path);

        /// <summary>已加载 path 集合（诊断用）。</summary>
        IEnumerable<string> LoadedPaths { get; }

        /// <summary>已加载数量。</summary>
        int LoadedCount { get; }
    }

    /// <summary>资源未找到异常。</summary>
    public sealed class AssetNotFoundException : InvalidOperationException
    {
        public string Path { get; }
        public AssetNotFoundException(string path)
            : base($"Asset not found: '{path}'.")
        {
            Path = path;
        }
    }
}
