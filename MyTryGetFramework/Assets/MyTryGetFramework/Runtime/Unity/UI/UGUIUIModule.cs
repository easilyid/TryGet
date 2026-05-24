using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TryGet.Unity
{
    /// <summary>
    /// IUIModule 的 Unity Adapter，基于 UGUI Canvas。
    ///
    /// V0.5 最小版设计权衡：
    /// - **不分层 / 不 Modal / 不传参**：保持 Adapter 薄，复杂语义留给业务子类化扩展。
    /// - **不耦合 IResourceModule**：业务通过 <see cref="RegisterPrefab"/> 注入 UI 预制体引用
    ///   （Production 业务可先用 ResourceModule 加载 prefab 后注入 Adapter）。
    /// - **单 Canvas root**：Initialize 时创建 "[UGUIRoot]" GameObject 含 Canvas + GraphicRaycaster +
    ///   EventSystem，所有 UI 挂在此 root 下。
    /// - **打开顺序 = 渲染顺序**：UGUI 默认按 Hierarchy 顺序绘制（后绘制在上层），
    ///   插入 Hierarchy 最后位置即栈顶。
    /// - **复用 MemoryUIModule 状态机**：用一个 MemoryUIModule 实例做内部状态机（OpenedUIs/Order），
    ///   GameObject lifecycle 由 Adapter 包裹。
    ///
    /// Production 业务可继承本类，重写 OnOpened/OnClosed 钩子加入分层 / Modal / 传参逻辑。
    /// </summary>
    public class UGUIUIModule : IUIModule
    {
        private readonly MemoryUIModule _state = new MemoryUIModule();
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _instances = new Dictionary<string, GameObject>();

        private GameObject _root;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        public int Priority => -300;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public IReadOnlyList<string> OpenedUIs => _state.OpenedUIs;
        public int OpenedCount => _state.OpenedCount;

        /// <summary>
        /// Canvas root（业务可获取并设置渲染模式 / 排序 / RenderCamera 等）。
        /// 在 OnInit 之后非 null。
        /// </summary>
        public Canvas Canvas => _canvas;

        public void OnInit(IModuleHost host)
        {
            _state.OnInit(host);

            _root = new GameObject("[UGUIRoot]");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _root.AddComponent<CanvasScaler>();
            _root.AddComponent<GraphicRaycaster>();

            _canvasRect = _root.GetComponent<RectTransform>();
        }

        public void Shutdown()
        {
            // 销毁所有 UI 实例
            foreach (var go in _instances.Values)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _instances.Clear();
            _prefabs.Clear();
            _state.Shutdown();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _canvas = null;
                _canvasRect = null;
            }
        }

        /// <summary>
        /// 注册 UI prefab。重复注册同 uiName 覆盖。
        /// </summary>
        public void RegisterPrefab(string uiName, GameObject prefab)
        {
            if (string.IsNullOrEmpty(uiName))
                throw new ArgumentException("UI name must be non-empty.", nameof(uiName));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            _prefabs[uiName] = prefab;
        }

        /// <summary>
        /// 取消注册 prefab。若该 UI 正打开，先 Close。
        /// </summary>
        public bool UnregisterPrefab(string uiName)
        {
            if (string.IsNullOrEmpty(uiName)) return false;
            if (_state.IsOpen(uiName)) Close(uiName);
            return _prefabs.Remove(uiName);
        }

        public void Open(string uiName)
        {
            if (string.IsNullOrEmpty(uiName))
                throw new ArgumentException("UI name must be non-empty.", nameof(uiName));
            if (!_prefabs.TryGetValue(uiName, out var prefab))
                throw new InvalidOperationException(
                    $"UI prefab '{uiName}' not registered. Call RegisterPrefab first.");

            // 委托内部 state 机检查重复 + 异常
            _state.Open(uiName);

            var instance = UnityEngine.Object.Instantiate(prefab, _canvasRect, worldPositionStays: false);
            instance.name = uiName;
            _instances[uiName] = instance;

            OnOpened(uiName, instance);
        }

        public void Close(string uiName)
        {
            if (string.IsNullOrEmpty(uiName)) return;
            if (!_state.IsOpen(uiName)) return;

            if (_instances.TryGetValue(uiName, out var go))
            {
                OnClosed(uiName, go);
                if (go != null) UnityEngine.Object.Destroy(go);
                _instances.Remove(uiName);
            }
            _state.Close(uiName);
        }

        public bool IsOpen(string uiName) => _state.IsOpen(uiName);

        public void CloseAll()
        {
            // 反向迭代避免修改 _instances 时索引错位
            var snapshot = new List<string>(_state.OpenedUIs);
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                Close(snapshot[i]);
            }
        }

        /// <summary>
        /// UI Open 后钩子（业务子类化重写以做分层 / 传参 / 动画）。默认空。
        /// </summary>
        protected virtual void OnOpened(string uiName, GameObject instance) { }

        /// <summary>
        /// UI Close 前钩子（业务可重写做关闭动画 / 持久化等，注意此时 GameObject 还未 Destroy）。
        /// </summary>
        protected virtual void OnClosed(string uiName, GameObject instance) { }
    }
}
