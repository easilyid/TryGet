namespace TryGet
{
    /// <summary>
    /// 跨帧流程状态机服务契约（V2.0 升级为栈模式，吸收 BigCat SceneMgr.stack 设计）。
    ///
    /// 栈语义：
    /// - Push：暂停当前 → 新 Procedure 入栈
    /// - Pop：栈顶退出 → 恢复下层
    /// - Replace：栈顶退出 → 新 Procedure 替换栈顶
    /// </summary>
    public interface IProcedureModule : IModule, IUpdateModule
    {
        string CurrentProcedure { get; }
        bool IsRunning { get; }
        int StackDepth { get; }
        IModuleHost Host { get; }

        void AddProcedure(string id, IProcedure procedure);
        void Start(string initial);
        void Stop();

        /// <summary>暂停当前 Procedure，将目标 Push 入栈。</summary>
        void Push(string target);

        /// <summary>退出栈顶 Procedure，恢复下层。栈空时等同 Stop。</summary>
        void Pop();

        /// <summary>退出栈顶 Procedure，将目标替换为新栈顶。</summary>
        void Replace(string target);

        bool IsEntering { get; }
        bool IsExiting { get; }
        System.Exception LastAsyncError { get; }
    }
}
