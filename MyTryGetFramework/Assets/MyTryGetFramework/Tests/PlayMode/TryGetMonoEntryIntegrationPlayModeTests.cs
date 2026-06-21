using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Async;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryIntegrationPlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_DrivesDefaultSchedulerThroughUnityFrames()
        {
            var gameObject = new GameObject("TryGetMonoEntry Scheduler Integration Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerIntegrationTryGetMonoEntry>();
                var module = entry.Module;

                for (int i = 0; i < 20 && !module.AllContinuationsResumed; i++)
                    yield return null;

                Assert.That(module.SchedulerFromHost, Is.TypeOf<TGTaskScheduler>());
                Assert.That(module.YieldResumed, Is.True);
                Assert.That(module.LateUpdateResumed, Is.True);
                Assert.That(module.WaitForFramesResumed, Is.True);
                Assert.That(module.RunnerTask.IsCompleted, Is.True);

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(module.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_DrivesRegisteredTimerModuleThroughUnityUpdate()
        {
            var gameObject = new GameObject("TryGetMonoEntry Timer Integration Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TimerIntegrationTryGetMonoEntry>();
                var module = entry.Module;

                for (int i = 0; i < 60 && module.FiredCount == 0; i++)
                    yield return null;

                Assert.That(module.TimerFromHost, Is.SameAs(entry.Timer));
                Assert.That(module.FiredCount, Is.EqualTo(1));
                Assert.That(entry.Timer.PendingCount, Is.EqualTo(0));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(module.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_DrivesSchedulerEndOfFrameContinuationThroughUnityCoroutine()
        {
            var gameObject = new GameObject("TryGetMonoEntry Scheduler EndOfFrame Integration Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerEndOfFrameTryGetMonoEntry>();
                var module = entry.Module;

                for (int i = 0; i < 10 && !module.EndOfFrameResumed; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(module.SchedulerFromHost, Is.TypeOf<TGTaskScheduler>());
                Assert.That(module.EndOfFrameResumed, Is.True,
                    "默认 Scheduler 的 EndOfFrame phase continuation 应由 TryGetMonoEntry 的真实 WaitForEndOfFrame coroutine 恢复。");
                Assert.That(module.RunnerTask.IsCompleted, Is.True);

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(module.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    public interface ISchedulerIntegrationModule : IModule
    {
    }

    public sealed class SchedulerIntegrationModule : ISchedulerIntegrationModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public ITGTaskScheduler SchedulerFromHost { get; private set; }
        public TGTask RunnerTask { get; private set; }
        public bool YieldResumed { get; private set; }
        public bool LateUpdateResumed { get; private set; }
        public bool WaitForFramesResumed { get; private set; }
        public int ShutdownCount { get; private set; }
        public bool AllContinuationsResumed => YieldResumed && LateUpdateResumed && WaitForFramesResumed;

        public void OnInit(IModuleSystem host)
        {
            SchedulerFromHost = host.Get<ITGTaskScheduler>();
            RunnerTask = RunSchedulerContinuations();
            RunnerTask.Forget();
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }

        private async TGTask RunSchedulerContinuations()
        {
            await SchedulerFromHost.Yield();
            YieldResumed = true;

            await SchedulerFromHost.DelayUntilPhase(FramePhase.LateUpdate);
            LateUpdateResumed = true;

            await SchedulerFromHost.WaitForFrames(2);
            WaitForFramesResumed = true;
        }
    }

    public sealed class SchedulerIntegrationTryGetMonoEntry : TryGetMonoEntry
    {
        public SchedulerIntegrationModule Module { get; } = new SchedulerIntegrationModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<ISchedulerIntegrationModule>(Module);
        }
    }

    public interface ISchedulerEndOfFrameModule : IModule
    {
    }

    public sealed class SchedulerEndOfFrameModule : ISchedulerEndOfFrameModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public ITGTaskScheduler SchedulerFromHost { get; private set; }
        public TGTask RunnerTask { get; private set; }
        public bool EndOfFrameResumed { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            SchedulerFromHost = host.Get<ITGTaskScheduler>();
            RunnerTask = RunEndOfFrameContinuation();
            RunnerTask.Forget();
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }

        private async TGTask RunEndOfFrameContinuation()
        {
            await SchedulerFromHost.DelayUntilPhase(FramePhase.EndOfFrame);
            EndOfFrameResumed = true;
        }
    }

    public sealed class SchedulerEndOfFrameTryGetMonoEntry : TryGetMonoEntry
    {
        public SchedulerEndOfFrameModule Module { get; } = new SchedulerEndOfFrameModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<ISchedulerEndOfFrameModule>(Module);
        }
    }

    public interface ITimerIntegrationModule : IModule
    {
    }

    public sealed class TimerIntegrationModule : ITimerIntegrationModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(ITimerModule) };
        public ITimerModule TimerFromHost { get; private set; }
        public int FiredCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            TimerFromHost = host.Get<ITimerModule>();
            TimerFromHost.Schedule(0.01f, () => FiredCount++);
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class TimerIntegrationTryGetMonoEntry : TryGetMonoEntry
    {
        public TimerModule Timer { get; } = new TimerModule();
        public TimerIntegrationModule Module { get; } = new TimerIntegrationModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<ITimerModule>(Timer);
            host.Register<ITimerIntegrationModule>(Module);
        }
    }
}
