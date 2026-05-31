using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 框架能力单元契约（ADR-0011）。一个 IModule 实例代表一个由 ModuleSystem 托管的运行时能力。
    ///
    /// 纪律：
    /// - 每个 Module 必须先有 I{Name}Module 接口，再有实现
    /// - Module 之间通过 host.Get&lt;T&gt;() 注入；构造函数禁止 new 其他 Module
    /// - DependsOn 显式声明前置依赖，ModuleSystem 据此拓扑排序
    /// - Priority 仅作拓扑排序的 tie-breaker（同优先级下声明顺序为最终序）
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// 排序权重（tie-breaker）。数值越小越早 OnInit、越晚 Shutdown。
        /// 拓扑序优先于 Priority，仅在拓扑等价时按 Priority 决定。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 显式依赖的其他 Module 接口类型。ModuleSystem 据此拓扑排序，并检测循环依赖。
        /// 返回空集合表示无依赖。
        /// </summary>
        IReadOnlyList<Type> DependsOn { get; }

        /// <summary>
        /// 由 ModuleSystem 在所有依赖都已 OnInit 后调用。允许通过 host.Get&lt;T&gt;() 拉依赖。
        /// </summary>
        void OnInit(IModuleSystem host);

        /// <summary>
        /// 由 ModuleSystem 在关闭流程中按 OnInit 的逆序调用。Module 应在此清理资源。
        /// </summary>
        void Shutdown();
    }
}
