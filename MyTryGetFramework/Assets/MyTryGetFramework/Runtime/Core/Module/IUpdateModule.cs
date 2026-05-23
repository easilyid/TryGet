namespace TryGet
{
    /// <summary>
    /// 帧 Update 阶段需要被驱动的 Module 实现此接口。
    /// 不需要 Update 的 Module 不要实现此接口，避免 ModuleHost 空转。
    /// </summary>
    public interface IUpdateModule : IModule
    {
        /// <summary>
        /// 每帧由 ModuleHost.Update 调用，按 OnInit 顺序执行。
        /// </summary>
        /// <param name="deltaTime">受 timeScale 影响的帧间隔（秒）。</param>
        /// <param name="unscaledDeltaTime">不受 timeScale 影响的帧间隔（秒）。</param>
        void Update(float deltaTime, float unscaledDeltaTime);
    }
}
