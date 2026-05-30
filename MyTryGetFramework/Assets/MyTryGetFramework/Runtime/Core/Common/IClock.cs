using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 时钟服务契约（V0.7 起新增）。
    ///
    /// 暴露当前帧 dt + 累计时长 + 帧计数三件套，让 Module 业务无需直接读 <c>UnityEngine.Time</c>。
    /// Net 端由 <see cref="SystemClock"/> 通过 <see cref="IUpdateModule.Update"/> 注入 dt；
    /// Unity 端可由后续 Unity Adapter 读 <c>Time.deltaTime</c> 实现。
    ///
    /// **不暴露 wall-clock**（<c>DateTime.UtcNow</c> / <c>Stopwatch.GetTimestamp</c>）：
    /// - Net 端服务器和 Unity 端客户端的 wall-clock 概念不同（时区 / 同步源 / NTP）
    /// - 真需要 wall-clock 时由 V0.8+ Network 层引入独立 <c>IWallClock</c>
    /// - 这里 <see cref="ElapsedTime"/> 是"自 Host Initialize 起累计 dt"的 game time，与 wall-clock 解耦
    ///
    /// **不替换 <see cref="IUpdateModule.Update"/> 参数**：
    /// - 现存所有 Module 接 (dt, udt) 参数，去掉等于全量重写
    /// - (dt, udt) 是"本帧调度信号"，<see cref="IClock"/> 是"全局时钟状态查询"，职责分离
    ///
    /// 对标：BigCat <c>TimeModule</c>（唯一实现 Clock 抽象的参考框架）；
    /// .NET 8+ <c>TimeProvider</c> 是 wall-clock 取向不适用游戏帧。
    /// </summary>
    public interface IClock : IModule
    {
        /// <summary>当前帧的 scaled deltaTime（受 timeScale 影响，秒）。</summary>
        float DeltaTime { get; }

        /// <summary>当前帧的 unscaled deltaTime（不受 timeScale 影响，秒）。</summary>
        float UnscaledDeltaTime { get; }

        /// <summary>累计 scaled 已运行秒数（自 Initialize 起每帧累加 <see cref="DeltaTime"/>）。</summary>
        double ElapsedTime { get; }

        /// <summary>累计 unscaled 已运行秒数（自 Initialize 起每帧累加 <see cref="UnscaledDeltaTime"/>）。</summary>
        double UnscaledElapsedTime { get; }

        /// <summary>累计帧数（从 0 开始；<see cref="IModuleHost.Update"/> 每次调用 +1）。</summary>
        long FrameCount { get; }
    }

    /// <summary>
    /// <see cref="IClock"/> 的 Net / Headless 默认实现：
    /// 不读 <c>UnityEngine.Time</c>，由 <see cref="IModuleHost.Update"/> 在每帧把 dt 喂进来。
    ///
    /// Priority=-900：在 <see cref="ConsoleLogger"/>（V0.7 Iter 2，Priority=-1000）之后，
    /// 在 <see cref="ITGTaskScheduler"/>（-150）/ <see cref="IProcedureModule"/>（-200）等业务 Module 之前。
    /// 这样 SystemClock.Update 先于业务 Module 跑，业务可读到本帧最新的 DeltaTime / ElapsedTime。
    /// </summary>
    public sealed class SystemClock : IClock, IUpdateModule
    {
        public int Priority => -900;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public float DeltaTime { get; private set; }
        public float UnscaledDeltaTime { get; private set; }
        public double ElapsedTime { get; private set; }
        public double UnscaledElapsedTime { get; private set; }
        public long FrameCount { get; private set; }

        public void OnInit(IModuleHost host) { /* 无依赖 */ }

        public void Shutdown()
        {
            DeltaTime = 0;
            UnscaledDeltaTime = 0;
            ElapsedTime = 0;
            UnscaledElapsedTime = 0;
            FrameCount = 0;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            ElapsedTime += deltaTime;
            UnscaledElapsedTime += unscaledDeltaTime;
            FrameCount++;
        }
    }
}
