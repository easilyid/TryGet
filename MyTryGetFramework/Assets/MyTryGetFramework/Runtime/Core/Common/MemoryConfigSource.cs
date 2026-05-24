using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// <see cref="IConfigSource"/> 的内存实现（V0.8 起，替代 <see cref="MemoryConfigModule"/>）。
    ///
    /// 持 <c>Dictionary&lt;string, byte[]&gt;</c>，通过 <see cref="SetRaw"/> 显式注入数据。
    /// 仅测试 / Headless / Adapter 开发期 mock 用。
    /// Production Adapter（V1.1+）：<c>LubanConfigSource</c> 从 Luban 生成的 .bytes 加载。
    /// </summary>
    public sealed class MemoryConfigSource : IConfigSource
    {
        private readonly Dictionary<string, byte[]> _data = new Dictionary<string, byte[]>();

        public int Priority => -460; // 与原 MemoryConfigModule.Priority 一致
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int Count => _data.Count;
        public IEnumerable<string> ConfigIds => _data.Keys;

        public void OnInit(IModuleHost host) { }
        public void Shutdown() { _data.Clear(); }

        public bool Has(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;
            return _data.ContainsKey(configId);
        }

        public byte[] GetRaw(string configId)
        {
            if (string.IsNullOrEmpty(configId))
                throw new ArgumentException("configId must be non-empty.", nameof(configId));
            if (!_data.TryGetValue(configId, out var data))
                throw new ConfigNotFoundException(configId);
            return data;
        }

        public bool TryGetRaw(string configId, out byte[] data)
        {
            if (string.IsNullOrEmpty(configId))
            {
                data = null;
                return false;
            }
            return _data.TryGetValue(configId, out data);
        }

        /// <summary>测试 / Adapter 用：注入 configId 对应的 byte[] 数据。</summary>
        public void SetRaw(string configId, byte[] data)
        {
            if (string.IsNullOrEmpty(configId))
                throw new ArgumentException("configId must be non-empty.", nameof(configId));
            _data[configId] = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>测试用：移除单个 id。</summary>
        public bool Remove(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;
            return _data.Remove(configId);
        }
    }
}
