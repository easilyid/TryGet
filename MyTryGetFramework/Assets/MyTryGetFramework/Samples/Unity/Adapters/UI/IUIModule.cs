using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// UI 服务契约（V0.4 Common Module）。
    ///
    /// 设计原则：Core 层只管理"哪些 UI 在打开"的状态，**不包含渲染、布局、Canvas 层级、Modal 屏蔽**等 Unity 特定逻辑。
    /// 实际 UGUI 渲染由 Adapters/UGUI 层的 <c>UGUIUIModule</c>（继承本接口）负责（V0.4 后续迭代）。
    ///
    /// Memory 实现（<see cref="MemoryUIModule"/>）只维护一个 List+HashSet 状态机，
    /// 用于单元测试、Headless 服务端、UI 流程逻辑测试（不依赖 UnityEngine.UI）。
    ///
    /// 进阶（分层 Canvas / Modal / 异步加载 / 数据传参）由 Adapter 接口扩展，**不污染 Core**。
    /// </summary>
    public interface IUIModule : IModule
    {
        /// <summary>
        /// 打开一个 UI。重复 Open 同名抛 <see cref="InvalidOperationException"/>。
        /// Adapter 实现可重写为"已存在则置顶"语义。
        /// </summary>
        void Open(string uiName);

        /// <summary>
        /// 关闭一个 UI。未打开时静默（幂等），不抛。
        /// </summary>
        void Close(string uiName);

        /// <summary>
        /// 是否打开。null/empty 返回 false。
        /// </summary>
        bool IsOpen(string uiName);

        /// <summary>
        /// 当前打开的 UI 列表，按打开顺序（最早→最晚）。返回的视图为只读快照语义。
        /// </summary>
        IReadOnlyList<string> OpenedUIs { get; }

        /// <summary>
        /// 打开的 UI 数量。
        /// </summary>
        int OpenedCount { get; }

        /// <summary>
        /// 关闭全部 UI。等价于对每个 OpenedUIs 调 Close，但更高效。
        /// </summary>
        void CloseAll();
    }
}
