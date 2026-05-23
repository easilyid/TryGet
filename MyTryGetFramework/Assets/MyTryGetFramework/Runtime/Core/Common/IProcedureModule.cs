namespace TryGet
{
    /// <summary>
    /// 跨帧流程状态机服务契约（V0.3 起，design.md §7）。
    ///
    /// 与 EntityWorld 的 Phase（帧内 Enter/Update/Exit 三档）正交：
    /// - Phase 是帧内 ECS 调度层级
    /// - Procedure 是跨帧的"游戏流程"层级（如 Boot → Login → InGame → Settle）
    ///
    /// 关键纪律（design.md §13 #6）：Procedure 不混入热更逻辑（修正 TEngine 反模式）。
    /// 热更/资源加载是 ResourceModule / HotReloadModule 的职责，Procedure 只编排"何时切到哪一步"。
    /// </summary>
    public interface IProcedureModule : IModule, IUpdateModule
    {
        /// <summary>
        /// 当前活动 Procedure 的 id。未启动时为 null。
        /// </summary>
        string CurrentState { get; }

        /// <summary>
        /// 是否有 Procedure 正在运行。
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 持有的 ModuleHost 引用，在 <see cref="IModule.OnInit"/> 时被注入。
        /// Procedure 通过此引用拉取其他 Module（如 ITimerModule / IEntityWorld）。
        /// 未通过 ModuleHost 驱动时为 null。
        /// </summary>
        IModuleHost Host { get; }

        /// <summary>
        /// 注册一个 Procedure 到此 Module。id 在此 Module 内唯一，重复注册抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void AddProcedure(string id, IProcedure procedure);

        /// <summary>
        /// 启动 Procedure 状态机。调用初始 Procedure 的 OnEnter。
        /// 必须先 <see cref="AddProcedure"/> 注册 <paramref name="initial"/> id 对应的 Procedure。
        /// </summary>
        void Start(string initial);

        /// <summary>
        /// 切换到目标 Procedure：当前 Procedure.OnExit → 目标 Procedure.OnEnter。
        /// 同状态切换允许（视作 Exit → Enter 同状态重启）。
        /// </summary>
        void TransitionTo(string target);

        /// <summary>
        /// 停止状态机：调用当前 Procedure.OnExit 后置为未运行。
        /// 允许重复调用（未运行时静默 return）。
        /// </summary>
        void Stop();
    }
}
