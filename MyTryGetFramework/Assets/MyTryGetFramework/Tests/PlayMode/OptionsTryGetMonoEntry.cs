using System;
using System.Collections.Generic;
using TryGet.Async;
using TryGet.Unity;

namespace TryGet.Tests.PlayMode
{
    public sealed class OptionsTryGetMonoEntry : TryGetMonoEntry
    {
        public OptionsLogger Logger { get; } = new OptionsLogger();
        public OptionsClock Clock { get; } = new OptionsClock();
        public OptionsScheduler Scheduler { get; } = new OptionsScheduler();
        public TrackingModule Module { get; } = new TrackingModule();
        public bool SetupCalled { get; private set; }
        public ILogger LoggerFromHost { get; private set; }
        public IClock ClockFromHost { get; private set; }
        public ITGTaskScheduler SchedulerFromHost { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override GameLauncherOptions Options => new GameLauncherOptions
        {
            Logger = Logger,
            Clock = Clock,
            Scheduler = Scheduler,
            MinimumLogLevel = LogLevel.Trace,
        };

        protected override void Setup(IModuleSystem host)
        {
            SetupCalled = true;
            LoggerFromHost = host.Get<ILogger>();
            ClockFromHost = host.Get<IClock>();
            SchedulerFromHost = host.Get<ITGTaskScheduler>();
            host.Register<ITrackingModule>(Module);
        }
    }

    public sealed class OptionsLogger : ILogger
    {
        public int Priority => -1000;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public LogLevel MinimumLevel { get; set; } = LogLevel.Warn;
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public void OnInit(IModuleSystem host) { InitCount++; }
        public void Shutdown() { ShutdownCount++; }
        public void Trace(string message) { }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(string message, Exception exception) { }
    }

    public sealed class OptionsClock : IClock
    {
        public int Priority => -900;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public float DeltaTime => 0f;
        public float UnscaledDeltaTime => 0f;
        public double ElapsedTime => 0.0;
        public double UnscaledElapsedTime => 0.0;
        public long FrameCount => 0L;
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public void OnInit(IModuleSystem host) { InitCount++; }
        public void Shutdown() { ShutdownCount++; }
    }

    public sealed class OptionsScheduler : ITGTaskScheduler
    {
        public int Priority => -150;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int UpdateCount { get; private set; }
        public void OnInit(IModuleSystem host) { InitCount++; }
        public void Shutdown() { ShutdownCount++; }
        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void FixedUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void Update(float deltaTime, float unscaledDeltaTime) { UpdateCount++; }
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
}
