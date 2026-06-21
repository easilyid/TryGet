using System;
using System.Collections;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// TimerModule 在真实 Unity 帧循环下的端到端验证（区别于 EditMode 手动累计 deltaTime）。
    ///
    /// 覆盖用户真实接入定时器的高频路径：
    /// - ScheduleRepeat 在真实帧下周期性多次触发，Cancel 后停止；
    /// - Pause 冻结倒计时、Resume 后继续直至触发；
    /// - 真实游戏暂停场景：<c>Time.timeScale = 0</c> 时 scaled 定时器冻结、unscaled 定时器照常触发。
    /// </summary>
    [TestFixture]
    public sealed class TimerModulePlayModeTests
    {
        [UnityTest]
        public IEnumerator ScheduleRepeat_FiresMultipleTimesAcrossFrames_CancelStops()
        {
            var gameObject = new GameObject(nameof(ScheduleRepeat_FiresMultipleTimesAcrossFrames_CancelStops));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TimerHostEntry>();
                yield return null;

                var timer = entry.Timer;
                var counter = new TimerFireCounter();
                var handle = timer.ScheduleRepeat(0.05f, counter.Increment);

                for (int i = 0; i < 180 && counter.Count < 3; i++)
                    yield return null;
                Assert.That(counter.Count, Is.GreaterThanOrEqualTo(3), "周期定时器应在真实帧下多次触发");

                int countAtCancel = counter.Count;
                Assert.That(timer.Cancel(handle), Is.True);
                for (int i = 0; i < 30; i++)
                    yield return null;
                Assert.That(counter.Count, Is.EqualTo(countAtCancel), "取消后不应再触发");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Pause_FreezesScheduledTimer_ResumeAllowsItToFire()
        {
            var gameObject = new GameObject(nameof(Pause_FreezesScheduledTimer_ResumeAllowsItToFire));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TimerHostEntry>();
                yield return null;

                var timer = entry.Timer;
                var counter = new TimerFireCounter();
                var handle = timer.Schedule(0.2f, counter.Increment);

                Assert.That(timer.Pause(handle), Is.True);
                Assert.That(timer.IsPaused(handle), Is.True);

                for (int i = 0; i < 60; i++) // 远超 0.2s 的真实时间
                    yield return null;
                Assert.That(counter.Count, Is.EqualTo(0), "暂停期间倒计时冻结，不应触发");

                Assert.That(timer.Resume(handle), Is.True);
                Assert.That(timer.IsPaused(handle), Is.False);

                for (int i = 0; i < 180 && counter.Count == 0; i++)
                    yield return null;
                Assert.That(counter.Count, Is.EqualTo(1), "恢复后应继续倒计时并触发一次");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_Destroy_ClearsPendingTimers_BeforeTheyFire()
        {
            var gameObject = new GameObject(nameof(TryGetMonoEntry_Destroy_ClearsPendingTimers_BeforeTheyFire));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TimerHostEntry>();
                yield return null;

                var timer = entry.Timer;
                var counter = new TimerFireCounter();
                timer.Schedule(10f, counter.Increment);

                Assert.That(timer.PendingCount, Is.EqualTo(1));
                Assert.That(counter.Count, Is.EqualTo(0));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
                Assert.That(timer.PendingCount, Is.EqualTo(0),
                    "TryGetMonoEntry.OnDestroy 应 Shutdown TimerModule，清空 pending timers。");

                for (int i = 0; i < 5; i++)
                    yield return null;

                Assert.That(counter.Count, Is.EqualTo(0),
                    "Entry 销毁后旧 timer 不应在后续真实 Unity 帧中触发。");
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_SceneUnload_ClearsPendingTimers_BeforeTheyFire()
        {
            var scene = SceneManager.CreateScene("TryGet_TimerUnload_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(TryGetMonoEntry_SceneUnload_ClearsPendingTimers_BeforeTheyFire));
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<TimerHostEntry>();
            yield return null;

            var timer = entry.Timer;
            var counter = new TimerFireCounter();
            timer.Schedule(10f, counter.Increment);

            Assert.That(timer.PendingCount, Is.EqualTo(1));
            Assert.That(counter.Count, Is.EqualTo(0));

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(timer.PendingCount, Is.EqualTo(0),
                "非持久 TimerHostEntry 所属场景卸载时，应通过 OnDestroy/Shutdown 清空 pending timers。");

            for (int i = 0; i < 5; i++)
                yield return null;

            Assert.That(counter.Count, Is.EqualTo(0),
                "场景卸载后旧 timer 不应在后续真实 Unity 帧中触发。");
        }

        [UnityTest]
        public IEnumerator PersistentTryGetMonoEntry_SceneUnload_KeepsDrivingPendingTimers()
        {
            var scene = SceneManager.CreateScene("TryGet_TimerPersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(PersistentTryGetMonoEntry_SceneUnload_KeepsDrivingPendingTimers));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentTimerHostEntry>();
                yield return null;

                var timer = entry.Timer;
                var counter = new TimerFireCounter();
                timer.Schedule(0.05f, counter.Increment);

                Assert.That(timer.PendingCount, Is.EqualTo(1));
                Assert.That(counter.Count, Is.EqualTo(0));
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(entry.HasHost, Is.True);
                Assert.That(timer.PendingCount, Is.EqualTo(1),
                    "持久 TimerHostEntry 卸载原场景时不应 Shutdown TimerModule。");

                for (int i = 0; i < 120 && counter.Count == 0; i++)
                    yield return null;

                Assert.That(counter.Count, Is.EqualTo(1),
                    "持久 TimerHostEntry 卸载原场景后，pending timer 应继续由真实 Unity Update 驱动触发。");
                Assert.That(timer.PendingCount, Is.EqualTo(0));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry == null || !entry.HasHost, Is.True);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ScheduleUnscaled_FiresWhileScaledFrozen_WhenTimeScaleZero()
        {
            var gameObject = new GameObject(nameof(ScheduleUnscaled_FiresWhileScaledFrozen_WhenTimeScaleZero));
            var destroyRequested = false;
            float originalTimeScale = Time.timeScale;

            try
            {
                var entry = gameObject.AddComponent<TimerHostEntry>();
                yield return null;

                var timer = entry.Timer;
                var scaledCounter = new TimerFireCounter();
                var unscaledCounter = new TimerFireCounter();

                Time.timeScale = 0f; // 模拟游戏暂停
                timer.Schedule(0.1f, scaledCounter.Increment);
                timer.ScheduleUnscaled(0.1f, unscaledCounter.Increment);

                for (int i = 0; i < 180 && unscaledCounter.Count == 0; i++)
                    yield return null;

                Assert.That(unscaledCounter.Count, Is.GreaterThanOrEqualTo(1),
                    "unscaled 定时器在 Time.timeScale=0（游戏暂停）下应照常触发");
                Assert.That(scaledCounter.Count, Is.EqualTo(0),
                    "scaled 定时器在 Time.timeScale=0 下应冻结，不触发");

                Time.timeScale = originalTimeScale;

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                Time.timeScale = originalTimeScale; // 兜底恢复，避免污染其它测试
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TimerCallbacks_PlayModeHostTimer_ExceptionDoesNotStopOtherDueTimers()
        {
            var gameObject = new GameObject(nameof(TimerCallbacks_PlayModeHostTimer_ExceptionDoesNotStopOtherDueTimers));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TimerHostEntry>();
                yield return null;

                var timer = entry.Timer;
                var counter = new TimerFireCounter();
                var throwingCounter = new TimerFireCounter();

                timer.Schedule(0f, () =>
                {
                    throwingCounter.Increment();
                    throw new InvalidOperationException("timer boom");
                });
                timer.Schedule(0f, counter.Increment);

                var timerUpdate = (IUpdateModule)timer;
                var ex = Assert.Throws<AggregateException>(() => timerUpdate.Update(0f, 0f));

                Assert.That(ex.InnerExceptions, Has.Count.EqualTo(1));
                Assert.That(ex.InnerExceptions[0], Is.TypeOf<InvalidOperationException>());
                Assert.That(ex.InnerExceptions[0].Message, Is.EqualTo("timer boom"));
                Assert.That(throwingCounter.Count, Is.EqualTo(1));
                Assert.That(counter.Count, Is.EqualTo(1),
                    "同一批 PlayMode host timer 中，一个 callback 抛异常不应阻止其它到期 timer callback 执行。");
                Assert.That(timer.PendingCount, Is.EqualTo(0));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry == null || !entry.HasHost, Is.True);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    /// <summary>注册一个 <see cref="TimerModule"/> 的入口；测试在 Awake 完成后通过 <see cref="Timer"/> 调度定时器。</summary>
    public sealed class TimerHostEntry : TryGetMonoEntry
    {
        public ITimerModule Timer { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            var timer = new TimerModule();
            host.Register<ITimerModule>(timer);
            Timer = timer;
        }
    }

    public sealed class PersistentTimerHostEntry : TryGetMonoEntry
    {
        public ITimerModule Timer { get; private set; }
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            var timer = new TimerModule();
            host.Register<ITimerModule>(timer);
            Timer = timer;
        }
    }

    /// <summary>定时器触发计数器（作为 Action 回调目标，避免在迭代器测试体内用闭包捕获状态）。</summary>
    public sealed class TimerFireCounter
    {
        public int Count { get; private set; }
        public void Increment() => Count++;
    }
}
