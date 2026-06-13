namespace TryGet
{
    /// <summary>
    /// 跨帧流程状态机服务契约（V2.0 升级为栈模式，吸收 BigCat SceneMgr.stack 设计）。
    ///
    /// 栈语义：
    /// - Push：暂停当前 → 新 Procedure 入栈
    /// - Pop：栈顶退出 → 恢复下层
    /// - Replace：栈顶退出 → 新 Procedure 替换栈顶
    ///
    /// 可 await 切换（V2.0 C4）：<see cref="Start"/> / <see cref="Push"/> / <see cref="Pop"/> /
    /// <see cref="Replace"/> 返回表示「本次切换完成」的 <see cref="Async.TGTask"/>。同步流程立即完成；
    /// 异步流程（<see cref="IAsyncProcedure"/>）在 OnEnterAsync/OnExitAsync 全链完成时完成；异步错误
    /// 通过 await 抛出（同时仍写入 <see cref="LastAsyncError"/> 兼容）。调用者可 <c>await module.Push(...)</c>
    /// 等待切换完成，不再需要轮询 <see cref="IsEntering"/> / <see cref="IsExiting"/>。
    /// 返回值可忽略（不接收返回值时行为与旧版 void API 一致）。
    /// </summary>
    public interface IProcedureModule : IModule, IUpdateModule
    {
        string CurrentProcedure { get; }
        bool IsRunning { get; }
        int StackDepth { get; }
        IModuleSystem Host { get; }

        void AddProcedure(string id, IProcedure procedure);

        /// <summary>开始初始流程。返回表示进入完成的切换 task。</summary>
        Async.TGTask Start(string initial);

        /// <summary>强制停止并清空栈（立即清栈，异步 exit 在后台完成）。终结操作，不返回切换 task。</summary>
        void Stop();

        /// <summary>暂停当前 Procedure，将目标 Push 入栈。返回表示目标进入完成的切换 task。</summary>
        Async.TGTask Push(string target);

        /// <summary>退出栈顶 Procedure，恢复下层。栈空时等同 Stop。返回表示退出+恢复完成的切换 task。</summary>
        Async.TGTask Pop();

        /// <summary>退出栈顶 Procedure，将目标替换为新栈顶。返回表示退出+进入全链完成的切换 task。</summary>
        Async.TGTask Replace(string target);

        bool IsEntering { get; }
        bool IsExiting { get; }
        System.Exception LastAsyncError { get; }
    }
}
