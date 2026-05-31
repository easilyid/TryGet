namespace TryGet
{
    /// <summary>
    /// 帧 FixedUpdate 阶段需要被驱动的 Module 实现此接口。
    /// </summary>
    public interface IFixedUpdateModule : IModule
    {
        /// <summary>
        /// 每帧由 ModuleSystem.FixedUpdate 调用，按 OnInit 顺序执行。
        /// </summary>
        /// <param name="deltaTime">受 timeScale 影响的帧间隔（秒）。</param>
        /// <param name="unscaledDeltaTime">不受 timeScale 影响的帧间隔（秒）。</param>
        void FixedUpdate(float deltaTime, float unscaledDeltaTime);
    }
}
