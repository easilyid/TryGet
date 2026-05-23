using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.3 完整 demo（design.md §12 Gate criteria）：
    /// 模拟"登录流程"——Boot → Login → InGame 三个 Procedure 协同。
    ///
    /// 串起 V0.2 + V0.3 全栈：
    /// - ModuleHost：根容器、生命周期编排
    /// - Common 三件套：Log / Timer / Pool（Pool 演示 Bullet 池化）
    /// - EntityWorld：玩法层 + System 调度
    /// - ProcedureModule：跨帧流程，Procedure 通过 m.Host.Get&lt;...&gt;() 拉其他 Module
    ///
    /// 这同时是一个端到端集成测试，验证整个 V0.3 栈协同无 regression。
    /// </summary>
    [TestFixture]
    public class LoginFlowDemoTests
    {
        private class PlayerAspect : Aspect
        {
            public string Username;
            public int Level;
        }

        /// <summary>"子弹"池化对象（POCO）。</summary>
        private class Bullet
        {
            public int OwnerId;
        }

        /// <summary>InGame 期间的 System：每帧让 Player 升级。</summary>
        private class PlayerLevelUpSystem : SystemBase
        {
            public int Ticks;

            protected override void OnCreate()
            {
                Query = Query.Create().WithAll<PlayerAspect>().Build();
            }

            protected override void Execute(IReadOnlyList<Entity> entities)
            {
                Ticks++;
                foreach (var e in entities)
                {
                    var p = e.GetAspect<PlayerAspect>();
                    if (p != null) p.Level++;
                }
            }
        }

        // —— 三个 Procedure ——

        private class BootProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            private ITimerModule _timer;
            private TimerHandle _bootTimer;
            private bool _readyToLogin;

            public BootProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("Boot.OnEnter");
                // 通过 m.Host 拉 ITimerModule 模拟"资源加载延迟 0.5s"
                _timer = m.Host.Get<ITimerModule>();
                _bootTimer = _timer.Schedule(0.5f, () => _readyToLogin = true);

                m.Host.Get<ILogModule>().Info("Boot: loading resources...");
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                if (_readyToLogin)
                {
                    _log.Add("Boot.Ready → Login");
                    m.TransitionTo("Login");
                }
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("Boot.OnExit");
                if (_bootTimer.IsValid) _timer.Cancel(_bootTimer);
            }
        }

        private class LoginProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            private int _ticks;

            public LoginProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("Login.OnEnter");
                m.Host.Get<ILogModule>().Info("Login: authenticating...");
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                _ticks++;
                if (_ticks >= 3)
                {
                    _log.Add($"Login.Authenticated (after {_ticks} ticks) → InGame");
                    m.TransitionTo("InGame");
                }
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("Login.OnExit");
            }
        }

        private class InGameProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            private IEntityWorld _world;
            private IPoolModule _pools;
            private IObjectPool<Bullet> _bulletPool;
            private Entity _player;

            public InGameProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("InGame.OnEnter");
                _world = m.Host.Get<IEntityWorld>();
                _pools = m.Host.Get<IPoolModule>();

                _player = _world.CreateEntity();
                _player.Attach(new PlayerAspect { Username = "hero", Level = 1 });

                _bulletPool = _pools.GetOrCreatePool<Bullet>(() => new Bullet(),
                    onReturn: b => b.OwnerId = 0);

                m.Host.Get<ILogModule>().Info("InGame: player spawned");
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                // 模拟"射击"：rent + return
                var b = _bulletPool.Rent();
                b.OwnerId = _player.Id.Index;
                _bulletPool.Return(b);
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("InGame.OnExit");
            }
        }

        [Test]
        public void FullLoginFlow_BootLoginInGame_AllPhasesExecuted()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            var logModule = new ConsoleLogModule { CaptureToMemory = true };
            host.Register<ILogModule>(logModule);
            host.Register<ITimerModule>(new TimerModule());
            host.Register<IPoolModule>(new PoolModule());
            host.Register<IEntityWorld>(new EntityWorld("Game"));
            var procedures = new ProcedureModule();
            host.Register<IProcedureModule>(procedures);

            host.Initialize();

            // 注册 InGame 期间的 System
            host.Get<IEntityWorld>().RegisterSystem(new PlayerLevelUpSystem(), Phase.Update);

            procedures.AddProcedure("Boot", new BootProcedure(log));
            procedures.AddProcedure("Login", new LoginProcedure(log));
            procedures.AddProcedure("InGame", new InGameProcedure(log));

            procedures.Start("Boot");

            // —— 帧驱动 ——
            host.Update(0.6f, 0.6f);       // Boot timer 触发 → Login
            host.Update(0.016f, 0.016f);   // Login tick 1
            host.Update(0.016f, 0.016f);   // Login tick 2
            host.Update(0.016f, 0.016f);   // Login tick 3 → InGame
            host.Update(0.016f, 0.016f);   // InGame tick 1
            host.Update(0.016f, 0.016f);   // InGame tick 2
            host.Update(0.016f, 0.016f);   // InGame tick 3

            // —— 验证 Procedure 全流程走过 ——
            Assert.AreEqual("InGame", procedures.CurrentState, "最终应处于 InGame");

            CollectionAssert.Contains(log, "Boot.OnEnter");
            CollectionAssert.Contains(log, "Boot.Ready → Login");
            CollectionAssert.Contains(log, "Boot.OnExit");
            CollectionAssert.Contains(log, "Login.OnEnter");
            CollectionAssert.Contains(log, "Login.OnExit");
            CollectionAssert.Contains(log, "InGame.OnEnter");

            // —— 验证 Log 写入 ——
            var captured = logModule.GetCapturedEntries();
            bool sawBoot = false, sawLogin = false, sawInGame = false;
            foreach (var e in captured)
            {
                if (e.message.Contains("Boot:")) sawBoot = true;
                if (e.message.Contains("Login:")) sawLogin = true;
                if (e.message.Contains("InGame:")) sawInGame = true;
            }
            Assert.IsTrue(sawBoot && sawLogin && sawInGame, "三个 Procedure 都应通过 ILogModule 写日志");

            // —— Shutdown 干净退出 ——
            host.Shutdown();

            Assert.IsFalse(host.IsInitialized);
            Assert.AreEqual(EntityWorldState.Shutdown, host.Get<IEntityWorld>().State);
            CollectionAssert.Contains(log, "InGame.OnExit");
        }

        [Test]
        public void ProcedureModule_HostInjection_AvailableInOnEnter()
        {
            // 验证 IProcedureModule.Host 在 OnEnter 内可用
            var host = new ModuleHost();
            host.Register<ILogModule>(new ConsoleLogModule());
            host.Register<ITimerModule>(new TimerModule());
            host.Register<IPoolModule>(new PoolModule());
            host.Register<IEntityWorld>(new EntityWorld("Test"));
            var pm = new ProcedureModule();
            host.Register<IProcedureModule>(pm);
            host.Initialize();

            IModuleHost capturedHost = null;
            var probe = new HostProbeProcedure(h => capturedHost = h);
            pm.AddProcedure("probe", probe);
            pm.Start("probe");

            Assert.AreSame(host, capturedHost, "ProcedureModule.Host 应是 OnInit 注入的 ModuleHost");

            host.Shutdown();
        }

        private class HostProbeProcedure : ProcedureBase
        {
            private readonly System.Action<IModuleHost> _capture;
            public HostProbeProcedure(System.Action<IModuleHost> capture) { _capture = capture; }
            public override void OnEnter(IProcedureModule m) => _capture(m.Host);
        }
    }
}
