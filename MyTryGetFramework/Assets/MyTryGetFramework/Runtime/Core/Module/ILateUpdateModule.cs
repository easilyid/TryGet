namespace TryGet
{
    /// <summary>
    /// 帧 LateUpdate 阶段需要被驱动的 Module 实现此接口。
    /// 典型用途：相机跟随、UI 位置同步、状态汇总等需要在 Update 之后执行的逻辑。
    /// </summary>
    public interface ILateUpdateModule : IModule
    {
        /// <summary>
        /// 每帧由 ModuleHost.LateUpdate 调用，按 OnInit 顺序执行。
        /// 参数与 <see cref="IUpdateModule.Update"/> 对齐。
        /// </summary>
        /// <param name="deltaTime">受 timeScale 影响的帧间隔（秒）。</param>
        /// <param name="unscaledDeltaTime">不受 timeScale 影响的帧间隔（秒）。</param>
        void LateUpdate(float deltaTime, float unscaledDeltaTime);
    }
}
