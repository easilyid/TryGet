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
    ///
    /// **生命周期异常契约（同步与异步一致）**：<c>OnEnter</c> / <c>OnExit</c> / <c>OnPause</c> /
    /// <c>OnResume</c>（及其 Async 版本）抛出的异常**不会从切换方法同步逃逸**，而是经返回的切换 task
    /// 上报（<c>await</c> / <c>GetResult</c> 抛出）并写入 <see cref="LastAsyncError"/>。失败后模块状态有
    /// 明确定义、不会停在半切换：
    /// <list type="bullet">
    ///   <item>Enter 失败：目标 Procedure 仍留在栈上（"已进入但出错"），调用 <see cref="Stop"/> 复位。</item>
    ///   <item>Exit 失败：栈顶照常移除（退出不可逆）；<see cref="Replace"/> 在 exit 失败时不进入替换目标。</item>
    ///   <item>Push 的 OnPause 失败：中止 Push，不进入目标，当前 Procedure 仍为栈顶。</item>
    ///   <item>Pop 的 OnResume 失败：栈顶已退出、下层已成为新栈顶，仅 resume 出错。</item>
    /// </list>
    /// 注意：参数校验类错误（id 为空 / 未注册 / 未启动 / 异步切换进行中）仍同步抛
    /// <see cref="System.InvalidOperationException"/>，与生命周期异常区分。
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
