using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IConfigModule 的内存实现（跨端，零外部依赖、production-ready）。
    ///
    /// 用途：测试、Headless 服务端、Luban Adapter 后端、运行期配置注入。
    /// </summary>
    public sealed class MemoryConfigModule : IConfigModule
    {
        private readonly Dictionary<string, object> _configs = new Dictionary<string, object>();

        // Priority=-460：在 Pool/Timer (-500) 之后，Save (-450) 之前。
        // 配置数据通常最早加载（其他 Module 的 OnInit 可能要读配置初始化业务参数）。
        public int Priority => -460;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int RegisteredCount => _configs.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _configs.Clear();
        }

        public void Register<T>(string key, T config) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (_configs.ContainsKey(key))
                throw new InvalidOperationException($"Config '{key}' already registered.");

            _configs[key] = config;
        }

        public T Get<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must be non-empty.", nameof(key));

            if (_configs.TryGetValue(key, out var raw))
            {
                if (raw is T typed)
                    return typed;
                throw new InvalidOperationException(
                    $"Config at '{key}' is type {raw.GetType().Name}, requested {typeof(T).Name}.");
            }
            throw new ConfigNotFoundException(key);
        }

        public bool TryGet<T>(string key, out T config) where T : class
        {
            config = null;
            if (string.IsNullOrEmpty(key))
                return false;

            if (_configs.TryGetValue(key, out var raw) && raw is T typed)
            {
                config = typed;
                return true;
            }
            return false;
        }

        public bool Has(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return _configs.ContainsKey(key);
        }

        public bool Unregister(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return _configs.Remove(key);
        }
    }
}
