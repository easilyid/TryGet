using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IResourceModule 的内存实现（跨端，零外部依赖）。
    ///
    /// 用途：单元测试、程序化资源、Headless 服务端、Adapter 开发期 mock。
    /// Production Unity 应注册 <c>YooAssetResourceModule</c>（V0.4 后续迭代）替代此实现。
    ///
    /// 限制：Load 是同步的；不模拟"未加载"、"加载中"等真实异步状态。
    /// </summary>
    public sealed class MemoryResourceModule : IResourceModule
    {
        private readonly Dictionary<string, object> _resources = new Dictionary<string, object>();

        // 在 Pool/Timer (-500) 之后、EntityWorld (-100) 之前：业务 Module 可在 OnInit 中预加载资源
        public int Priority => -400;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int RegisteredCount => _resources.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _resources.Clear();
        }

        public T Load<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path must be non-empty.", nameof(path));

            if (_resources.TryGetValue(path, out var resource))
            {
                if (resource is T typed)
                    return typed;
                throw new InvalidOperationException(
                    $"Resource at '{path}' is type {resource.GetType().Name}, requested {typeof(T).Name}.");
            }
            throw new ResourceNotFoundException(path);
        }

        public bool TryLoad<T>(string path, out T resource) where T : class
        {
            resource = null;
            if (string.IsNullOrEmpty(path))
                return false;

            if (_resources.TryGetValue(path, out var raw) && raw is T typed)
            {
                resource = typed;
                return true;
            }
            return false;
        }

        public void Release(object resource)
        {
            // Memory 实现：no-op。GC 自然回收。
            // Adapter 实现可在此释放 AssetBundle / 减少引用计数等。
        }

        public void Register<T>(string path, T resource) where T : class
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path must be non-empty.", nameof(path));
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            if (_resources.ContainsKey(path))
                throw new InvalidOperationException($"Resource '{path}' already registered.");

            _resources[path] = resource;
        }

        public bool Unregister(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return _resources.Remove(path);
        }
    }
}
