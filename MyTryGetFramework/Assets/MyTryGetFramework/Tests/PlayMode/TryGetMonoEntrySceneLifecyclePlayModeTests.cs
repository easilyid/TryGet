using System;
using System.Collections;
using NUnit.Framework;
using TryGet.Async;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntrySceneLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_NonPersistentEntry_ShutsDownWhenOwningSceneUnloads()
        {
            var scene = SceneManager.CreateScene("TryGet_NonPersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry NonPersistent Scene Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();
            var module = entry.Module;

            yield return null;

            Assert.That(entry.HasHost, Is.True);
            Assert.That(module.InitCount, Is.EqualTo(1));

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(module.ShutdownCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_NonPersistentEntry_StopsDrivingFramesAfterOwningSceneUnloads()
        {
            var scene = SceneManager.CreateScene("TryGet_NonPersistent_Drive_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry NonPersistent Frame Drive Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();
            var module = entry.Module;

            yield return null;
            for (int i = 0; i < 5 && module.EndOfFrameCount == 0; i++)
                yield return new WaitForEndOfFrame();

            Assert.That(entry.HasHost, Is.True);
            Assert.That(module.UpdateCount, Is.GreaterThan(0));
            Assert.That(module.LateUpdateCount, Is.GreaterThan(0));
            Assert.That(module.EndOfFrameCount, Is.GreaterThan(0));

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(module.ShutdownCount, Is.EqualTo(1));
            Assert.That(entry == null || !entry.HasHost, Is.True);

            int updateAfterUnload = module.UpdateCount;
            int lateAfterUnload = module.LateUpdateCount;
            int endAfterUnload = module.EndOfFrameCount;

            for (int i = 0; i < 3; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            Assert.That(module.UpdateCount, Is.EqualTo(updateAfterUnload),
                "非持久 Entry 所属场景卸载后，不应继续通过 Unity Update 驱动旧模块。");
            Assert.That(module.LateUpdateCount, Is.EqualTo(lateAfterUnload),
                "非持久 Entry 所属场景卸载后，不应继续通过 Unity LateUpdate 驱动旧模块。");
            Assert.That(module.EndOfFrameCount, Is.EqualTo(endAfterUnload),
                "非持久 Entry 所属场景卸载后，EndOfFrame coroutine 不应继续运行。");
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_NonPersistentEntry_PendingSchedulerTaskCancelsWhenOwningSceneUnloads()
        {
            var scene = SceneManager.CreateScene("TryGet_NonPersistent_Scheduler_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry NonPersistent Scheduler Scene Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<TrackingTryGetMonoEntry>();
            yield return null;

            Assert.That(entry.ExposedHost.TryGet<ITGTaskScheduler>(out var scheduler), Is.True);

            var probe = new PersistentSchedulerProbe();
            probe.WaitForFrames(scheduler, 1000).Forget();
            yield return null;

            Assert.That(probe.Completed, Is.False,
                "测试前提：pending scheduler task 应在卸载所属场景前仍未完成。");
            Assert.That(probe.Canceled, Is.False);

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(probe.Canceled, Is.True,
                "非持久 Entry 所属场景卸载时，OnDestroy 应 Shutdown 默认 Scheduler 并取消 pending task。");
            Assert.That(probe.Completed, Is.False);
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_PersistentEntry_SurvivesOwningSceneUnload_AndShutsDownOnDestroy()
        {
            var scene = SceneManager.CreateScene("TryGet_Persistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry Persistent Scene Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentTryGetMonoEntry>();
                var module = entry.Module;

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(module.InitCount, Is.EqualTo(1));
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(entry.HasHost, Is.True);
                Assert.That(module.ShutdownCount, Is.EqualTo(0));

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
        public IEnumerator TryGetMonoEntry_PersistentEntry_ContinuesDrivingFramesAfterOwningSceneUnload()
        {
            var scene = SceneManager.CreateScene("TryGet_Persistent_Drive_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry Persistent Frame Drive Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentTryGetMonoEntry>();
                var module = entry.Module;

                yield return null;
                for (int i = 0; i < 5 && module.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.HasHost, Is.True);
                Assert.That(module.UpdateCount, Is.GreaterThan(0));
                Assert.That(module.LateUpdateCount, Is.GreaterThan(0));
                Assert.That(module.EndOfFrameCount, Is.GreaterThan(0));

                int updateBeforeUnload = module.UpdateCount;
                int lateBeforeUnload = module.LateUpdateCount;
                int endBeforeUnload = module.EndOfFrameCount;

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
                Assert.That(entry.HasHost, Is.True);
                Assert.That(module.ShutdownCount, Is.EqualTo(0));

                for (int i = 0; i < 5 && module.EndOfFrameCount <= endBeforeUnload; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(module.UpdateCount, Is.GreaterThan(updateBeforeUnload),
                    "持久化 Entry 卸载原场景后应继续通过 Unity Update 驱动模块。");
                Assert.That(module.LateUpdateCount, Is.GreaterThan(lateBeforeUnload),
                    "持久化 Entry 卸载原场景后应继续通过 Unity LateUpdate 驱动模块。");
                Assert.That(module.EndOfFrameCount, Is.GreaterThan(endBeforeUnload),
                    "持久化 Entry 卸载原场景后 EndOfFrame coroutine 应继续运行。");

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
        public IEnumerator TryGetMonoEntry_PersistentEntry_PendingSchedulerTaskCompletesAfterOwningSceneUnload()
        {
            var scene = SceneManager.CreateScene("TryGet_Persistent_Scheduler_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry Persistent Scheduler Scene Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentTryGetMonoEntry>();
                yield return null;

                var probe = new PersistentSchedulerProbe();
                probe.WaitForFrames(entry.Scheduler, 20).Forget();
                yield return null;

                Assert.That(probe.Completed, Is.False,
                    "测试前提：pending scheduler task 应在卸载原场景前仍未完成。");

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.Module.ShutdownCount, Is.EqualTo(0));
                Assert.That(probe.Canceled, Is.False,
                    "持久化 Entry 的原场景卸载不应取消默认 Scheduler 上已经排队的任务。");

                for (int i = 0; i < 80 && !probe.Completed && !probe.Canceled; i++)
                    yield return null;

                Assert.That(probe.Completed, Is.True,
                    "持久化 Entry 卸载原场景后，默认 Scheduler 上的 pending task 应继续由真实 Unity 帧驱动完成。");
                Assert.That(probe.Canceled, Is.False);

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

    public sealed class PersistentSchedulerProbe
    {
        public bool Completed { get; private set; }
        public bool Canceled { get; private set; }

        public async TGTask WaitForFrames(ITGTaskScheduler scheduler, int frames)
        {
            try
            {
                await scheduler.WaitForFrames(frames);
                Completed = true;
            }
            catch (OperationCanceledException)
            {
                Canceled = true;
            }
        }
    }
}
