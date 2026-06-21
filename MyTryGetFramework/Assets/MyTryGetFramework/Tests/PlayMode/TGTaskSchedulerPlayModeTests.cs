using System.Collections;
using NUnit.Framework;
using TryGet.Async;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// TGTaskScheduler 在真实 Unity 时间轴下的 PlayMode 验证。
    /// </summary>
    [TestFixture]
    public sealed class TGTaskSchedulerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Scheduler_UnscaledDelay_CompletesWhileScaledDelayFreezes_WhenTimeScaleZero()
        {
            var gameObject = new GameObject(nameof(Scheduler_UnscaledDelay_CompletesWhileScaledDelayFreezes_WhenTimeScaleZero));
            var destroyRequested = false;
            float originalTimeScale = Time.timeScale;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new SchedulerTimeScaleProbe();
                Time.timeScale = 0f;

                probe.RunScaledDelay(entry.Scheduler, 0.1f).Forget();
                probe.RunUnscaledDelay(entry.Scheduler, 0.1f).Forget();

                for (int i = 0; i < 180 && !probe.UnscaledDone; i++)
                    yield return null;

                Assert.That(probe.UnscaledDone, Is.True,
                    "scheduler.Delay(..., TimeMode.Unscaled) 应在 Time.timeScale=0 时随真实 Unity 帧推进完成。");
                Assert.That(probe.ScaledDone, Is.False,
                    "scheduler.Delay 默认 scaled 时间应在 Time.timeScale=0 时冻结。");

                Time.timeScale = originalTimeScale;

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry == null || !entry.HasHost, Is.True);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Scheduler_FixedUpdateDelay_CompletesThroughUnityFixedUpdate()
        {
            var gameObject = new GameObject(nameof(Scheduler_FixedUpdateDelay_CompletesThroughUnityFixedUpdate));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new SchedulerTimeScaleProbe();
                probe.RunFixedUpdateDelay(entry.Scheduler, Time.fixedDeltaTime * 2f).Forget();

                for (int i = 0; i < 60 && !probe.FixedDone; i++)
                    yield return new WaitForFixedUpdate();

                Assert.That(probe.FixedDone, Is.True,
                    "scheduler.Delay(..., FramePhase.FixedUpdate) 应由真实 Unity FixedUpdate 驱动完成。");

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
        public IEnumerator Scheduler_FixedUpdateWaitForFrames_CompletesInsideUnityFixedStep()
        {
            var gameObject = new GameObject(nameof(Scheduler_FixedUpdateWaitForFrames_CompletesInsideUnityFixedStep));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new SchedulerTimeScaleProbe();
                probe.RunFixedUpdateWaitForFrames(entry.Scheduler, 3).Forget();

                for (int i = 0; i < 30 && !probe.FixedFrameWaitDone; i++)
                    yield return new WaitForFixedUpdate();

                Assert.That(probe.FixedFrameWaitDone, Is.True,
                    "scheduler.WaitForFrames(..., FramePhase.FixedUpdate) 应由真实 Unity FixedUpdate phase 推进完成。");
                Assert.That(probe.FixedFrameWaitCompletedInsideFixedStep, Is.True,
                    "FixedUpdate phase 的 continuation 应在 Unity fixed step 内恢复，而不是在渲染 Update 中恢复。");

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

    public sealed class SchedulerTimeScaleProbe
    {
        public bool ScaledDone { get; private set; }
        public bool UnscaledDone { get; private set; }
        public bool FixedDone { get; private set; }
        public bool FixedFrameWaitDone { get; private set; }
        public bool FixedFrameWaitCompletedInsideFixedStep { get; private set; }

        public async TGTask RunScaledDelay(ITGTaskScheduler scheduler, float seconds)
        {
            await scheduler.Delay(seconds);
            ScaledDone = true;
        }

        public async TGTask RunUnscaledDelay(ITGTaskScheduler scheduler, float seconds)
        {
            await scheduler.Delay(seconds, TimeMode.Unscaled);
            UnscaledDone = true;
        }

        public async TGTask RunFixedUpdateDelay(ITGTaskScheduler scheduler, float seconds)
        {
            await scheduler.Delay(seconds, FramePhase.FixedUpdate);
            FixedDone = true;
        }

        public async TGTask RunFixedUpdateWaitForFrames(ITGTaskScheduler scheduler, int frames)
        {
            await scheduler.WaitForFrames(frames, FramePhase.FixedUpdate);
            FixedFrameWaitCompletedInsideFixedStep = Time.inFixedTimeStep;
            FixedFrameWaitDone = true;
        }
    }
}
