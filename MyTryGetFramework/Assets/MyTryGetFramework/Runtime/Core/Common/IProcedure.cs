namespace TryGet
{
    /// <summary>
    /// 单个 Procedure（跨帧流程节点）的契约。
    ///
    /// 生命周期：OnEnter → 多帧 OnUpdate → OnExit。
    /// 栈操作时额外触发：OnPause（被 Push 覆盖）/ OnResume（上层 Pop 后恢复）。
    /// </summary>
    public interface IProcedure
    {
        void OnEnter(IProcedureModule module);
        void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime);
        void OnExit(IProcedureModule module);
        void OnPause(IProcedureModule module);
        void OnResume(IProcedureModule module);
    }

    /// <summary>
    /// IProcedure 抽象基类。提供空默认实现，子类只 override 关心的回调即可。
    /// </summary>
    public abstract class ProcedureBase : IProcedure
    {
        public virtual void OnEnter(IProcedureModule module) { }
        public virtual void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime) { }
        public virtual void OnExit(IProcedureModule module) { }
        public virtual void OnPause(IProcedureModule module) { }
        public virtual void OnResume(IProcedureModule module) { }
    }
}
