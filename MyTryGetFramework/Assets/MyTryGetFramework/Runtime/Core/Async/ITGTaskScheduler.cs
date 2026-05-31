namespace TryGet.Async
{
    /// <summary>
    /// 异步调度服务。把"下一帧 / 延迟 N 秒 / 等待 N 帧"转化为可 await 的 TGTask。
    ///
    /// V2.2+ 支持 Phase-aware 调度：可指定在哪个 FramePhase 恢复异步操作。
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

        /// <summary>累计经过 <paramref name="seconds"/> 秒后完成的 TGTask（默认在 Update 阶段）。</summary>
        TGTask Delay(float seconds);

        /// <summary>在指定 <paramref name="phase"/> 累计经过 <paramref name="seconds"/> 秒后完成的 TGTask。</summary>
        TGTask Delay(float seconds, FramePhase phase);

        /// <summary>经过 <paramref name="frameCount"/> 帧后完成的 TGTask（默认在 Update 阶段）。</summary>
        TGTask WaitForFrames(int frameCount);

        /// <summary>在指定 <paramref name="phase"/> 经过 <paramref name="frameCount"/> 帧后完成的 TGTask。</summary>
        TGTask WaitForFrames(int frameCount, FramePhase phase);

        /// <summary>等到指定 <paramref name="phase"/> 的下一次执行。</summary>
        TGTask DelayUntilPhase(FramePhase phase);
    }
}
