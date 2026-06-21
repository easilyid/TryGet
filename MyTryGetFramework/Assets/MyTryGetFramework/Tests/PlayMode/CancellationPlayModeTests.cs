using System;
using System.Collections;
using NUnit.Framework;
using TryGet.Async;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// 取消模型（ADR-0021）在真实 Unity 帧循环下的端到端验证。
    ///
    /// EditMode 用手动 scheduler.Update 驱动取消；本组用真实 Unity 帧推进 Delay，覆盖用户实际接入路径：
    /// - <see cref="TGCancelSource.Cancel"/> 取消 pending Delay，await 抛 OperationCanceledException；
    /// - <see cref="TGCancelSource.CancelAfter"/> 真实秒级超时后自动取消（依赖真实帧推进 Delay，PlayMode 独有价值）；
    /// - <see cref="TGCancelToken.None"/> 的 Delay 跨真实帧正常完成，不受取消影响。
    ///
    /// 取消契约：以 OperationCanceledException（含子类 <see cref="TGTaskAbortException"/>）完成，用户应捕获此基类。
    /// </summary>
    [TestFixture]
    public sealed class CancellationPlayModeTests
    {
        [UnityTest]
        public IEnumerator CancelSource_CancelPendingDelay_AwaitThrowsOperationCanceled()
        {
            var gameObject = new GameObject(nameof(CancelSource_CancelPendingDelay_AwaitThrowsOperationCanceled));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                var source = TGCancelSource.Rent();
                probe.AwaitDelay(entry.Scheduler, 100f, source.Token).Forget();

                yield return null;
                Assert.That(probe.Caught, Is.False, "尚未取消，pending 不应结束");
                Assert.That(probe.CompletedNormally, Is.False);

                source.Cancel();
                for (int i = 0; i < 5 && !probe.Caught; i++)
                    yield return null;

                Assert.That(probe.Caught, Is.True, "Cancel 后 await 应以取消异常结束");
                Assert.That(probe.CompletedNormally, Is.False, "取消的 Delay 不应正常完成");
                Assert.That(typeof(OperationCanceledException).IsAssignableFrom(probe.CaughtType), Is.True,
                    "取消应以 OperationCanceledException（含子类）完成");

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
        public IEnumerator CancelSource_CancelFullDelayOverload_AwaitThrowsOperationCanceled()
        {
            var gameObject = new GameObject(nameof(CancelSource_CancelFullDelayOverload_AwaitThrowsOperationCanceled));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                var source = TGCancelSource.Rent();
                probe.AwaitFullDelayOverload(
                    entry.Scheduler,
                    100f,
                    FramePhase.LateUpdate,
                    TimeMode.Unscaled,
                    source.Token).Forget();

                yield return null;
                Assert.That(probe.Caught, Is.False);
                Assert.That(probe.CompletedNormally, Is.False);

                source.Cancel();
                for (int i = 0; i < 5 && !probe.Caught; i++)
                    yield return null;

                Assert.That(probe.Caught, Is.True,
                    "Delay(seconds, phase, timeMode, token) 完整重载应在真实 Unity 帧下响应取消。");
                Assert.That(probe.CompletedNormally, Is.False);
                Assert.That(typeof(OperationCanceledException).IsAssignableFrom(probe.CaughtType), Is.True);

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
        public IEnumerator CancelSource_CancelWaitForFramesPhaseOverload_AwaitThrowsOperationCanceled()
        {
            var gameObject = new GameObject(nameof(CancelSource_CancelWaitForFramesPhaseOverload_AwaitThrowsOperationCanceled));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                var source = TGCancelSource.Rent();
                probe.AwaitWaitForFramesOverload(entry.Scheduler, 100, FramePhase.LateUpdate, source.Token).Forget();

                yield return null;
                Assert.That(probe.Caught, Is.False);
                Assert.That(probe.CompletedNormally, Is.False);

                source.Cancel();
                for (int i = 0; i < 5 && !probe.Caught; i++)
                    yield return null;

                Assert.That(probe.Caught, Is.True,
                    "WaitForFrames(frameCount, phase, token) 完整重载应在真实 Unity 帧下响应取消。");
                Assert.That(probe.CompletedNormally, Is.False);
                Assert.That(typeof(OperationCanceledException).IsAssignableFrom(probe.CaughtType), Is.True);

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
        public IEnumerator CancelSource_CancelYieldPhaseOverload_AwaitThrowsOperationCanceled()
        {
            var gameObject = new GameObject(nameof(CancelSource_CancelYieldPhaseOverload_AwaitThrowsOperationCanceled));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                var source = TGCancelSource.Rent();
                probe.AwaitYieldOverload(entry.Scheduler, FramePhase.LateUpdate, source.Token).Forget();

                source.Cancel();
                for (int i = 0; i < 5 && !probe.Caught; i++)
                    yield return null;

                Assert.That(probe.Caught, Is.True,
                    "Yield(phase, token) 完整重载应在真实 Unity 帧下响应取消。");
                Assert.That(probe.CompletedNormally, Is.False);
                Assert.That(typeof(OperationCanceledException).IsAssignableFrom(probe.CaughtType), Is.True);

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
        public IEnumerator CancelAfter_RealTimeout_AutoCancelsPendingDelay()
        {
            var gameObject = new GameObject(nameof(CancelAfter_RealTimeout_AutoCancelsPendingDelay));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                var source = TGCancelSource.Rent();
                source.CancelAfter(0.05f, entry.Scheduler); // 0.05s 后自动取消
                probe.AwaitDelay(entry.Scheduler, 100f, source.Token).Forget();

                for (int i = 0; i < 120 && !probe.Caught; i++)
                    yield return null;

                Assert.That(probe.Caught, Is.True, "CancelAfter 超时后应自动取消 pending Delay");
                Assert.That(probe.CompletedNormally, Is.False);

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
        public IEnumerator NoneToken_Delay_CompletesNormallyAcrossFrames()
        {
            var gameObject = new GameObject(nameof(NoneToken_Delay_CompletesNormallyAcrossFrames));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                probe.AwaitDelay(entry.Scheduler, 0.05f, TGCancelToken.None).Forget();

                for (int i = 0; i < 120 && !probe.CompletedNormally; i++)
                    yield return null;

                Assert.That(probe.CompletedNormally, Is.True, "None token 的 Delay 应跨真实帧正常完成");
                Assert.That(probe.Caught, Is.False, "未取消，不应抛取消异常");

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
        public IEnumerator TryGetMonoEntry_Destroy_CancelsDefaultSchedulerPendingTasks()
        {
            var gameObject = new GameObject(nameof(TryGetMonoEntry_Destroy_CancelsDefaultSchedulerPendingTasks));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new CancellationProbe();
                probe.AwaitDelay(entry.Scheduler, 100f, TGCancelToken.None).Forget();

                yield return null;
                Assert.That(probe.Caught, Is.False);
                Assert.That(probe.CompletedNormally, Is.False);

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;

                for (int i = 0; i < 5 && !probe.Caught; i++)
                    yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
                Assert.That(probe.Caught, Is.True,
                    "TryGetMonoEntry.OnDestroy 应 Shutdown 默认 TGTaskScheduler，并取消所有 pending task。");
                Assert.That(probe.CompletedNormally, Is.False);
                Assert.That(typeof(OperationCanceledException).IsAssignableFrom(probe.CaughtType), Is.True);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    /// <summary>暴露默认注册的 scheduler，供测试在真实帧下发起可取消异步操作。</summary>
    public sealed class SchedulerProbeEntry : TryGetMonoEntry
    {
        public ITGTaskScheduler Scheduler { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            Scheduler = host.Get<ITGTaskScheduler>();
        }
    }

    /// <summary>承载一个可取消 Delay 的 await，记录其结局（避免在迭代器测试体内用闭包捕获状态）。</summary>
    public sealed class CancellationProbe
    {
        public bool Caught { get; private set; }
        public bool CompletedNormally { get; private set; }
        public Type CaughtType { get; private set; }

        public async TGTask AwaitDelay(ITGTaskScheduler scheduler, float seconds, TGCancelToken token)
        {
            try
            {
                await scheduler.Delay(seconds, token);
                CompletedNormally = true;
            }
            catch (OperationCanceledException ex)
            {
                Caught = true;
                CaughtType = ex.GetType();
            }
        }

        public async TGTask AwaitFullDelayOverload(
            ITGTaskScheduler scheduler,
            float seconds,
            FramePhase phase,
            TimeMode timeMode,
            TGCancelToken token)
        {
            try
            {
                await scheduler.Delay(seconds, phase, timeMode, token);
                CompletedNormally = true;
            }
            catch (OperationCanceledException ex)
            {
                Caught = true;
                CaughtType = ex.GetType();
            }
        }

        public async TGTask AwaitWaitForFramesOverload(
            ITGTaskScheduler scheduler,
            int frameCount,
            FramePhase phase,
            TGCancelToken token)
        {
            try
            {
                await scheduler.WaitForFrames(frameCount, phase, token);
                CompletedNormally = true;
            }
            catch (OperationCanceledException ex)
            {
                Caught = true;
                CaughtType = ex.GetType();
            }
        }

        public async TGTask AwaitYieldOverload(
            ITGTaskScheduler scheduler,
            FramePhase phase,
            TGCancelToken token)
        {
            try
            {
                await scheduler.Yield(phase, token);
                CompletedNormally = true;
            }
            catch (OperationCanceledException ex)
            {
                Caught = true;
                CaughtType = ex.GetType();
            }
        }
    }
}
