namespace TryGet.Async
{
    /// <summary>
    /// 异步调度服务。把"下一帧 / 延迟 N 秒 / 等待 N 帧"转化为可 await 的 TGTask。
    ///
    /// 实现为 <see cref="IModule"/> + <see cref="IUpdateModule"/>：注册到 <see cref="IModuleHost"/> 后，
    /// 每帧由 host 驱动 <see cref="IUpdateModule.Update"/> 检查到期任务。
    ///
    /// 单线程模型：所有 API 必须在主线程调用。
    ///
    /// 推荐 Priority：-150（介于 Procedure=-200 和用户模块默认优先级之间）。
    /// </summary>
    public interface ITGTaskScheduler : IModule, IUpdateModule
    {
        /// <summary>下一帧完成的 TGTask。等价 Unity 的 yield return null。</summary>
        TGTask Yield();

        /// <summary>累计经过 <paramref name="seconds"/> 秒后完成的 TGTask（受 Update 的 dt 控制）。</summary>
        TGTask Delay(float seconds);

        /// <summary>经过 <paramref name="frameCount"/> 帧后完成的 TGTask。</summary>
        TGTask WaitForFrames(int frameCount);
    }
}
