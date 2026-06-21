using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryPlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_BootstrapsHost_DrivesAllFramePhases_AndShutsDown()
        {
            var gameObject = new GameObject("TryGetMonoEntry PlayMode Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();

                yield return null;

                var module = entry.Module;
                Assert.That(entry.SetupCalled, Is.True);
                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.HostWasInitializedDuringSetup, Is.False);
                Assert.That(entry.HostWasInitializedAfterSetup, Is.True);
                Assert.That(module, Is.Not.Null);
                Assert.That(module.InitCount, Is.EqualTo(1));
                Assert.That(module.ObservedHost, Is.SameAs(entry.ExposedHost));
                Assert.That(entry.ExposedHost.TryGet<ITrackingModule>(out var registered), Is.True);
                Assert.That(registered, Is.SameAs(module));

                for (int i = 0; i < 10 && (module.EarlyUpdateCount == 0 || module.UpdateCount == 0 || module.LateUpdateCount == 0); i++)
                    yield return null;

                Assert.That(module.EarlyUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(module.UpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(module.LateUpdateCount, Is.GreaterThanOrEqualTo(1));
                DriveEndOfFrameCoroutine(entry);
                Assert.That(module.EndOfFrameCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(module.FirstEarlyUpdateOrder, Is.LessThan(module.FirstUpdateOrder));

                yield return new WaitForFixedUpdate();
                for (int i = 0; i < 5 && module.FixedUpdateCount == 0; i++)
                    yield return null;

                Assert.That(module.FixedUpdateCount, Is.GreaterThanOrEqualTo(1));

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
        public IEnumerator TryGetMonoEntry_DrivesRealUnityFramePhasesInOrder()
        {
            var gameObject = new GameObject("TryGetMonoEntry Real Frame Phase Order Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();
                var module = entry.Module;

                yield return new WaitForEndOfFrame();
                module.ResetPhaseOrder();

                for (int i = 0; i < 10 && module.FirstEndOfFrameOrder == 0; i++)
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }

                Assert.That(module.FirstEarlyUpdateOrder, Is.GreaterThan(0));
                Assert.That(module.FirstUpdateOrder, Is.GreaterThan(0));
                Assert.That(module.FirstLateUpdateOrder, Is.GreaterThan(0));
                Assert.That(module.FirstEndOfFrameOrder, Is.GreaterThan(0));
                Assert.That(module.FirstEarlyUpdateOrder, Is.LessThan(module.FirstUpdateOrder),
                    "TryGetMonoEntry.Update should drive framework EarlyUpdate before Update in the same real Unity frame.");
                Assert.That(module.FirstUpdateOrder, Is.LessThan(module.FirstLateUpdateOrder),
                    "TryGetMonoEntry should drive framework Update before Unity LateUpdate reaches framework LateUpdate.");
                Assert.That(module.FirstLateUpdateOrder, Is.LessThan(module.FirstEndOfFrameOrder),
                    "TryGetMonoEntry should drive framework EndOfFrame from the real WaitForEndOfFrame coroutine after LateUpdate.");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(module.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry == null || !entry.HasHost, Is.True);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_DrivesEndOfFrameThroughUnityCoroutine()
        {
            var gameObject = new GameObject("TryGetMonoEntry Real EndOfFrame Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();

                yield return null;

                int before = entry.Module.EndOfFrameCount;
                for (int i = 0; i < 5 && entry.Module.EndOfFrameCount <= before; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.Module.EndOfFrameCount, Is.GreaterThan(before),
                    "TryGetMonoEntry 应通过真实 Unity WaitForEndOfFrame coroutine 驱动 EndOfFrame Module");

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
        public IEnumerator TryGetMonoEntry_Destroy_StopsEndOfFrameCoroutine()
        {
            var gameObject = new GameObject("TryGetMonoEntry Stop EndOfFrame Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();
                yield return null;

                var module = entry.Module;
                for (int i = 0; i < 5 && module.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(module.EndOfFrameCount, Is.GreaterThan(0),
                    "测试前提：EndOfFrame coroutine 已经在真实 Unity 帧中驱动过模块。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry == null || !entry.HasHost, Is.True);
                Assert.That(module.ShutdownCount, Is.EqualTo(1));

                int endOfFrameCountAfterDestroy = module.EndOfFrameCount;
                for (int i = 0; i < 3; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(module.EndOfFrameCount, Is.EqualTo(endOfFrameCountAfterDestroy),
                    "TryGetMonoEntry 销毁后 EndOfFrame coroutine 应停止，不应继续驱动已 Shutdown 的模块。");
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        internal static void DriveEndOfFrameCoroutine(TrackingTryGetMonoEntry entry)
        {
            var method = typeof(TryGetMonoEntry).GetMethod(
                "EndOfFrameCoroutine",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            var coroutine = (IEnumerator)method.Invoke(entry, null);
            Assert.That(coroutine.MoveNext(), Is.True);
            Assert.That(coroutine.Current, Is.TypeOf<WaitForEndOfFrame>());

            int before = entry.Module.EndOfFrameCount;
            Assert.That(coroutine.MoveNext(), Is.True);
            Assert.That(entry.Module.EndOfFrameCount, Is.GreaterThan(before));
        }
    }
}
