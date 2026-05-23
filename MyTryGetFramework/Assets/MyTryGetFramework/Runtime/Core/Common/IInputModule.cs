using System;

namespace TryGet
{
    /// <summary>
    /// 输入服务契约（V0.5 Common Module，V0.4 deferred 补齐）。
    ///
    /// 设计原则：Core 层只定义"Action 抽象"（业务用名字查询 "Jump" / "Move" 而非直接绑按键），
    /// **不依赖 Unity InputSystem / Input Manager**。Adapters/Unity 层的 UnityInputModule
    /// 把 IInputAction 映射成 Action 名（V0.5 后续迭代）。
    ///
    /// Memory 实现（<see cref="MemoryInputModule"/>）暴露 SimulatePress/Release 测试 API，
    /// 让 EditMode / Procedure 流程测试能复现"按键序列"。
    ///
    /// Per-frame edge 语义：WasPressedThisFrame / WasReleasedThisFrame 在每帧 OnUpdate
    /// 调用后清空，业务必须在 Update 内立即查询，否则错过。这与 Unity Input.GetKeyDown 一致。
    /// </summary>
    public interface IInputModule : IModule
    {
        /// <summary>
        /// 该 action 是否当前按下（按住状态）。
        /// </summary>
        bool IsPressed(string action);

        /// <summary>
        /// 该 action 是否本帧按下（edge 触发）。下一帧 Update 后归 false。
        /// </summary>
        bool WasPressedThisFrame(string action);

        /// <summary>
        /// 该 action 是否本帧释放（edge 触发）。下一帧 Update 后归 false。
        /// </summary>
        bool WasReleasedThisFrame(string action);

        /// <summary>
        /// 1D 轴值（如方向键水平 / 摇杆水平）。范围 [-1, 1]，无值返回 0。
        /// </summary>
        float GetAxis(string action);

        /// <summary>
        /// 2D 轴值（如方向键 / 摇杆方向）。无值返回 (0, 0)。
        /// </summary>
        void GetAxis2D(string action, out float x, out float y);

        /// <summary>
        /// 当前按下的 action 数量（诊断用）。
        /// </summary>
        int ActiveCount { get; }
    }
}
