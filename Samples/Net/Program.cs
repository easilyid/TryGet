using System;
using System.Diagnostics;
using System.Threading;
using TryGet;
using TryGet.Async;

namespace TryGet.Samples.Net
{
    /// <summary>
    /// V0.6 Iter 10 — Net 端 sample 入口。
    ///
    /// 演示：
    /// - 在纯 .NET console（无 UnityEngine）跑通 MyTryGetFramework.Core
    /// - 用 TGTask + ITGTaskScheduler 实现异步 Procedure 切换
    /// - 三阶段流转：Boot (异步加载 0.3s) → Login (异步认证 0.3s) → InGame (玩 1s) → 退出
    /// - 主循环 = 同步 tick 驱动（V0.7 IEntry/Bootstrap 落地后将被规范化）
    /// </summary>
    internal static class Program
    {
        private const int FrameSleepMs = 16; // ~60 FPS
        private const long TimeoutMs = 10_000;

        private static int Main()
        {
            var log = new ConsoleLogModule { MinimumLevel = LogLevel.Debug };
            var timer = new TimerModule();
            var sched = new TGTaskScheduler();
            var proc = new ProcedureModule();

            var host = new ModuleHost();
            host.Register<ILogModule>(log);
            host.Register<ITimerModule>(timer);
            host.Register<ITGTaskScheduler>(sched);
            host.Register<IProcedureModule>(proc);

            // 全局未观察异常钩子（V0.6 Iter 8 落地）
            Action<Exception> unobserved = ex => log.Error("Unobserved TGTask exception", ex);
            TGTaskScheduler.UnobservedException += unobserved;

            try
            {
                host.Initialize();

                proc.AddProcedure("boot", new BootProcedure(log, sched));
                proc.AddProcedure("login", new LoginProcedure(log, sched));
                proc.AddProcedure("ingame", new InGameProcedure(log, sched));

                var mainTask = RunMainAsync(log, sched, proc);

                // 主循环：tick 驱动 host.Update，直至 mainTask 完成或超时
                var sw = Stopwatch.StartNew();
                long lastMs = 0;
                while (!mainTask.IsCompleted)
                {
                    long nowMs = sw.ElapsedMilliseconds;
                    float dt = (nowMs - lastMs) / 1000f;
                    lastMs = nowMs;
                    host.Update(dt, dt);

                    if (nowMs > TimeoutMs)
                    {
                        log.Error($"MainAsync did not complete in {TimeoutMs}ms — aborting.");
                        return 2;
                    }

                    Thread.Sleep(FrameSleepMs);
                }

                try
                {
                    mainTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Error("MainAsync threw", ex);
                    return 1;
                }

                log.Info("=== Program exit (success) ===");
                return 0;
            }
            finally
            {
                TGTaskScheduler.UnobservedException -= unobserved;
                host.Shutdown();
            }
        }

        /// <summary>
        /// 主异步流程：手动编排三阶段 Procedure 切换。
        /// V0.7 IEntry/Bootstrap 落地后，此函数会归并到 <c>IEntry.RunAsync</c>。
        /// </summary>
        private static async TGTask RunMainAsync(ILogModule log, ITGTaskScheduler sched, IProcedureModule proc)
        {
            log.Info("=== MainAsync start ===");

            proc.Start("boot");
            await WaitForEnter(proc, sched);

            proc.TransitionTo("login");
            await WaitForEnter(proc, sched);

            proc.TransitionTo("ingame");
            await WaitForEnter(proc, sched);

            // InGame play time
            log.Info("InGame] playing for 1.0s...");
            await sched.Delay(1.0f);

            proc.Stop();
            await WaitForExit(proc, sched);

            log.Info("=== MainAsync done ===");
        }

        private static async TGTask WaitForEnter(IProcedureModule proc, ITGTaskScheduler sched)
        {
            while (proc.IsEntering) await sched.Yield();
        }

        private static async TGTask WaitForExit(IProcedureModule proc, ITGTaskScheduler sched)
        {
            while (proc.IsExiting) await sched.Yield();
        }
    }

    // ============= Procedure 实现 =============

    internal sealed class BootProcedure : AsyncProcedureBase
    {
        private readonly ILogModule _log;
        private readonly ITGTaskScheduler _sched;

        public BootProcedure(ILogModule log, ITGTaskScheduler sched) { _log = log; _sched = sched; }

        public override void OnEnter(IProcedureModule m) => _log.Info("Boot] OnEnter (sync)");
        public override void OnExit(IProcedureModule m) => _log.Info("Boot] OnExit (sync)");

        public override async TGTask OnEnterAsync(IProcedureModule m)
        {
            _log.Info("Boot] async: loading config...");
            await _sched.Delay(0.3f);
            _log.Info("Boot] async: config loaded");
        }
    }

    internal sealed class LoginProcedure : AsyncProcedureBase
    {
        private readonly ILogModule _log;
        private readonly ITGTaskScheduler _sched;

        public LoginProcedure(ILogModule log, ITGTaskScheduler sched) { _log = log; _sched = sched; }

        public override void OnEnter(IProcedureModule m) => _log.Info("Login] OnEnter (sync)");
        public override void OnExit(IProcedureModule m) => _log.Info("Login] OnExit (sync)");

        public override async TGTask OnEnterAsync(IProcedureModule m)
        {
            _log.Info("Login] async: authenticating...");
            await _sched.Delay(0.3f);
            _log.Info("Login] async: authenticated");
        }
    }

    internal sealed class InGameProcedure : AsyncProcedureBase
    {
        private readonly ILogModule _log;
        private readonly ITGTaskScheduler _sched;

        public InGameProcedure(ILogModule log, ITGTaskScheduler sched) { _log = log; _sched = sched; }

        public override void OnEnter(IProcedureModule m) => _log.Info("InGame] OnEnter (sync)");
        public override void OnExit(IProcedureModule m) => _log.Info("InGame] OnExit (sync)");

        public override async TGTask OnEnterAsync(IProcedureModule m)
        {
            _log.Info("InGame] async: loading scene...");
            await _sched.Delay(0.2f);
            _log.Info("InGame] async: scene ready");
        }
    }
}
