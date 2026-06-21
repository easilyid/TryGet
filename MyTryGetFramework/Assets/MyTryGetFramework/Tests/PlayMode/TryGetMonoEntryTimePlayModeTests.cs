using System.Collections;
using NUnit.Framework;
using TryGet.Async;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryTimePlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_ForwardsUnityTimeValuesToFrameModules()
        {
            var gameObject = new GameObject("TryGetMonoEntry Time Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();
                var module = entry.Module;

                for (int i = 0; i < 10 && (module.EarlyUpdateCount == 0 || module.UpdateCount == 0 || module.LateUpdateCount == 0); i++)
                    yield return null;

                Assert.That(module.EarlyUpdateReceivedUnityTime, Is.True);
                Assert.That(module.UpdateReceivedUnityTime, Is.True);
                Assert.That(module.LateUpdateReceivedUnityTime, Is.True);

                yield return new WaitForFixedUpdate();
                for (int i = 0; i < 5 && module.FixedUpdateCount == 0; i++)
                    yield return null;

                Assert.That(module.FixedUpdateReceivedUnityTime, Is.True);

                TryGetMonoEntryPlayModeTests.DriveEndOfFrameCoroutine(entry);
                Assert.That(module.EndOfFrameReceivedUnityTime, Is.True);

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_DefaultSchedulerFrameCountTracksRenderedUnityFrames()
        {
            var gameObject = new GameObject("TryGetMonoEntry Scheduler Frame Count Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.ExposedHost.TryGet<ITGTaskScheduler>(out var scheduler), Is.True);
                Assert.That(scheduler, Is.TypeOf<TGTaskScheduler>());
                var defaultScheduler = (TGTaskScheduler)scheduler;

                long schedulerFrameBefore = defaultScheduler.FrameCount;
                int unityFrameBefore = Time.frameCount;

                for (int i = 0; i < 6; i++)
                    yield return null;

                long schedulerFrameDelta = defaultScheduler.FrameCount - schedulerFrameBefore;
                int unityFrameDelta = Time.frameCount - unityFrameBefore;

                Assert.That(schedulerFrameDelta, Is.GreaterThanOrEqualTo(5),
                    "默认 Scheduler 的 FrameCount 应随真实 Unity 渲染帧持续推进。");
                Assert.That(schedulerFrameDelta, Is.LessThanOrEqualTo(unityFrameDelta + 1),
                    "同一 Unity 帧内的 Fixed/Early/Update/Late/EndOfFrame 组合不应让 FrameCount 明显超计。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
