namespace TryGet.Async
{
    /// <summary>
    /// 异步调度服务。把"下一帧 / 延迟 N 秒 / 等待 N 帧"转化为可 await 的 TGTask。
    ///
    /// V2.2+ 支持 Phase-aware 调度：可指定在哪个 FramePhase 恢复异步操作。
    /// C3+ 支持 Scaled / Unscaled time：可指定延迟是否受 Time.timeScale 影响。
    ///
    /// 实现为 <see cref="IModule"/> + 5 个 Update 接口：注册到 <see cref="IModuleSystem"/> 后，
    /// 每帧由 host 驱动所有 Phase 的 Update 方法检查到期任务。
    ///
    /// 单线程模型：所有 API 必须在主线程调用。
    ///
    /// 推荐 Priority：-150（介于 Procedure=-200 和用户模块默认优先级之间）。
    /// </summary>
    public interface ITGTaskScheduler : IModule,
        IEarlyUpdateModule,
        IFixedUpdateModule,
        IUpdateModule,
        ILateUpdateModule,
        IEndOfFrameModule
    {
        /// <summary>下一帧完成的 TGTask（默认在 Update 阶段）。等价 Unity 的 yield return null。</summary>
        TGTask Yield();

        /// <summary>在指定 <paramref name="phase"/> 的下一次执行时完成的 TGTask。</summary>
        TGTask Yield(FramePhase phase);

        /// <summary>
        /// 累计经过 <paramref name="seconds"/> 秒后完成的 TGTask（默认在 Update 阶段，Scaled time）。
        /// </summary>
        TGTask Delay(float seconds);

        /// <summary>
        /// 在指定 <paramref name="phase"/> 累计经过 <paramref name="seconds"/> 秒后完成的 TGTask（Scaled time）。
        /// </summary>
        TGTask Delay(float seconds, FramePhase phase);

        /// <summary>
        /// C3：累计经过 <paramref name="seconds"/> 秒后完成的 TGTask（默认在 Update 阶段）。
        /// <paramref name="timeMode"/> 指定是否受 Time.timeScale 影响。
        /// </summary>
        TGTask Delay(float seconds, TimeMode timeMode);

        /// <summary>
        /// C3：在指定 <paramref name="phase"/> 累计经过 <paramref name="seconds"/> 秒后完成的 TGTask。
        /// <paramref name="timeMode"/> 指定是否受 Time.timeScale 影响。
        /// </summary>
        TGTask Delay(float seconds, FramePhase phase, TimeMode timeMode);

        /// <summary>经过 <paramref name="frameCount"/> 帧后完成的 TGTask（默认在 Update 阶段）。</summary>
        TGTask WaitForFrames(int frameCount);

        /// <summary>在指定 <paramref name="phase"/> 经过 <paramref name="frameCount"/> 帧后完成的 TGTask。</summary>
        TGTask WaitForFrames(int frameCount, FramePhase phase);

        /// <summary>等到指定 <paramref name="phase"/> 的下一次执行。</summary>
        TGTask DelayUntilPhase(FramePhase phase);

        // ---- 可取消重载（ADR-0021）：传入 TGCancelToken，取消时绑定的 TGTask 以 OperationCanceledException 完成。 ----

        /// <summary>可取消版 <see cref="Yield()"/>（默认 Update 阶段）。</summary>
        TGTask Yield(TGCancelToken token);

        /// <summary>可取消版 <see cref="Yield(FramePhase)"/>。</summary>
        TGTask Yield(FramePhase phase, TGCancelToken token);

        /// <summary>可取消版 <see cref="Delay(float)"/>（默认 Update 阶段、Scaled time）。</summary>
        TGTask Delay(float seconds, TGCancelToken token);

        /// <summary>可取消版 <see cref="Delay(float, FramePhase, TimeMode)"/>。</summary>
        TGTask Delay(float seconds, FramePhase phase, TimeMode timeMode, TGCancelToken token);

        /// <summary>可取消版 <see cref="WaitForFrames(int)"/>（默认 Update 阶段）。</summary>
        TGTask WaitForFrames(int frameCount, TGCancelToken token);

        /// <summary>可取消版 <see cref="WaitForFrames(int, FramePhase)"/>。</summary>
        TGTask WaitForFrames(int frameCount, FramePhase phase, TGCancelToken token);
    }
}
