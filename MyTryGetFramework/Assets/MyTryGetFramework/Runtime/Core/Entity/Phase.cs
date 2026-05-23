namespace TryGet
{
    /// <summary>
    /// World 内固定的生命周期阶段。
    /// V0.1 固定为 Enter、Update、Exit 三阶段。
    /// </summary>
    public enum Phase
    {
        /// <summary>
        /// 进入阶段 — setup System 在更新前运行一次。
        /// </summary>
        Enter = 0,

        /// <summary>
        /// 更新阶段 — 运行时 System 每帧重复执行。
        /// </summary>
        Update = 1,

        /// <summary>
        /// 退出阶段 — teardown System 在关闭时运行一次。
        /// </summary>
        Exit = 2,
    }
}
