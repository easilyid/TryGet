using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleSystem 端到端测试。
    /// </summary>
    [TestFixture]
    public class ModuleSystemEndToEndTests
    {
        private struct PlayerSpawnedEvent { public int PlayerId; }
        private struct TickEvent { public int FrameIndex; }

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
                typeof(ILogger), typeof(ITimerModule), typeof(IPoolModule)
            };

            public bool InitCalled;
            public bool ShutdownCalled;
            public List<int> SpawnedPlayers = new List<int>();
            public List<int> TickedFrames = new List<int>();
            public int BulletsCreated;
            public int BulletsRecycled;
            public TimerHandle DelayedTimer;
            public bool DelayedTimerFired;

            private IModuleSystem _host;
            private ILogger _log;
            private ITimerModule _timer;
            private IPoolModule _pool;
            private IObjectPool<Bullet> _bulletPool;
            private int _frameCounter;

            public void OnInit(IModuleSystem host)
            {
                InitCalled = true;
                _host = host;
                _log = host.Get<ILogger>();
                _timer = host.Get<ITimerModule>();
                _pool = host.Get<IPoolModule>();

                _bulletPool = _pool.GetOrCreatePool<Bullet>(
                    factory: () => { BulletsCreated++; return new Bullet(); },
                    onReturn: b => { BulletsRecycled++; b.OwnerId = 0; b.Lifetime = 0; });

                host.EventModule.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
                host.EventModule.Subscribe<TickEvent>(OnTick);

                DelayedTimer = _timer.Schedule(0.5f, () => DelayedTimerFired = true);

                _log.Info("GameLoop initialized");
            }

            public void Update(float dt, float unscaledDt)
            {
                _frameCounter++;
                _host.EventModule.Publish(new TickEvent { FrameIndex = _frameCounter });

                if (_frameCounter == 2)
                    _host.EventModule.Publish(new PlayerSpawnedEvent { PlayerId = 42 });

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
                _host.EventModule.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
                _host.EventModule.Unsubscribe<TickEvent>(OnTick);
                _log.Info("GameLoop shutting down");
            }

            private void OnPlayerSpawned(PlayerSpawnedEvent evt) => SpawnedPlayers.Add(evt.PlayerId);
            private void OnTick(TickEvent evt) => TickedFrames.Add(evt.FrameIndex);
        }

        [Test]
        public void FullLifecycle_RegisterInitUpdateShutdown_AllModulesWorkTogether()
        {
            var host = new ModuleSystem();
            var log = new ConsoleLogger { CaptureToMemory = true };
            var timer = new TimerModule();
            var pool = new PoolModule();
            var game = new GameLoopModule();

            host.Register<IGameLoopModule>(game);
            host.Register<IPoolModule>(pool);
            host.Register<ITimerModule>(timer);
            host.Register<ILogger>(log);

            host.Initialize();

            Assert.IsTrue(host.IsInitialized);
            Assert.IsTrue(game.InitCalled);

            int initLogCount = log.GetCapturedEntries().Count;
            Assert.GreaterOrEqual(initLogCount, 1);

            host.Update(0.16f, 0.16f);
            Assert.AreEqual(new[] { 1 }, game.TickedFrames.ToArray());

            host.Update(0.16f, 0.16f);
            Assert.AreEqual(new[] { 1, 2 }, game.TickedFrames.ToArray());
            Assert.AreEqual(new[] { 42 }, game.SpawnedPlayers.ToArray());

            host.Update(0.16f, 0.16f);
            Assert.AreEqual(1, game.BulletsCreated);
            Assert.AreEqual(1, game.BulletsRecycled);

            host.Update(0.16f, 0.16f);
            Assert.IsTrue(game.DelayedTimerFired);
            Assert.AreEqual(0, timer.PendingCount);

            var bullet = pool.GetOrCreatePool<Bullet>(() => throw new Exception("不该再 new"))
                .Rent();
            Assert.IsNotNull(bullet);
            Assert.AreEqual(1, game.BulletsCreated);

            host.Shutdown();

            Assert.IsFalse(host.IsInitialized);
            Assert.IsTrue(game.ShutdownCalled);
            Assert.Throws<InvalidOperationException>(() => host.Update(0.16f, 0.16f));
        }

        [Test]
        public void FullLifecycle_ShutdownOrderIsReverseOfInit()
        {
            var shutdownLog = new List<string>();

            var host = new ModuleSystem();
            host.Register<ILogger>(new TracingLogger(shutdownLog));
            host.Register<ITimerModule>(new TracingTimerModule(shutdownLog));
            host.Register<IPoolModule>(new TracingPoolModule(shutdownLog));
            host.Register<IGameLoopModule>(new TracingGameLoopModule(shutdownLog));

            host.Initialize();
            host.Shutdown();

            int idxGame = shutdownLog.IndexOf("shutdown:GameLoop");
            int idxLog = shutdownLog.IndexOf("shutdown:Log");
            int idxTimer = shutdownLog.IndexOf("shutdown:Timer");
            int idxPool = shutdownLog.IndexOf("shutdown:Pool");

            Assert.Greater(idxGame, -1);
            Assert.Less(idxGame, idxLog);
            Assert.Less(idxGame, idxTimer);
            Assert.Less(idxGame, idxPool);
        }

        private class TracingLogger : ILogger
        {
            private readonly List<string> _log;
            public TracingLogger(List<string> log) { _log = log; }
            public int Priority => -1000;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public LogLevel MinimumLevel { get; set; }
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { _log.Add("shutdown:Log"); }
            public void Trace(string m) { } public void Debug(string m) { }
            public void Info(string m) { } public void Warn(string m) { }
            public void Error(string m) { } public void Error(string m, Exception e) { }
        }

        private class TracingTimerModule : ITimerModule
        {
            private readonly List<string> _log;
            public TracingTimerModule(List<string> log) { _log = log; }
            public int Priority => -500;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public int PendingCount => 0;
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { _log.Add("shutdown:Timer"); }
            public TimerHandle Schedule(float s, Action c) => default;
            public TimerHandle ScheduleUnscaled(float s, Action c) => default;
            public TimerHandle ScheduleRepeat(float s, Action c) => default;
            public bool Cancel(TimerHandle h) => false;
            public bool Pause(TimerHandle h) => false;
            public bool Resume(TimerHandle h) => false;
            public bool IsPaused(TimerHandle h) => false;
        }

        private class TracingPoolModule : IPoolModule
        {
            private readonly List<string> _log;
            public TracingPoolModule(List<string> log) { _log = log; }
            public int Priority => -500;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleSystem host) { }
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
                typeof(ILogger), typeof(ITimerModule), typeof(IPoolModule)
            };
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { _log.Add("shutdown:GameLoop"); }
        }
    }
}
