using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// 异步 Procedure 契约。
    ///
    /// 继承 <see cref="IProcedure"/>：必须实现同步的 OnEnter/OnUpdate/OnExit（推荐空实现），
    /// 同时实现 <see cref="OnEnterAsync"/> / <see cref="OnExitAsync"/> 承载异步逻辑。
    ///
    /// 调度语义：
    /// - <see cref="IProcedureModule.Start"/> / <see cref="IProcedureModule.TransitionTo"/> 进入此 Procedure 时：
    ///   1. 先同步调 <see cref="IProcedure.OnEnter"/>（与普通 IProcedure 兼容）
    ///   2. 再异步跑 <see cref="OnEnterAsync"/>
    /// - OnEnterAsync 未完成期间：
    ///   - <see cref="IProcedureModule.IsEntering"/> = true
    ///   - <see cref="IProcedureModule.Update"/> 跳过 OnUpdate 调用
    ///   - 再次调 TransitionTo / Stop 抛 InvalidOperationException
    /// - <see cref="IProcedureModule.Stop"/> / TransitionTo 离开此 Procedure 时同理跑 <see cref="OnExitAsync"/>。
    ///
    /// 异常处理：OnEnterAsync / OnExitAsync 抛异常时，异常存入
    /// <see cref="IProcedureModule.LastAsyncError"/> 供用户查询，状态机保留在当前 Procedure。
    ///
    /// 推荐使用 <see cref="AsyncProcedureBase"/> 避免手写空 OnEnter/OnExit。
    /// </summary>
    public interface IAsyncProcedure : IProcedure
    {
        /// <summary>
        /// 异步进入逻辑。在同步 OnEnter 之后执行。返回 <c>TGTask.CompletedTask</c> 表示同步路径（立即完成）。
        /// </summary>
        TGTask OnEnterAsync(IProcedureModule module);

        /// <summary>
        /// 异步退出逻辑。在同步 OnExit 之前执行（确保异步资源 release 优先）。
        /// </summary>
        TGTask OnExitAsync(IProcedureModule module);
    }

    /// <summary>
    /// <see cref="IAsyncProcedure"/> 抽象基类，提供同步 + 异步全部空默认实现。
    /// 子类只 override 关心的回调即可。
    /// </summary>
    public abstract class AsyncProcedureBase : ProcedureBase, IAsyncProcedure
    {
        public virtual TGTask OnEnterAsync(IProcedureModule module) => TGTask.CompletedTask;
        public virtual TGTask OnExitAsync(IProcedureModule module) => TGTask.CompletedTask;
    }
}
