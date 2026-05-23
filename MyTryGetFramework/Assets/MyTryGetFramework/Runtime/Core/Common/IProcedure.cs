namespace TryGet
{
    /// <summary>
    /// 单个 Procedure（跨帧流程节点）的契约。
    ///
    /// 用法：实现 IProcedure 或继承 <see cref="ProcedureBase"/>（提供空默认实现）。
    /// 通过 <see cref="IProcedureModule.AddProcedure"/> 注册，<see cref="IProcedureModule.TransitionTo"/> 切换。
    ///
    /// 生命周期：进入 → 多帧 Update → 退出。
    /// </summary>
    public interface IProcedure
    {
        /// <summary>
        /// 进入此 Procedure 时调用。可在此发起异步加载、订阅事件等。
        /// </summary>
        void OnEnter(IProcedureModule module);

        /// <summary>
        /// 每帧 Update 调用（仅当此 Procedure 为当前 Procedure 时）。
        /// 通常在此根据条件触发 <see cref="IProcedureModule.TransitionTo"/>。
        /// </summary>
        void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 退出此 Procedure 时调用。可在此清理资源、退订事件。
        /// </summary>
        void OnExit(IProcedureModule module);
    }

    /// <summary>
    /// IProcedure 抽象基类。提供空默认实现，子类只 override 关心的回调即可。
    /// </summary>
    public abstract class ProcedureBase : IProcedure
    {
        public virtual void OnEnter(IProcedureModule module) { }
        public virtual void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime) { }
        public virtual void OnExit(IProcedureModule module) { }
    }
}
