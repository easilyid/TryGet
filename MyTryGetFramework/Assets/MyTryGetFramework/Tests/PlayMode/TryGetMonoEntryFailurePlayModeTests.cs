using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryFailurePlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_SetupFailure_ClearsHostAndKeepsExceptionVisible()
        {
            var gameObject = new GameObject("TryGetMonoEntry Setup Failure Test");
            var destroyRequested = false;

            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: setup failed"));

                var entry = gameObject.AddComponent<SetupFailureTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasHost, Is.False);
                LogAssert.NoUnexpectedReceived();

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
        public IEnumerator TryGetMonoEntry_SetupFailureAfterRegisteringModule_ClearsHostWithoutLifecycleCalls()
        {
            var gameObject = new GameObject("TryGetMonoEntry Setup Partial Failure Test");
            var destroyRequested = false;

            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: setup failed after register"));

                var entry = gameObject.AddComponent<SetupFailureAfterRegisterTryGetMonoEntry>();
                var probe = entry.Probe;

                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(probe.InitCount, Is.EqualTo(0),
                    "A module registered before Setup throws must not be initialized after the host is discarded.");
                Assert.That(probe.ShutdownCount, Is.EqualTo(0),
                    "A module that never reached OnInit should not receive Shutdown during setup-failure cleanup.");

                for (int i = 0; i < 3; i++)
                    yield return null;

                LogAssert.NoUnexpectedReceived();

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
        public IEnumerator TryGetMonoEntry_DefaultPersistentSetupFailure_DoesNotSurviveOwningSceneUnload()
        {
            var scene = SceneManager.CreateScene("TryGet_SetupFailurePersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry Persistent Setup Failure Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: persistent setup failed"));

                var entry = gameObject.AddComponent<PersistentSetupFailureTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasHost, Is.False);

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.True,
                    "A default-persistent entry that failed during Setup must not leave a broken Host-less GameObject in DontDestroyOnLoad.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_DefaultPersistentInitializeFailure_DoesNotSurviveOwningSceneUnload()
        {
            var scene = SceneManager.CreateScene("TryGet_InitializeFailurePersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("TryGetMonoEntry Persistent Initialize Failure Test");
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: init failed"));

                var entry = gameObject.AddComponent<PersistentInitializeFailureTryGetMonoEntry>();
                var probe = entry.Probe;

                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(probe.InitCount, Is.EqualTo(1));
                Assert.That(probe.ShutdownCount, Is.EqualTo(1),
                    "Initialize failure must roll back modules that already completed OnInit.");
                Assert.That(gameObject.scene.handle, Is.EqualTo(scene.handle),
                    "A default-persistent entry must only move to DontDestroyOnLoad after Initialize succeeds.");

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.True,
                    "A default-persistent entry that failed during Initialize must not leave a broken Host-less GameObject in DontDestroyOnLoad.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_InitializeFailure_RollsBackAndDoesNotDriveFailedHost()
        {
            var gameObject = new GameObject("TryGetMonoEntry Initialize Failure Test");
            var destroyRequested = false;

            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: init failed"));

                var entry = gameObject.AddComponent<InitializeFailureTryGetMonoEntry>();
                var probe = entry.Probe;

                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(probe.InitCount, Is.EqualTo(1));
                Assert.That(probe.ShutdownCount, Is.EqualTo(1));

                for (int i = 0; i < 3; i++)
                    yield return null;

                LogAssert.NoUnexpectedReceived();

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
        public IEnumerator TryGetMonoEntry_ShutdownFailure_ClearsHostAndKeepsExceptionVisible()
        {
            var gameObject = new GameObject("TryGetMonoEntry Shutdown Failure Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ShutdownFailureTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.Module.InitCount, Is.EqualTo(1));

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: shutdown failed"));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.Module.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_ShutdownFailure_DoesNotBlockOtherModulesCleanup()
        {
            var gameObject = new GameObject("TryGetMonoEntry Shutdown Failure Cleanup Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ShutdownFailureDoesNotBlockCleanupTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.FailingModule.InitCount, Is.EqualTo(1));
                Assert.That(entry.CleanupModule.InitCount, Is.EqualTo(1));

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: shutdown failed"));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.FailingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.CleanupModule.ShutdownCount, Is.EqualTo(1),
                    "某个 Module.Shutdown 抛异常时，ModuleSystem 仍应继续 Shutdown 其它已初始化 Module。");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_UpdateModuleThrows_ExceptionVisibleSkipsLaterModuleAndRecoversNextFrame()
        {
            var gameObject = new GameObject("TryGetMonoEntry Update Failure Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<UpdateFailureTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: update failed"));

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.UpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterModule.UpdateCount, Is.EqualTo(0),
                    "某个 IUpdateModule 在真实 TryGetMonoEntry.Update 中抛异常时，同帧后续 Update module 应被 fail-fast 跳过。");

                yield return null;

                Assert.That(entry.ThrowingModule.UpdateCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(entry.LaterModule.UpdateCount, Is.GreaterThanOrEqualTo(1),
                    "Unity 记录上一帧异常后，TryGetMonoEntry 的 host 应保持可用，并在下一帧继续派发。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_UpdateModuleThrows_LateUpdateAndEndOfFrameStillRun()
        {
            var gameObject = new GameObject("TryGetMonoEntry Update Later Phase Isolation Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<UpdateFailureWithLaterPhasesTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: update failed"));

                yield return null;

                for (int i = 0; i < 5 && entry.LaterPhaseModule.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.UpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterPhaseModule.LateUpdateCount, Is.GreaterThanOrEqualTo(1),
                    "Update phase 的异常不应阻止 Unity 后续 LateUpdate 回调继续驱动框架。");
                Assert.That(entry.LaterPhaseModule.EndOfFrameCount, Is.GreaterThanOrEqualTo(1),
                    "Update phase 的异常不应阻止 TryGetMonoEntry 的 EndOfFrame coroutine 在真实 Unity 帧中继续运行。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterPhaseModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_EarlyUpdateModuleThrows_ExceptionVisibleSkipsUpdatePhaseAndRecoversNextFrame()
        {
            var gameObject = new GameObject("TryGetMonoEntry EarlyUpdate Failure Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<EarlyUpdateFailureTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: early update failed"));

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.EarlyUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterEarlyModule.EarlyUpdateCount, Is.EqualTo(0),
                    "某个 IEarlyUpdateModule 在真实 TryGetMonoEntry.Update 开头抛异常时，同帧后续 EarlyUpdate module 应被 fail-fast 跳过。");
                Assert.That(entry.UpdateModule.UpdateCount, Is.EqualTo(0),
                    "EarlyUpdate 异常会退出 TryGetMonoEntry.Update，同一 Unity 帧不应继续派发 Update phase。");

                yield return null;

                Assert.That(entry.ThrowingModule.EarlyUpdateCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(entry.LaterEarlyModule.EarlyUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.UpdateModule.UpdateCount, Is.GreaterThanOrEqualTo(1),
                    "Unity 记录上一帧 EarlyUpdate 异常后，TryGetMonoEntry 应保持 host 可用，并在下一帧恢复 EarlyUpdate/Update 派发。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterEarlyModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.UpdateModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_EarlyUpdateModuleThrows_LateUpdateAndEndOfFrameStillRun()
        {
            var gameObject = new GameObject("TryGetMonoEntry EarlyUpdate Later Phase Isolation Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<EarlyUpdateFailureWithLaterPhasesTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: early update failed"));

                yield return null;

                for (int i = 0; i < 5 && entry.LaterPhaseModule.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.EarlyUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterPhaseModule.LateUpdateCount, Is.GreaterThanOrEqualTo(1),
                    "EarlyUpdate phase 的异常不应阻止 Unity 后续 LateUpdate 回调继续驱动框架。");
                Assert.That(entry.LaterPhaseModule.EndOfFrameCount, Is.GreaterThanOrEqualTo(1),
                    "EarlyUpdate phase 的异常不应阻止 TryGetMonoEntry 的 EndOfFrame coroutine 在真实 Unity 帧中继续运行。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterPhaseModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_FixedUpdateModuleThrows_ExceptionVisibleSkipsLaterModuleAndRecoversNextFixedStep()
        {
            var gameObject = new GameObject("TryGetMonoEntry FixedUpdate Failure Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<FixedUpdateFailureTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: fixed update failed"));

                for (int i = 0; i < 10 && entry.ThrowingModule.FixedUpdateCount == 0; i++)
                    yield return new WaitForFixedUpdate();

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.FixedUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterModule.FixedUpdateCount, Is.EqualTo(0),
                    "某个 IFixedUpdateModule 在真实 TryGetMonoEntry.FixedUpdate 中抛异常时，同次 fixed step 后续 FixedUpdate module 应被 fail-fast 跳过。");

                for (int i = 0; i < 10 && entry.LaterModule.FixedUpdateCount == 0; i++)
                    yield return new WaitForFixedUpdate();

                Assert.That(entry.ThrowingModule.FixedUpdateCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(entry.LaterModule.FixedUpdateCount, Is.GreaterThanOrEqualTo(1),
                    "Unity 记录上一 fixed step 异常后，TryGetMonoEntry 的 host 应保持可用，并在下一次 FixedUpdate 继续派发。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_FixedUpdateModuleThrows_UpdateLateUpdateAndEndOfFrameStillRun()
        {
            var gameObject = new GameObject("TryGetMonoEntry FixedUpdate Later Phase Isolation Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<FixedUpdateFailureWithLaterPhasesTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: fixed update failed"));

                for (int i = 0; i < 10 && entry.ThrowingModule.FixedUpdateCount == 0; i++)
                    yield return new WaitForFixedUpdate();

                int updateBefore = entry.LaterPhaseModule.UpdateCount;
                int lateBefore = entry.LaterPhaseModule.LateUpdateCount;
                int endBefore = entry.LaterPhaseModule.EndOfFrameCount;

                for (int i = 0; i < 10 &&
                    (entry.LaterPhaseModule.UpdateCount <= updateBefore ||
                     entry.LaterPhaseModule.LateUpdateCount <= lateBefore ||
                     entry.LaterPhaseModule.EndOfFrameCount <= endBefore); i++)
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.FixedUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterPhaseModule.UpdateCount, Is.GreaterThan(updateBefore),
                    "FixedUpdate phase 的异常不应阻止 Unity 后续 Update 回调继续驱动框架。");
                Assert.That(entry.LaterPhaseModule.LateUpdateCount, Is.GreaterThan(lateBefore),
                    "FixedUpdate phase 的异常不应阻止 Unity 后续 LateUpdate 回调继续驱动框架。");
                Assert.That(entry.LaterPhaseModule.EndOfFrameCount, Is.GreaterThan(endBefore),
                    "FixedUpdate phase 的异常不应阻止 TryGetMonoEntry 的 EndOfFrame coroutine 在后续真实 Unity 帧中继续运行。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterPhaseModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_LateUpdateModuleThrows_ExceptionVisibleSkipsLaterModuleAndRecoversNextFrame()
        {
            var gameObject = new GameObject("TryGetMonoEntry LateUpdate Failure Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<LateUpdateFailureTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: late update failed"));

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.LateUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterModule.LateUpdateCount, Is.EqualTo(0),
                    "某个 ILateUpdateModule 在真实 TryGetMonoEntry.LateUpdate 中抛异常时，同帧后续 LateUpdate module 应被 fail-fast 跳过。");

                yield return null;

                Assert.That(entry.ThrowingModule.LateUpdateCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(entry.LaterModule.LateUpdateCount, Is.GreaterThanOrEqualTo(1),
                    "Unity 记录上一帧 LateUpdate 异常后，TryGetMonoEntry 的 host 应保持可用，并在下一帧继续派发。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_LateUpdateModuleThrows_EndOfFrameCoroutineStillRuns()
        {
            var gameObject = new GameObject("TryGetMonoEntry LateUpdate EndOfFrame Isolation Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<LateUpdateFailureWithEndOfFrameTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: late update failed"));

                for (int i = 0; i < 5 && entry.EndOfFrameModule.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.LateUpdateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.EndOfFrameModule.EndOfFrameCount, Is.GreaterThanOrEqualTo(1),
                    "LateUpdate phase 的异常不应阻止 TryGetMonoEntry 的 EndOfFrame coroutine 在真实 Unity 帧中继续运行。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.EndOfFrameModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_EndOfFrameModuleThrows_ExceptionVisibleSkipsLaterModuleAndRecoversNextFrame()
        {
            var gameObject = new GameObject("TryGetMonoEntry EndOfFrame Failure Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<EndOfFrameFailureTryGetMonoEntry>();

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: end of frame failed"));

                for (int i = 0; i < 5 && entry.ThrowingModule.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.ThrowingModule.EndOfFrameCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.LaterModule.EndOfFrameCount, Is.EqualTo(0),
                    "某个 IEndOfFrameModule 在真实 TryGetMonoEntry EndOfFrame coroutine 中抛异常时，同帧后续 EndOfFrame module 应被 fail-fast 跳过。");

                for (int i = 0; i < 5 && entry.LaterModule.EndOfFrameCount == 0; i++)
                    yield return new WaitForEndOfFrame();

                Assert.That(entry.ThrowingModule.EndOfFrameCount, Is.GreaterThanOrEqualTo(2),
                    "Unity 记录上一帧 EndOfFrame 异常后，TryGetMonoEntry 的 EndOfFrame coroutine 应继续派发下一帧。");
                Assert.That(entry.LaterModule.EndOfFrameCount, Is.GreaterThanOrEqualTo(1),
                    "上一帧异常恢复后，后续 EndOfFrame module 应在下一次 EndOfFrame 正常收到派发。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.ThrowingModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.LaterModule.ShutdownCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    public sealed class SetupFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            throw new InvalidOperationException("setup failed");
        }
    }

    public sealed class SetupFailureAfterRegisterTryGetMonoEntry : TryGetMonoEntry
    {
        public FailureProbeModule Probe { get; } = new FailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IFailureProbeModule>(Probe);
            throw new InvalidOperationException("setup failed after register");
        }
    }

    public sealed class PersistentSetupFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            throw new InvalidOperationException("persistent setup failed");
        }
    }

    public interface IFailureProbeModule : IModule
    {
    }

    public sealed class FailureProbeModule : IFailureProbeModule
    {
        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface IFailingInitModule : IModule
    {
    }

    public sealed class FailingInitModule : IFailingInitModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public void OnInit(IModuleSystem host) => throw new InvalidOperationException("init failed");
        public void Shutdown() { }
    }

    public sealed class InitializeFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public FailureProbeModule Probe { get; } = new FailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IFailureProbeModule>(Probe);
            host.Register<IFailingInitModule>(new FailingInitModule());
        }
    }

    public sealed class PersistentInitializeFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public FailureProbeModule Probe { get; } = new FailureProbeModule();
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IFailureProbeModule>(Probe);
            host.Register<IFailingInitModule>(new FailingInitModule());
        }
    }

    public interface IShutdownFailureModule : IModule
    {
    }

    public sealed class ShutdownFailureModule : IShutdownFailureModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
            throw new InvalidOperationException("shutdown failed");
        }
    }

    public sealed class ShutdownFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public ShutdownFailureModule Module { get; } = new ShutdownFailureModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IShutdownFailureModule>(Module);
        }
    }

    public interface IShutdownCleanupProbeModule : IModule
    {
    }

    public sealed class ShutdownCleanupProbeModule : IShutdownCleanupProbeModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class ShutdownFailureDoesNotBlockCleanupTryGetMonoEntry : TryGetMonoEntry
    {
        public ShutdownFailureModule FailingModule { get; } = new ShutdownFailureModule();
        public ShutdownCleanupProbeModule CleanupModule { get; } = new ShutdownCleanupProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IShutdownCleanupProbeModule>(CleanupModule);
            host.Register<IShutdownFailureModule>(FailingModule);
        }
    }

    public interface IThrowingUpdateProbeModule : IModule, IUpdateModule
    {
    }

    public sealed class ThrowingUpdateProbeModule : IThrowingUpdateProbeModule
    {
        private bool _hasThrown;

        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int UpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            UpdateCount++;
            if (_hasThrown)
                return;

            _hasThrown = true;
            throw new InvalidOperationException("update failed");
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface ILaterUpdateProbeModule : IModule, IUpdateModule
    {
    }

    public sealed class LaterUpdateProbeModule : ILaterUpdateProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingUpdateProbeModule) };
        public int UpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            UpdateCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class UpdateFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingUpdateProbeModule ThrowingModule { get; } = new ThrowingUpdateProbeModule();
        public LaterUpdateProbeModule LaterModule { get; } = new LaterUpdateProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterUpdateProbeModule>(LaterModule);
        }
    }

    public interface ILaterPhasesAfterUpdateFailureProbeModule : IModule, ILateUpdateModule, IEndOfFrameModule
    {
    }

    public sealed class LaterPhasesAfterUpdateFailureProbeModule : ILaterPhasesAfterUpdateFailureProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingUpdateProbeModule) };
        public int LateUpdateCount { get; private set; }
        public int EndOfFrameCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            LateUpdateCount++;
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class UpdateFailureWithLaterPhasesTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingUpdateProbeModule ThrowingModule { get; } = new ThrowingUpdateProbeModule();
        public LaterPhasesAfterUpdateFailureProbeModule LaterPhaseModule { get; } = new LaterPhasesAfterUpdateFailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterPhasesAfterUpdateFailureProbeModule>(LaterPhaseModule);
        }
    }

    public interface IThrowingEarlyUpdateProbeModule : IModule, IEarlyUpdateModule
    {
    }

    public sealed class ThrowingEarlyUpdateProbeModule : IThrowingEarlyUpdateProbeModule
    {
        private bool _hasThrown;

        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int EarlyUpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            EarlyUpdateCount++;
            if (_hasThrown)
                return;

            _hasThrown = true;
            throw new InvalidOperationException("early update failed");
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface ILaterEarlyUpdateProbeModule : IModule, IEarlyUpdateModule
    {
    }

    public sealed class LaterEarlyUpdateProbeModule : ILaterEarlyUpdateProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingEarlyUpdateProbeModule) };
        public int EarlyUpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            EarlyUpdateCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface IUpdateAfterEarlyFailureProbeModule : IModule, IUpdateModule
    {
    }

    public sealed class UpdateAfterEarlyFailureProbeModule : IUpdateAfterEarlyFailureProbeModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int UpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            UpdateCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class EarlyUpdateFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingEarlyUpdateProbeModule ThrowingModule { get; } = new ThrowingEarlyUpdateProbeModule();
        public LaterEarlyUpdateProbeModule LaterEarlyModule { get; } = new LaterEarlyUpdateProbeModule();
        public UpdateAfterEarlyFailureProbeModule UpdateModule { get; } = new UpdateAfterEarlyFailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingEarlyUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterEarlyUpdateProbeModule>(LaterEarlyModule);
            host.Register<IUpdateAfterEarlyFailureProbeModule>(UpdateModule);
        }
    }

    public interface ILaterPhasesAfterEarlyFailureProbeModule : IModule, ILateUpdateModule, IEndOfFrameModule
    {
    }

    public sealed class LaterPhasesAfterEarlyFailureProbeModule : ILaterPhasesAfterEarlyFailureProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingEarlyUpdateProbeModule) };
        public int LateUpdateCount { get; private set; }
        public int EndOfFrameCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            LateUpdateCount++;
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class EarlyUpdateFailureWithLaterPhasesTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingEarlyUpdateProbeModule ThrowingModule { get; } = new ThrowingEarlyUpdateProbeModule();
        public LaterPhasesAfterEarlyFailureProbeModule LaterPhaseModule { get; } = new LaterPhasesAfterEarlyFailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingEarlyUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterPhasesAfterEarlyFailureProbeModule>(LaterPhaseModule);
        }
    }

    public interface IThrowingFixedUpdateProbeModule : IModule, IFixedUpdateModule
    {
    }

    public sealed class ThrowingFixedUpdateProbeModule : IThrowingFixedUpdateProbeModule
    {
        private bool _hasThrown;

        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int FixedUpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void FixedUpdate(float deltaTime, float unscaledDeltaTime)
        {
            FixedUpdateCount++;
            if (_hasThrown)
                return;

            _hasThrown = true;
            throw new InvalidOperationException("fixed update failed");
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface ILaterFixedUpdateProbeModule : IModule, IFixedUpdateModule
    {
    }

    public sealed class LaterFixedUpdateProbeModule : ILaterFixedUpdateProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingFixedUpdateProbeModule) };
        public int FixedUpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void FixedUpdate(float deltaTime, float unscaledDeltaTime)
        {
            FixedUpdateCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class FixedUpdateFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingFixedUpdateProbeModule ThrowingModule { get; } = new ThrowingFixedUpdateProbeModule();
        public LaterFixedUpdateProbeModule LaterModule { get; } = new LaterFixedUpdateProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingFixedUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterFixedUpdateProbeModule>(LaterModule);
        }
    }

    public interface ILaterPhasesAfterFixedFailureProbeModule :
        IModule,
        IUpdateModule,
        ILateUpdateModule,
        IEndOfFrameModule
    {
    }

    public sealed class LaterPhasesAfterFixedFailureProbeModule : ILaterPhasesAfterFixedFailureProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingFixedUpdateProbeModule) };
        public int UpdateCount { get; private set; }
        public int LateUpdateCount { get; private set; }
        public int EndOfFrameCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            UpdateCount++;
        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            LateUpdateCount++;
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class FixedUpdateFailureWithLaterPhasesTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingFixedUpdateProbeModule ThrowingModule { get; } = new ThrowingFixedUpdateProbeModule();
        public LaterPhasesAfterFixedFailureProbeModule LaterPhaseModule { get; } = new LaterPhasesAfterFixedFailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingFixedUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterPhasesAfterFixedFailureProbeModule>(LaterPhaseModule);
        }
    }

    public interface IThrowingLateUpdateProbeModule : IModule, ILateUpdateModule
    {
    }

    public sealed class ThrowingLateUpdateProbeModule : IThrowingLateUpdateProbeModule
    {
        private bool _hasThrown;

        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int LateUpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            LateUpdateCount++;
            if (_hasThrown)
                return;

            _hasThrown = true;
            throw new InvalidOperationException("late update failed");
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface ILaterLateUpdateProbeModule : IModule, ILateUpdateModule
    {
    }

    public sealed class LaterLateUpdateProbeModule : ILaterLateUpdateProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingLateUpdateProbeModule) };
        public int LateUpdateCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            LateUpdateCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class LateUpdateFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingLateUpdateProbeModule ThrowingModule { get; } = new ThrowingLateUpdateProbeModule();
        public LaterLateUpdateProbeModule LaterModule { get; } = new LaterLateUpdateProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingLateUpdateProbeModule>(ThrowingModule);
            host.Register<ILaterLateUpdateProbeModule>(LaterModule);
        }
    }

    public interface IEndOfFrameAfterLateFailureProbeModule : IModule, IEndOfFrameModule
    {
    }

    public sealed class EndOfFrameAfterLateFailureProbeModule : IEndOfFrameAfterLateFailureProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingLateUpdateProbeModule) };
        public int EndOfFrameCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class LateUpdateFailureWithEndOfFrameTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingLateUpdateProbeModule ThrowingModule { get; } = new ThrowingLateUpdateProbeModule();
        public EndOfFrameAfterLateFailureProbeModule EndOfFrameModule { get; } = new EndOfFrameAfterLateFailureProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingLateUpdateProbeModule>(ThrowingModule);
            host.Register<IEndOfFrameAfterLateFailureProbeModule>(EndOfFrameModule);
        }
    }

    public interface IThrowingEndOfFrameProbeModule : IModule, IEndOfFrameModule
    {
    }

    public sealed class ThrowingEndOfFrameProbeModule : IThrowingEndOfFrameProbeModule
    {
        private bool _hasThrown;

        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int EndOfFrameCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
            if (_hasThrown)
                return;

            _hasThrown = true;
            throw new InvalidOperationException("end of frame failed");
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public interface ILaterEndOfFrameProbeModule : IModule, IEndOfFrameModule
    {
    }

    public sealed class LaterEndOfFrameProbeModule : ILaterEndOfFrameProbeModule
    {
        public int Priority => 10;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowingEndOfFrameProbeModule) };
        public int EndOfFrameCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host) { }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class EndOfFrameFailureTryGetMonoEntry : TryGetMonoEntry
    {
        public ThrowingEndOfFrameProbeModule ThrowingModule { get; } = new ThrowingEndOfFrameProbeModule();
        public LaterEndOfFrameProbeModule LaterModule { get; } = new LaterEndOfFrameProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IThrowingEndOfFrameProbeModule>(ThrowingModule);
            host.Register<ILaterEndOfFrameProbeModule>(LaterModule);
        }
    }
}
