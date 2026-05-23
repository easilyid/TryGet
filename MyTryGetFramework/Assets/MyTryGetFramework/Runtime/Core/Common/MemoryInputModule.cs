using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IInputModule 的内存实现（跨端，零外部依赖）。
    ///
    /// 用途：EditMode 测试、Procedure 流程测试、Headless 模拟玩家输入。
    /// 提供 SimulatePress / SimulateRelease / SetAxis / SetAxis2D 等"可控"API
    /// 让测试代码能复现真实输入序列。
    ///
    /// 实现细节：是 <see cref="ILateUpdateModule"/>（而非 IUpdateModule），
    /// 在 **LateUpdate** 阶段清空 _pressedThisFrame / _releasedThisFrame 实现"per-frame edge"语义。
    /// 选 LateUpdate 而非 Update 的原因：业务（Procedure / Aspect / System）在 Update 阶段
    /// 读 WasPressedThisFrame，如果在 Update 阶段先清边，业务就永远看不到本帧输入。
    /// LateUpdate 在所有 Update 之后跑，保证业务先消费 edge 再清空，与 Unity Input 行为一致。
    ///
    /// Production Unity 由 Adapters/Unity 层的 UnityInputModule（接 InputSystem 的
    /// InputAction）替换。
    /// </summary>
    public sealed class MemoryInputModule : IInputModule, ILateUpdateModule
    {
        private readonly HashSet<string> _pressed = new HashSet<string>();
        private readonly HashSet<string> _pressedThisFrame = new HashSet<string>();
        private readonly HashSet<string> _releasedThisFrame = new HashSet<string>();
        private readonly Dictionary<string, float> _axis1D = new Dictionary<string, float>();
        private readonly Dictionary<string, (float x, float y)> _axis2D = new Dictionary<string, (float, float)>();

        // 介于 Audio (-380) 与 UI (-300) 之间：Input 是基础设施，
        // Procedure / 业务 OnUpdate 时需要看到本帧 edge 状态。
        public int Priority => -350;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int ActiveCount => _pressed.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            _pressed.Clear();
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
            _axis1D.Clear();
            _axis2D.Clear();
        }

        /// <summary>
        /// 每帧 LateUpdate：清空 edge 集合（让 WasPressedThisFrame 在下一帧之后归 false）。
        /// 在 LateUpdate 而非 Update 中清，保证业务 Update 阶段能消费本帧 edge。
        /// </summary>
        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
        }

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
            return _axis1D.TryGetValue(action, out var v) ? v : 0f;
        }

        public void GetAxis2D(string action, out float x, out float y)
        {
            x = 0f; y = 0f;
            if (string.IsNullOrEmpty(action)) return;
            if (_axis2D.TryGetValue(action, out var v))
            {
                x = v.x;
                y = v.y;
            }
        }

        // —— 测试 / 模拟 API ——

        /// <summary>
        /// 模拟按下 action。设置 IsPressed=true + WasPressedThisFrame=true。
        /// 已经按下的 action 重复按不会重复触发 edge。
        /// </summary>
        public void SimulatePress(string action)
        {
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("Action must be non-empty.", nameof(action));

            if (_pressed.Add(action))
            {
                // 仅当从未按下变按下时记 edge
                _pressedThisFrame.Add(action);
            }
        }

        /// <summary>
        /// 模拟释放 action。设置 IsPressed=false + WasReleasedThisFrame=true。
        /// 未按下的 action 调 Release 不触发 edge。
        /// 注意：同帧 Press→Release，Press edge 仍保留（与 Unity Input.GetKeyDown/Up 同帧均返回 true 一致）。
        /// </summary>
        public void SimulateRelease(string action)
        {
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("Action must be non-empty.", nameof(action));

            if (_pressed.Remove(action))
            {
                _releasedThisFrame.Add(action);
            }
        }

        /// <summary>
        /// 设置 1D 轴值。自动 clamp 到 [-1, 1]。
        /// </summary>
        public void SetAxis(string action, float value)
        {
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("Action must be non-empty.", nameof(action));
            _axis1D[action] = ClampAxis(value);
        }

        /// <summary>
        /// 设置 2D 轴值。x/y 各自 clamp 到 [-1, 1]。
        /// </summary>
        public void SetAxis2D(string action, float x, float y)
        {
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("Action must be non-empty.", nameof(action));
            _axis2D[action] = (ClampAxis(x), ClampAxis(y));
        }

        private static float ClampAxis(float v)
        {
            if (v < -1f) return -1f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
