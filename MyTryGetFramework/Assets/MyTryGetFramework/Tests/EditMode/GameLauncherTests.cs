using System;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    [TestFixture]
    public class GameLauncherTests
    {
        private struct GeneratedEvent
        {
            public int Value;
        }

        private interface IGeneratedModule : IModule { }

        private sealed class GeneratedModule : IGeneratedModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public bool Initialized { get; private set; }
            public void OnInit(IModuleSystem host) { Initialized = true; }
            public void Shutdown() { Initialized = false; }
        }

        private sealed class TestLogger : ILogger
        {
            public int Priority => -1000;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public LogLevel MinimumLevel { get; set; } = LogLevel.Warn;
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
            public void Trace(string message) { }
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private sealed class TestClock : IClock
        {
            public int Priority => -900;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public float DeltaTime => 0f;
            public float UnscaledDeltaTime => 0f;
            public double ElapsedTime => 0.0;
            public double UnscaledElapsedTime => 0.0;
            public long FrameCount => 0L;
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        private sealed class TestScheduler : ITGTaskScheduler
        {
            public int Priority => -150;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
            public void EarlyUpdate(float deltaTime, float unscaledDeltaTime) { }
            public void FixedUpdate(float deltaTime, float unscaledDeltaTime) { }
            public void Update(float deltaTime, float unscaledDeltaTime) { }
            public void LateUpdate(float deltaTime, float unscaledDeltaTime) { }
            public void EndOfFrame(float deltaTime, float unscaledDeltaTime) { }
            public TGTask Yield() => TGTask.CompletedTask;
            public TGTask Yield(FramePhase phase) => TGTask.CompletedTask;
            public TGTask Delay(float seconds) => TGTask.CompletedTask;
            public TGTask Delay(float seconds, FramePhase phase) => TGTask.CompletedTask;
            public TGTask Delay(float seconds, TimeMode timeMode) => TGTask.CompletedTask;
            public TGTask Delay(float seconds, FramePhase phase, TimeMode timeMode) => TGTask.CompletedTask;
            public TGTask WaitForFrames(int frameCount) => TGTask.CompletedTask;
            public TGTask WaitForFrames(int frameCount, FramePhase phase) => TGTask.CompletedTask;
            public TGTask DelayUntilPhase(FramePhase phase) => TGTask.CompletedTask;
            public TGTask Yield(TGCancelToken token) => TGTask.CompletedTask;
            public TGTask Yield(FramePhase phase, TGCancelToken token) => TGTask.CompletedTask;
            public TGTask Delay(float seconds, TGCancelToken token) => TGTask.CompletedTask;
            public TGTask Delay(float seconds, FramePhase phase, TimeMode timeMode, TGCancelToken token) => TGTask.CompletedTask;
            public TGTask WaitForFrames(int frameCount, TGCancelToken token) => TGTask.CompletedTask;
            public TGTask WaitForFrames(int frameCount, FramePhase phase, TGCancelToken token) => TGTask.CompletedTask;
        }

        private static int _generatedEventTotal;

        [SetUp]
        public void SetUp()
        {
            ModuleRegistry.ClearForTests();
            EventHandlerRegistry.ClearForTests();
            _generatedEventTotal = 0;
        }

        [TearDown]
        public void TearDown()
        {
            ModuleRegistry.ClearForTests();
            EventHandlerRegistry.ClearForTests();
            _generatedEventTotal = 0;
        }

        [Test]
        public void CreateHost_RegistersCoreBaselineServices()
        {
            var host = GameLauncher.CreateHost();

            Assert.IsNotNull(host.Get<ILogger>());
            Assert.IsNotNull(host.Get<IClock>());
            Assert.IsNotNull(host.Get<ITGTaskScheduler>());
        }

        [Test]
        public void CreateHost_UsesInjectedCoreServices()
        {
            var logger = new TestLogger();
            var clock = new TestClock();
            var scheduler = new TestScheduler();
            var options = new GameLauncherOptions
            {
                Logger = logger,
                Clock = clock,
                Scheduler = scheduler
            };

            var host = GameLauncher.CreateHost(options);

            Assert.AreSame(logger, host.Get<ILogger>());
            Assert.AreSame(clock, host.Get<IClock>());
            Assert.AreSame(scheduler, host.Get<ITGTaskScheduler>());
        }

        [Test]
        public void CreateHost_MinimumLogLevel_ConfiguresDefaultConsoleLogger()
        {
            var host = GameLauncher.CreateHost(new GameLauncherOptions
            {
                MinimumLogLevel = LogLevel.Trace
            });

            var logger = host.Get<ILogger>();

            Assert.IsInstanceOf<ConsoleLogger>(logger);
            Assert.AreEqual(LogLevel.Trace, logger.MinimumLevel);
        }

        [Test]
        public void CreateHost_InjectedLogger_IsNotOverriddenByMinimumLogLevel()
        {
            var logger = new TestLogger { MinimumLevel = LogLevel.Error };
            var host = GameLauncher.CreateHost(new GameLauncherOptions
            {
                Logger = logger,
                MinimumLogLevel = LogLevel.Trace
            });

            Assert.AreSame(logger, host.Get<ILogger>());
            Assert.AreEqual(LogLevel.Error, logger.MinimumLevel);
        }

        [Test]
        public void CreateHost_AppliesGeneratedModuleAndEventHandlerRegistries()
        {
            var generatedModule = new GeneratedModule();
            ModuleRegistry.RegisterWithMetadata(typeof(GeneratedModule), typeof(IGeneratedModule), "GeneratedTestAssembly");
            ModuleRegistry.Register(host => host.Register<IGeneratedModule>(generatedModule));

            EventHandlerRegistry.RegisterWithMetadata(
                "TryGet.Tests.GameLauncherTests.OnGeneratedEvent",
                typeof(GeneratedEvent),
                "GeneratedTestAssembly");
            EventHandlerRegistry.Register(bus => bus.Subscribe<GeneratedEvent>(OnGeneratedEvent));

            var host = GameLauncher.CreateHost();

            Assert.AreSame(generatedModule, host.Get<IGeneratedModule>());

            host.EventModule.Publish(new GeneratedEvent { Value = 7 });

            Assert.AreEqual(7, _generatedEventTotal);
        }

        [Test]
        public void CreateHost_WithOptions_StillAppliesGeneratedRegistries()
        {
            var generatedModule = new GeneratedModule();
            ModuleRegistry.RegisterWithMetadata(typeof(GeneratedModule), typeof(IGeneratedModule), "GeneratedTestAssembly");
            ModuleRegistry.Register(host => host.Register<IGeneratedModule>(generatedModule));

            EventHandlerRegistry.RegisterWithMetadata(
                "TryGet.Tests.GameLauncherTests.OnGeneratedEvent",
                typeof(GeneratedEvent),
                "GeneratedTestAssembly");
            EventHandlerRegistry.Register(bus => bus.Subscribe<GeneratedEvent>(OnGeneratedEvent));

            var host = GameLauncher.CreateHost(new GameLauncherOptions
            {
                Logger = new TestLogger(),
                Clock = new TestClock(),
                Scheduler = new TestScheduler()
            });

            Assert.AreSame(generatedModule, host.Get<IGeneratedModule>());

            host.EventModule.Publish(new GeneratedEvent { Value = 11 });

            Assert.AreEqual(11, _generatedEventTotal);
        }

        private static void OnGeneratedEvent(GeneratedEvent evt)
        {
            _generatedEventTotal += evt.Value;
        }
    }
}
