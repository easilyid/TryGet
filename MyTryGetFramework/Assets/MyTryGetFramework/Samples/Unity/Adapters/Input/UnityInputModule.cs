using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace TryGet.Unity
{
    /// <summary>
    /// IInputModule 的 Unity Adapter，接 com.unity.inputsystem 1.18.
    ///
    /// 设计权衡：
    /// - **业务用 binding 字符串注册 action**：RegisterButton(name, "&lt;Keyboard&gt;/space") 等，
    ///   Adapter 内部 new InputAction + Enable + hook performed/canceled。
    ///   简化：业务不需要 .inputactions Asset 文件（Asset-driven 留给上层业务自决）。
    /// - **LateUpdate 清边**（与 <see cref="MemoryInputModule"/> 行为契约对齐）：
    ///   InputAction.performed/canceled 写入 _pressedThisFrame/_releasedThisFrame，
    ///   ILateUpdateModule.LateUpdate 清空，业务在 IUpdateModule.Update 阶段可消费 edge。
    /// - **Axis 直接 ReadValue**：1D/2D 轴每次查询走 action.ReadValue&lt;T&gt;()，
    ///   不缓存（与 InputSystem 原生行为一致）。
    /// - **Shutdown 释放**：所有 InputAction.Disable + Dispose。
    ///
    /// 使用：
    /// <code>
    /// var ui = new UnityInputModule();
    /// host.Register&lt;IInputModule&gt;(ui);
    /// host.Initialize();
    /// ui.RegisterButton("Jump", "&lt;Keyboard&gt;/space");
    /// ui.RegisterAxis2D("Move", "&lt;Gamepad&gt;/leftStick");
    /// // 业务 Update 内
    /// if (host.Get&lt;IInputModule&gt;().WasPressedThisFrame("Jump")) ...
    /// </code>
    /// </summary>
    public sealed class UnityInputModule : IInputModule, ILateUpdateModule
    {
        private readonly Dictionary<string, InputAction> _buttons = new Dictionary<string, InputAction>();
        private readonly Dictionary<string, InputAction> _axes1D = new Dictionary<string, InputAction>();
        private readonly Dictionary<string, InputAction> _axes2D = new Dictionary<string, InputAction>();

        // edge state buffers（performed/canceled 回调写入，LateUpdate 清空）
        private readonly HashSet<string> _pressed = new HashSet<string>();
        private readonly HashSet<string> _pressedThisFrame = new HashSet<string>();
        private readonly HashSet<string> _releasedThisFrame = new HashSet<string>();

        public int Priority => -350;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int ActiveCount => _pressed.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            DisposeAll(_buttons);
            DisposeAll(_axes1D);
            DisposeAll(_axes2D);
            _pressed.Clear();
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
        }

        // —— 注册 ——

        /// <summary>
        /// 注册按钮 action（如 Jump / Fire）。binding 例: "&lt;Keyboard&gt;/space"。
        /// </summary>
        public void RegisterButton(string actionName, string binding)
        {
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentException("Action name must be non-empty.", nameof(actionName));
            if (string.IsNullOrEmpty(binding))
                throw new ArgumentException("Binding must be non-empty.", nameof(binding));
            if (_buttons.ContainsKey(actionName))
                throw new InvalidOperationException($"Button action '{actionName}' already registered.");

            var action = new InputAction(actionName, InputActionType.Button, binding);
            action.performed += ctx => OnButtonPerformed(actionName);
            action.canceled += ctx => OnButtonCanceled(actionName);
            action.Enable();
            _buttons[actionName] = action;
        }

        /// <summary>
        /// 注册 1D 轴 action（如 Horizontal / Throttle）。binding 例: "&lt;Gamepad&gt;/leftTrigger"。
        /// </summary>
        public void RegisterAxis(string actionName, string binding)
        {
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentException("Action name must be non-empty.", nameof(actionName));
            if (string.IsNullOrEmpty(binding))
                throw new ArgumentException("Binding must be non-empty.", nameof(binding));
            if (_axes1D.ContainsKey(actionName))
                throw new InvalidOperationException($"Axis action '{actionName}' already registered.");

            var action = new InputAction(actionName, InputActionType.Value, binding, expectedControlType: "Axis");
            action.Enable();
            _axes1D[actionName] = action;
        }

        /// <summary>
        /// 注册 2D 轴 action（如 Move / Look）。binding 例: "&lt;Gamepad&gt;/leftStick"。
        /// </summary>
        public void RegisterAxis2D(string actionName, string binding)
        {
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentException("Action name must be non-empty.", nameof(actionName));
            if (string.IsNullOrEmpty(binding))
                throw new ArgumentException("Binding must be non-empty.", nameof(binding));
            if (_axes2D.ContainsKey(actionName))
                throw new InvalidOperationException($"Axis2D action '{actionName}' already registered.");

            var action = new InputAction(actionName, InputActionType.Value, binding, expectedControlType: "Vector2");
            action.Enable();
            _axes2D[actionName] = action;
        }

        /// <summary>
        /// 取消注册 action（任意类型）。Disable + Dispose 内部 InputAction。返回是否真的删了。
        /// </summary>
        public bool UnregisterAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return false;
            return Remove(_buttons, actionName) || Remove(_axes1D, actionName) || Remove(_axes2D, actionName);
        }

        // —— 查询（IInputModule 实现）——

        public bool IsPressed(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return _pressed.Contains(action);
        }

        public bool WasPressedThisFrame(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return _pressedThisFrame.Contains(action);
        }

        public bool WasReleasedThisFrame(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return _releasedThisFrame.Contains(action);
        }

        public float GetAxis(string action)
        {
            if (string.IsNullOrEmpty(action)) return 0f;
            return _axes1D.TryGetValue(action, out var a) ? a.ReadValue<float>() : 0f;
        }

        public void GetAxis2D(string action, out float x, out float y)
        {
            x = 0f; y = 0f;
            if (string.IsNullOrEmpty(action)) return;
            if (_axes2D.TryGetValue(action, out var a))
            {
                var v = a.ReadValue<UnityEngine.Vector2>();
                x = v.x;
                y = v.y;
            }
        }

        // —— 内部 ——

        private void OnButtonPerformed(string actionName)
        {
            if (_pressed.Add(actionName))
                _pressedThisFrame.Add(actionName);
        }

        private void OnButtonCanceled(string actionName)
        {
            if (_pressed.Remove(actionName))
                _releasedThisFrame.Add(actionName);
        }

        private static bool Remove(Dictionary<string, InputAction> dict, string key)
        {
            if (dict.TryGetValue(key, out var a))
            {
                a.Disable();
                a.Dispose();
                dict.Remove(key);
                return true;
            }
            return false;
        }

        private static void DisposeAll(Dictionary<string, InputAction> dict)
        {
            foreach (var a in dict.Values)
            {
                a.Disable();
                a.Dispose();
            }
            dict.Clear();
        }
    }
}
