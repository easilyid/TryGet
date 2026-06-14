using TryGet.Async;

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
    ///
    /// 取消 scope（ADR-0021 D8，owner-managed，对标 <c>EventScope</c>）：通过 <see cref="CancelToken"/> 发起可取消的
    /// 异步操作（如 <c>scheduler.Delay(.., CancelToken)</c>）；基类 <see cref="OnExit"/> 默认调用 <see cref="CancelScope"/>，
    /// 使本 Procedure 离开栈（Pop/Replace/Stop/Shutdown 触发 OnExit）时自动取消其名下全部 pending（切流程不泄漏）。
    /// <see cref="OnPause"/> **不**取消（暂停可恢复）。子类若 override <see cref="OnExit"/>，请调 <c>base.OnExit(module)</c>
    /// 或自行调用 <see cref="CancelScope"/>。
    /// </summary>
    public abstract class ProcedureBase : IProcedure
    {
        private TGCancelSource _cancelSource;

        /// <summary>本 Procedure 的取消令牌（懒创建）。传给异步操作以便离栈时统一取消。</summary>
        protected TGCancelToken CancelToken => (_cancelSource ??= TGCancelSource.Rent()).Token;

        /// <summary>取消并回收本 Procedure 的取消 scope。基类 <see cref="OnExit"/> 默认调用。</summary>
        protected void CancelScope()
        {
            if (_cancelSource != null)
            {
                _cancelSource.Cancel();
                _cancelSource.Recycle();
                _cancelSource = null;
            }
        }

        public virtual void OnEnter(IProcedureModule module) { }
        public virtual void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime) { }
        public virtual void OnExit(IProcedureModule module) { CancelScope(); }
        public virtual void OnPause(IProcedureModule module) { }
        public virtual void OnResume(IProcedureModule module) { }
    }
}
