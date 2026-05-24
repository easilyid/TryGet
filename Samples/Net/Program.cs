using System;
using TryGet;
using TryGet.Async;

namespace TryGet.Samples.Net
{
    /// <summary>
    /// V0.7 Iter 3 — Samples/Net 入口重构。
    ///
    /// 与 V0.6 Iter 10 版本对比：
    /// - 主循环 + Module 注册 + 异常处理 全部归并到 <see cref="Entry.Run"/>
    /// - Program.Main 减为 ~15 行业务逻辑
    /// - Logger 升级到 V0.7 <see cref="ILogger"/>（替代 V0.6 <see cref="ILogModule"/>）
    /// - <see cref="IClock"/> 新增自动注册（Bootstrap 默认）— sample 内未直接使用，预留 V0.8+ 流程参考
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            return Entry.Run(
                setup: host =>
                {
                    host.Register<ITimerModule>(new TimerModule());
                    var proc = new ProcedureModule();
                    host.Register<IProcedureModule>(proc);

                    // 业务 Procedure 在 setup 阶段就 Add（Bootstrap.Initialize 内会被 OnInit）
                    var log = host.Get<ILogger>();
                    var sched = host.Get<ITGTaskScheduler>();
                    proc.AddProcedure("boot", new BootProcedure(log, sched));
                    proc.AddProcedure("login", new LoginProcedure(log, sched));
                    proc.AddProcedure("ingame", new InGameProcedure(log, sched));
                },
                mainAsync: RunMainAsync,
                options: new BootstrapOptions { MinimumLogLevel = LogLevel.Debug },
                timeoutMs: 10_000);
        }

        private static async TGTask RunMainAsync(IModuleHost host)
        {
            var log = host.Get<ILogger>();
            var sched = host.Get<ITGTaskScheduler>();
            var proc = host.Get<IProcedureModule>();

            log.Info("=== MainAsync start ===");

            proc.Start("boot");
            await WaitForEnter(proc, sched);

            proc.TransitionTo("login");
            await WaitForEnter(proc, sched);

            proc.TransitionTo("ingame");
            await WaitForEnter(proc, sched);

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
        private readonly ILogger _log;
        private readonly ITGTaskScheduler _sched;

        public BootProcedure(ILogger log, ITGTaskScheduler sched) { _log = log; _sched = sched; }

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
        private readonly ILogger _log;
        private readonly ITGTaskScheduler _sched;

        public LoginProcedure(ILogger log, ITGTaskScheduler sched) { _log = log; _sched = sched; }

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
        private readonly ILogger _log;
        private readonly ITGTaskScheduler _sched;

        public InGameProcedure(ILogger log, ITGTaskScheduler sched) { _log = log; _sched = sched; }

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
