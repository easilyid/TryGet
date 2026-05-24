using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IUIModule 的内存实现（跨端，零外部依赖）。
    ///
    /// 用途：单元测试、UI 流程逻辑测试、Headless 服务端、Adapter 开发期 mock。
    /// Production Unity 由 Adapters/UGUI 层的 <c>UGUIUIModule</c> 替换。
    ///
    /// 限制：不渲染、不分层、不传参、不支持 Modal 屏蔽。只做状态机。
    /// </summary>
    public sealed class MemoryUIModule : IUIModule
    {
        // 双数据结构：List 保证顺序（OpenedUIs / 用户可见），HashSet 保证 O(1) IsOpen
        private readonly List<string> _ordered = new List<string>();
        private readonly HashSet<string> _index = new HashSet<string>();

        // 介于 Resource (-400) 与业务 Module (0) 之间：
        // UI 通常依赖 Resource 加载 Prefab，所以 UI 必须在 Resource 之后初始化。
        public int Priority => -300;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public IReadOnlyList<string> OpenedUIs => _ordered;
        public int OpenedCount => _ordered.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _ordered.Clear();
            _index.Clear();
        }

        public void Open(string uiName)
        {
            if (string.IsNullOrEmpty(uiName))
                throw new ArgumentException("UI name must be non-empty.", nameof(uiName));
            if (_index.Contains(uiName))
                throw new InvalidOperationException(
                    $"UI '{uiName}' is already open. Close it first, or call IsOpen to check.");

            _ordered.Add(uiName);
            _index.Add(uiName);
        }

        public void Close(string uiName)
        {
            if (string.IsNullOrEmpty(uiName))
                return;
            if (_index.Remove(uiName))
            {
                // List.Remove 是 O(n)，但 UI 数量通常 < 30，可接受
                _ordered.Remove(uiName);
            }
        }

        public bool IsOpen(string uiName)
        {
            if (string.IsNullOrEmpty(uiName))
                return false;
            return _index.Contains(uiName);
        }

        public void CloseAll()
        {
            _ordered.Clear();
            _index.Clear();
        }
    }
}
