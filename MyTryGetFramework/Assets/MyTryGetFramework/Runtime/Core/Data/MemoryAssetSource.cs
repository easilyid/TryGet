using System;
using System.Collections.Generic;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// <see cref="IAssetSource"/> 的内存实现。
    ///
    /// 持 <c>Dictionary&lt;string, object&gt;</c>，<see cref="LoadAsync{T}"/> 立即返回 <see cref="TGTask{T}.FromResult"/>。
    /// 仅测试 / Headless / Adapter 开发期 mock 用。
    /// </summary>
    public sealed class MemoryAssetSource : IAssetSource
    {
        private readonly Dictionary<string, object> _assets = new Dictionary<string, object>();

        public int Priority => -400;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int LoadedCount => _assets.Count;
        public IEnumerable<string> LoadedPaths => _assets.Keys;

        public void OnInit(IModuleHost host) { }
        public void Shutdown() { _assets.Clear(); }

        public bool IsLoaded(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return _assets.ContainsKey(path);
        }

        public TGTask<T> LoadAsync<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
                return TGTask<T>.FromException(new ArgumentException("path must be non-empty.", nameof(path)));

            if (!_assets.TryGetValue(path, out var obj))
                return TGTask<T>.FromException(new AssetNotFoundException(path));

            if (obj is T typed)
                return TGTask<T>.FromResult(typed);

            return TGTask<T>.FromException(new InvalidCastException(
                $"Asset at '{path}' is of type {obj?.GetType().Name ?? "null"}, cannot cast to {typeof(T).Name}."));
        }

        public T Get<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path must be non-empty.", nameof(path));
            if (!_assets.TryGetValue(path, out var obj))
                throw new AssetNotFoundException(path);
            if (obj is T typed) return typed;
            throw new InvalidCastException(
                $"Asset at '{path}' is of type {obj?.GetType().Name ?? "null"}, cannot cast to {typeof(T).Name}.");
        }

        public bool TryGet<T>(string path, out T asset) where T : class
        {
            asset = null;
            if (string.IsNullOrEmpty(path)) return false;
            if (_assets.TryGetValue(path, out var obj) && obj is T typed)
            {
                asset = typed;
                return true;
            }
            return false;
        }

        public void Unload(string path)
        {
            if (!string.IsNullOrEmpty(path)) _assets.Remove(path);
        }

        /// <summary>测试 / Adapter 用：注入 path → 资源 对象。</summary>
        public void Add<T>(string path, T asset) where T : class
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path must be non-empty.", nameof(path));
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            _assets[path] = asset;
        }
    }
}
