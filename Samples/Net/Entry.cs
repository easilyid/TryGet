using System;
using System.Diagnostics;
using System.Threading;
using TryGet;
using TryGet.Async;

namespace TryGet.Samples.Net
{
    /// <summary>
    /// V0.7 Iter 3 — Net 端启动模板。
    ///
    /// 业务用 <see cref="Run"/> 启动框架。本类**不在 Core**（不定义 IEntry interface，参考 Fantasy/ET/BigCat 实践），
    /// 是 Net 端"如何用 Bootstrap 启动框架"的范本，可被业务直接拷贝改造。
    ///
    /// Unity 端的 Entry 形态是 MonoBehaviour（V1.0+ 落地，放在 Samples/Unity/）。
    /// </summary>
    public static class Entry
    {
        /// <summary>
        /// 启动框架并跑业务 mainAsync 直至完成或超时。
        /// </summary>
        /// <param name="setup">在 <c>host.Initialize</c> 之前的额外 Module 注册（可选）。</param>
        /// <param name="mainAsync">业务主流程。返回 TGTask 完成后主循环退出。</param>
        /// <param name="options">Bootstrap 配置（可选）。</param>
        /// <param name="frameSleepMs">每帧 sleep 毫秒数（控制 CPU 占用）。默认 16 ≈ 60FPS。</param>
        /// <param name="timeoutMs">主循环超时毫秒数。&lt;=0 表示无超时（生产服务器无限运行）。</param>
        /// <returns>0=成功 / 1=mainAsync 抛异常 / 2=超时 / 3=setup 抛异常</returns>
        public static int Run(
            Action<IModuleHost> setup,
            Func<IModuleHost, TGTask> mainAsync,
            BootstrapOptions options = null,
            int frameSleepMs = 16,
            long timeoutMs = -1)
        {
            if (mainAsync == null) throw new ArgumentNullException(nameof(mainAsync));

            var host = Bootstrap.CreateHost(options);
            ILogger logger = host.Get<ILogger>();

            try
            {
                try { setup?.Invoke(host); }
                catch (Exception ex)
                {
                    logger.Error("Entry.Run setup callback threw", ex);
                    return 3;
                }

                host.Initialize();

                var mainTask = mainAsync(host);
                var sw = Stopwatch.StartNew();
                long lastMs = 0;

                while (!mainTask.IsCompleted)
                {
                    long nowMs = sw.ElapsedMilliseconds;
                    float dt = (nowMs - lastMs) / 1000f;
                    lastMs = nowMs;
                    host.Update(dt, dt);

                    if (timeoutMs > 0 && nowMs > timeoutMs)
                    {
                        logger.Error($"Entry.Run timeout after {timeoutMs}ms");
                        return 2;
                    }

                    Thread.Sleep(frameSleepMs);
                }

                try
                {
                    mainTask.GetAwaiter().GetResult();
                    return 0;
                }
                catch (Exception ex)
                {
                    logger.Error("Entry.Run mainAsync threw", ex);
                    return 1;
                }
            }
            finally
            {
                host.Shutdown();
            }
        }
    }
}
