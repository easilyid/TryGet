using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleHost 端到端测试。V0.2 Gate criteria 之一：
    /// "至少一个完整的'注册 → 启动 → 关闭'端到端测试"。
    ///
    /// 模拟一个最小可玩流程：
    /// - 注册 Log/Timer/Pool 三件套 + 业务 GameLoopModule
    /// - GameLoopModule 在 OnInit 中拉依赖（host.Get）、注册 timer、订阅事件
    /// - 多帧 Update 推进，验证 timer 触发、事件投递、池化复用
    /// - Shutdown 反向卸载，验证所有 Module 干净退出
    /// </summary>
    [TestFixture]
    public class ModuleHostEndToEndTests
    {
        // —— 业务事件 ——
        private struct PlayerSpawnedEvent { public int PlayerId; }
        private struct TickEvent { public int FrameIndex; }

        // —— 业务 Module：依赖 Log/Timer/Pool + EventBus ——
        private interface IGameLoopModule : IModule { }

        private class Bullet
        {
            public int OwnerId;
            public float Lifetime;
        }

        private class GameLoopModule : IGameLoopModule, IUpdateModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => new[]
            {
                typeof(ILogModule), typeof(ITimerModule), typeof(IPoolModule)
            };

            // 验收用记录
            public bool InitCalled;
            public bool ShutdownCalled;
            public List<int> SpawnedPlayers = new List<int>();
            public List<int> TickedFrames = new List<int>();
            public int BulletsCreated;
            public int BulletsRecycled;
            public TimerHandle DelayedTimer;
            public bool DelayedTimerFired;

            private IModuleHost _host;
            private ILogModule _log;
            private ITimerModule _timer;
            private IPoolModule _pool;
            private IObjectPool<Bullet> _bulletPool;
            private int _frameCounter;

            public void OnInit(IModuleHost host)
            {
                InitCalled = true;
                _host = host;
                _log = host.Get<ILogModule>();
                _timer = host.Get<ITimerModule>();
                _pool = host.Get<IPoolModule>();

                _bulletPool = _pool.GetOrCreatePool<Bullet>(
                    factory: () => { BulletsCreated++; return new Bullet(); },
                    onReturn: b => { BulletsRecycled++; b.OwnerId = 0; b.Lifetime = 0; });

                host.EventBus.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
                host.EventBus.Subscribe<TickEvent>(OnTick);

                DelayedTimer = _timer.Schedule(0.5f, () => DelayedTimerFired = true);

                _log.Info("GameLoop initialized");
            }

            public void Update(float dt, float unscaledDt)
            {
                _frameCounter++;
                _host.EventBus.Publish(new TickEvent { FrameIndex = _frameCounter });

                // 第 2 帧发布 spawn 事件
                if (_frameCounter == 2)
                    _host.EventBus.Publish(new PlayerSpawnedEvent { PlayerId = 42 });

                // 第 3 帧借/还子弹
                if (_frameCounter == 3)
                {
                    var b = _bulletPool.Rent();
                    b.OwnerId = 99;
                    b.Lifetime = 1.5f;
                    _bulletPool.Return(b);
                }
            }

            public void Shutdown()
            {
                ShutdownCalled = true;
                _host.EventBus.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
                _host.EventBus.Unsubscribe<TickEvent>(OnTick);
                _log.Info("GameLoop shutting down");
            }

            private void OnPlayerSpawned(PlayerSpawnedEvent evt) => SpawnedPlayers.Add(evt.PlayerId);
            private void OnTick(TickEvent evt) => TickedFrames.Add(evt.FrameIndex);
        }

        [Test]
        public void FullLifecycle_RegisterInitUpdateShutdown_AllModulesWorkTogether()
        {
            // —— Bootstrap ——
            var host = new ModuleHost();
            var log = new ConsoleLogModule { CaptureToMemory = true };
            var timer = new TimerModule();
            var pool = new PoolModule();
            var game = new GameLoopModule();

            // 故意打乱注册顺序，验证拓扑排序：GameLoop 依赖三件套
            host.Register<IGameLoopModule>(game);
            host.Register<IPoolModule>(pool);
            host.Register<ITimerModule>(timer);
            host.Register<ILogModule>(log);

            // —— Initialize ——
            host.Initialize();

            Assert.IsTrue(host.IsInitialized);
            Assert.IsTrue(game.InitCalled, "GameLoop 应在 OnInit 完成依赖注入");

            // GameLoop OnInit 已写过 1 条日志
            int initLogCount = log.GetCapturedEntries().Count;
            Assert.GreaterOrEqual(initLogCount, 1, "OnInit 期间应已写入日志");

            // —— Frame 1：Tick 事件 ——
            host.Update(0.16f, 0.16f);
            Assert.AreEqual(new[] { 1 }, game.TickedFrames.ToArray());

            // —— Frame 2：Tick + PlayerSpawn 事件 ——
            host.Update(0.16f, 0.16f);
            Assert.AreEqual(new[] { 1, 2 }, game.TickedFrames.ToArray());
            Assert.AreEqual(new[] { 42 }, game.SpawnedPlayers.ToArray(), "Spawn 事件投递成功");

            // —— Frame 3：池借/还，验证复用 ——
            host.Update(0.16f, 0.16f);
            Assert.AreEqual(1, game.BulletsCreated, "第一次 Rent 触发 factory");
            Assert.AreEqual(1, game.BulletsRecycled, "Return 触发 onReturn 重置");

            // —— Frame 4：累计 0.64s，超过 0.5s timer 应触发 ——
            host.Update(0.16f, 0.16f);
            Assert.IsTrue(game.DelayedTimerFired, "Delayed timer 应在累计>0.5s 时触发");
            Assert.AreEqual(0, timer.PendingCount, "Timer 触发后从 pending 移除");

            // —— Frame 5：再借子弹应复用 idle 实例（factory 不再调用）——
            // 手动验证池复用
            var bullet = pool.GetOrCreatePool<Bullet>(() => throw new Exception("不该再 new"))
                .Rent();
            Assert.IsNotNull(bullet);
            Assert.AreEqual(1, game.BulletsCreated, "池里有 idle 实例，不应触发 factory");

            // —— Shutdown ——
            host.Shutdown();

            Assert.IsFalse(host.IsInitialized);
            Assert.IsTrue(game.ShutdownCalled, "GameLoop.Shutdown 被调用");

            // Shutdown 后 host 不接受 Update
            Assert.Throws<InvalidOperationException>(() => host.Update(0.16f, 0.16f));
        }

        [Test]
        public void FullLifecycle_ShutdownOrderIsReverseOfInit()
        {
            // 验证 Shutdown 倒序：GameLoop 依赖 Log/Timer/Pool，
            // 所以 Init 序：Log/Timer/Pool → GameLoop；Shutdown 序：GameLoop → Pool/Timer/Log
            var shutdownLog = new List<string>();

            var host = new ModuleHost();
            host.Register<ILogModule>(new TracingLogModule(shutdownLog));
            host.Register<ITimerModule>(new TracingTimerModule(shutdownLog));
            host.Register<IPoolModule>(new TracingPoolModule(shutdownLog));
            host.Register<IGameLoopModule>(new TracingGameLoopModule(shutdownLog));

            host.Initialize();
            host.Shutdown();

            // GameLoop 一定在 Pool/Timer/Log 之前 Shutdown
            int idxGame = shutdownLog.IndexOf("shutdown:GameLoop");
            int idxLog = shutdownLog.IndexOf("shutdown:Log");
            int idxTimer = shutdownLog.IndexOf("shutdown:Timer");
            int idxPool = shutdownLog.IndexOf("shutdown:Pool");

            Assert.Greater(idxGame, -1);
            Assert.Less(idxGame, idxLog, "GameLoop 应在 Log 之前 Shutdown");
            Assert.Less(idxGame, idxTimer, "GameLoop 应在 Timer 之前 Shutdown");
            Assert.Less(idxGame, idxPool, "GameLoop 应在 Pool 之前 Shutdown");
        }

        // —— 追踪 Shutdown 顺序的简化 Module ——

        private class TracingLogModule : ILogModule
        {
            private readonly List<string> _log;
            public TracingLogModule(List<string> log) { _log = log; }
            public int Priority => -1000;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public LogLevel MinimumLevel { get; set; }
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { _log.Add("shutdown:Log"); }
            public void Debug(string m) { } public void Info(string m) { }
            public void Warn(string m) { } public void Error(string m) { }
            public void Error(string m, Exception e) { }
        }

        private class TracingTimerModule : ITimerModule
        {
            private readonly List<string> _log;
            public TracingTimerModule(List<string> log) { _log = log; }
            public int Priority => -500;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public int PendingCount => 0;
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { _log.Add("shutdown:Timer"); }
            public TimerHandle Schedule(float s, Action c) => default;
            public TimerHandle ScheduleUnscaled(float s, Action c) => default;
            public bool Cancel(TimerHandle h) => false;
        }

        private class TracingPoolModule : IPoolModule
        {
            private readonly List<string> _log;
            public TracingPoolModule(List<string> log) { _log = log; }
            public int Priority => -500;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { _log.Add("shutdown:Pool"); }
            public IObjectPool<T> GetOrCreatePool<T>(Func<T> f, Action<T> r = null, int i = 0) where T : class => null;
            public bool DestroyPool<T>() where T : class => false;
        }

        private class TracingGameLoopModule : IGameLoopModule
        {
            private readonly List<string> _log;
            public TracingGameLoopModule(List<string> log) { _log = log; }
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => new[]
            {
                typeof(ILogModule), typeof(ITimerModule), typeof(IPoolModule)
            };
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { _log.Add("shutdown:GameLoop"); }
        }
    }
}
