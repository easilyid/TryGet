namespace TryGet
{
    /// <summary>
    /// 服务端 fixed-tick 循环抽象（V1.0 起）。
    ///
    /// 与 V0.7 <see cref="IClock"/>、V0.5 <see cref="IUpdateModule"/> 的关系：
    /// <list type="bullet">
    ///   <item><see cref="IClock"/> = 时间查询门面（DeltaTime / ElapsedTime / FrameCount）— "几点了？"</item>
    ///   <item><see cref="IUpdateModule"/> = 业务每帧回调（dt 是上帧实际耗时）— "客户端可变帧"</item>
    ///   <item><see cref="ITickLoop"/> = 业务每 tick 回调（dt 是固定 <see cref="TickInterval"/>）— "服务端定步长"</item>
    /// </list>
    ///
    /// 业务实现 ITickLoop，被服务端 driver（V1.1+ Adapter 提供）按 <see cref="TickInterval"/>
    /// 周期回调 <see cref="Tick"/>。Core 不提供 driver 实现；服务端 demo 通常用 <c>while (true)</c>
    /// + <c>TGTaskScheduler.Delay(TickInterval)</c> 自构。
    ///
    /// **典型场景**：
    /// <code>
    /// // MMO 服务端 30 Hz 物理循环
    /// public sealed class PhysicsTickLoop : ITickLoop
    /// {
    ///     public float TickInterval => 1f / 30f;
    ///     public void Tick(float dt) { /* dt 总是 1/30，做物理模拟 */ }
    /// }
    /// </code>
    ///
    /// 多 ITickLoop 共存场景（如物理 30Hz + AI 10Hz）由业务自调度，Core 不强制单例。
    /// </summary>
    public interface ITickLoop
    {
        /// <summary>
        /// 每 tick 间隔（秒）。常量，启动后不变。
        /// 典型值：1/30 (~33ms)、1/60 (~16ms)、0.1 (10Hz AI tick)。
        /// </summary>
        float TickInterval { get; }

        /// <summary>
        /// 本次 tick 业务回调。
        /// <paramref name="dt"/> 总等于 <see cref="TickInterval"/>（不变步长 — 服务端权威模拟靠这个保证 determinism）。
        /// </summary>
        void Tick(float dt);
    }

    /// <summary>
    /// 客户端可变-帧循环抽象（V1.0 起）。
    ///
    /// 与 <see cref="IUpdateModule"/> 的区别：
    /// <list type="bullet">
    ///   <item><see cref="IUpdateModule"/> 是 Module 通过 ModuleHost 集成的"框架级"业务每帧回调</item>
    ///   <item><see cref="IFrameLoop"/> 是更轻量的"业务级"每帧回调，业务自行触发，不经 ModuleHost</item>
    /// </list>
    ///
    /// 典型用法：业务 Aspect 实现 IFrameLoop，由 Aspect 持有的 EntityWorld 在 Update 内触发。
    /// 不强制使用——业务可继续走 IUpdateModule 路线。本接口提供一种"无 Module 包装"的轻量选择。
    ///
    /// **Unity 客户端典型场景**：
    /// <code>
    /// public sealed class CameraFollow : IFrameLoop
    /// {
    ///     public void Frame(float dt, float udt) { /* 跟随逻辑，dt 上帧耗时不固定 */ }
    /// }
    /// </code>
    /// </summary>
    public interface IFrameLoop
    {
        /// <summary>
        /// 本帧回调。
        /// <paramref name="deltaTime"/>：上一帧到本帧的实际秒数（受 <c>Time.timeScale</c> 影响）。
        /// <paramref name="unscaledDeltaTime"/>：同 deltaTime 但不受 timeScale 影响（用于 UI、菜单等）。
        /// </summary>
        void Frame(float deltaTime, float unscaledDeltaTime);
    }
}
