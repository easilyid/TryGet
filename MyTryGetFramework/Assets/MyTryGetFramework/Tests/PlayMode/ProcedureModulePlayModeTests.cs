using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Async;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// ProcedureModule 在真实 Unity 帧循环下的端到端验证（区别于 EditMode 的手动 Update 驱动）。
    ///
    /// 覆盖用户真实接入流程状态机的高频路径：
    /// - 同步 Procedure：OnEnter → 多帧 OnUpdate → Replace 触发 OnExit，旧 Procedure 不再 Update；
    /// - 异步 Procedure（<see cref="AsyncProcedureBase"/>）：OnEnterAsync 内 await scheduler.Delay 真实跨帧，
    ///   进入期间 IsEntering=true 且 OnUpdate 被跳过，完成后才开始驱动 OnUpdate；
    /// - 栈语义：Push 暂停下层（OnPause）、Pop 恢复（OnResume），仅栈顶收 OnUpdate；
    /// - ADR-0021 取消 scope：Procedure 离栈（OnExit）自动取消其名下 pending 异步操作，await 抛
    ///   <see cref="TGTaskAbortException"/>——这是取消模型在真实运行时环境的端到端验证。
    /// </summary>
    [TestFixture]
    public sealed class ProcedureModulePlayModeTests
    {
        [UnityTest]
        public IEnumerator SyncProcedure_EnterUpdatesAcrossFrames_ReplaceExitsAndStopsUpdating()
        {
            var gameObject = new GameObject(nameof(SyncProcedure_EnterUpdatesAcrossFrames_ReplaceExitsAndStopsUpdating));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null; // Awake → CreateHost + Setup + Initialize

                var procedures = entry.Procedures;
                var a = new CountingProcedure();
                var b = new CountingProcedure();
                procedures.AddProcedure("A", a);
                procedures.AddProcedure("B", b);

                procedures.Start("A");
                Assert.That(a.EnterCount, Is.EqualTo(1));
                Assert.That(procedures.CurrentProcedure, Is.EqualTo("A"));

                for (int i = 0; i < 3; i++)
                    yield return null;
                Assert.That(a.UpdateCount, Is.GreaterThanOrEqualTo(3), "A 应在真实帧下持续收到 OnUpdate");

                procedures.Replace("B");
                Assert.That(a.ExitCount, Is.EqualTo(1));
                Assert.That(b.EnterCount, Is.EqualTo(1));
                Assert.That(procedures.CurrentProcedure, Is.EqualTo("B"));

                int aUpdatesAtExit = a.UpdateCount;
                for (int i = 0; i < 3; i++)
                    yield return null;
                Assert.That(a.UpdateCount, Is.EqualTo(aUpdatesAtExit), "已退出的 A 不应再收到 OnUpdate");
                Assert.That(b.UpdateCount, Is.GreaterThanOrEqualTo(3), "新栈顶 B 应收到 OnUpdate");

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
        public IEnumerator AsyncProcedure_EnterAsyncPendingSkipsUpdate_ThenDrivesUpdate()
        {
            var gameObject = new GameObject(nameof(AsyncProcedure_EnterAsyncPendingSkipsUpdate_ThenDrivesUpdate));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var asyncProc = new AsyncEnterProcedure(0.05f);
                procedures.AddProcedure("Async", asyncProc);

                procedures.Start("Async");
                yield return null;

                Assert.That(asyncProc.EnterAsyncStarted, Is.True);
                Assert.That(procedures.IsEntering, Is.True, "OnEnterAsync 未完成期间应处于进入中");
                Assert.That(asyncProc.UpdateCount, Is.EqualTo(0), "进入中应跳过 OnUpdate");

                for (int i = 0; i < 120 && procedures.IsEntering; i++)
                    yield return null;

                Assert.That(procedures.IsEntering, Is.False, "Delay 完成后应结束进入态");
                Assert.That(asyncProc.EnterAsyncCompleted, Is.True);

                int updatesAtEnterDone = asyncProc.UpdateCount;
                for (int i = 0; i < 3; i++)
                    yield return null;
                Assert.That(asyncProc.UpdateCount, Is.GreaterThan(updatesAtEnterDone),
                    "进入完成后应开始驱动 OnUpdate");

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
        public IEnumerator ProcedureBase_CancelScope_AutoCancelsPendingAsyncOnExit()
        {
            var gameObject = new GameObject(nameof(ProcedureBase_CancelScope_AutoCancelsPendingAsyncOnExit));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var cancelProc = new CancelScopeProcedure();
                var next = new CountingProcedure();
                procedures.AddProcedure("Cancel", cancelProc);
                procedures.AddProcedure("Next", next);

                procedures.Start("Cancel");
                yield return null;
                Assert.That(cancelProc.PendingStarted, Is.True);
                Assert.That(cancelProc.PendingCanceled, Is.False, "尚未离栈，pending 不应被取消");

                procedures.Replace("Next"); // 离开 Cancel → OnExit → base.CancelScope
                for (int i = 0; i < 10 && !cancelProc.PendingCanceled; i++)
                    yield return null;

                Assert.That(cancelProc.PendingCanceled, Is.True,
                    "离栈应自动取消该 Procedure 名下 pending 异步操作（ADR-0021 取消 scope）");
                Assert.That(cancelProc.PendingCompletedNormally, Is.False);
                Assert.That(cancelProc.CanceledExceptionType, Is.Not.Null);
                Assert.That(typeof(OperationCanceledException).IsAssignableFrom(cancelProc.CanceledExceptionType), Is.True,
                    "取消应以 OperationCanceledException（含子类）完成——用户应捕获此基类，与框架既有取消契约一致");

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
        public IEnumerator Procedure_OnEnterPublishesEvent_HandlerModuleReceivesInRealPlayModeHost()
        {
            var gameObject = new GameObject(nameof(Procedure_OnEnterPublishesEvent_HandlerModuleReceivesInRealPlayModeHost));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureEventHostEntry>();
                yield return null;

                var bus = entry.EventBus;
                var receiver = entry.Receiver;
                var procedure = new ProcedureEventPublishingProcedure("boot");
                entry.Procedures.AddProcedure("boot", procedure);

                var transition = entry.Procedures.Start("boot");

                Assert.That(transition.IsCompleted, Is.True,
                    "A synchronous Procedure.OnEnter publish should complete its transition immediately.");
                Assert.DoesNotThrow(() => transition.GetAwaiter().GetResult());
                Assert.That(procedure.EnterCount, Is.EqualTo(1));
                Assert.That(receiver.ReceiveCount, Is.EqualTo(1),
                    "A module subscribed during TryGetMonoEntry boot should receive Procedure.OnEnter events from the same Host.EventModule.");
                Assert.That(receiver.LastId, Is.EqualTo("boot"));
                Assert.That(bus.GetSubscriberCount<ProcedureEnteredPlayModeEvent>(), Is.EqualTo(1));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
                Assert.That(receiver.ShutdownCount, Is.EqualTo(1));
                Assert.That(bus.GetSubscriberCount<ProcedureEnteredPlayModeEvent>(), Is.EqualTo(0),
                    "TryGetMonoEntry.OnDestroy should shut down the receiver module and remove its EventModule subscription.");

                int countAfterShutdown = receiver.ReceiveCount;
                bus.Publish(new ProcedureEnteredPlayModeEvent { Id = "after-shutdown" });
                Assert.That(receiver.ReceiveCount, Is.EqualTo(countAfterShutdown),
                    "A receiver module must not keep receiving events after its Host has shut down.");
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Procedure_EventScopeDisposedOnStop_UnsubscribesInRealPlayModeHost()
        {
            var gameObject = new GameObject(nameof(Procedure_EventScopeDisposedOnStop_UnsubscribesInRealPlayModeHost));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureEventHostEntry>();
                yield return null;

                var bus = entry.EventBus;
                var procedure = new ScopedEventProcedure();
                entry.Procedures.AddProcedure("scoped", procedure);

                entry.Procedures.Start("scoped").GetAwaiter().GetResult();

                Assert.That(bus.GetSubscriberCount<ScopedProcedurePlayModeEvent>(), Is.EqualTo(1),
                    "Procedure.OnEnter should be able to create an EventScope subscription on the real Host.EventModule.");

                bus.Publish(new ScopedProcedurePlayModeEvent { Value = 7 });
                Assert.That(procedure.Total, Is.EqualTo(7));

                entry.Procedures.Stop();

                Assert.That(entry.Procedures.IsRunning, Is.False);
                Assert.That(bus.GetSubscriberCount<ScopedProcedurePlayModeEvent>(), Is.EqualTo(0),
                    "Procedure.OnExit should dispose its owner-managed EventScope when Stop removes it from the stack.");

                bus.Publish(new ScopedProcedurePlayModeEvent { Value = 11 });
                Assert.That(procedure.Total, Is.EqualTo(7),
                    "Events published after Stop must not reach the stopped procedure.");

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
        public IEnumerator Stop_CancelsCurrentProcedurePendingScope_InRealPlayModeFrames()
        {
            var gameObject = new GameObject(nameof(Stop_CancelsCurrentProcedurePendingScope_InRealPlayModeFrames));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var cancelProc = new CancelScopeProcedure();
                procedures.AddProcedure("Cancel", cancelProc);

                procedures.Start("Cancel");
                yield return null;
                Assert.That(cancelProc.PendingStarted, Is.True);
                Assert.That(cancelProc.PendingCanceled, Is.False);
                Assert.That(procedures.IsRunning, Is.True);

                procedures.Stop();
                for (int i = 0; i < 10 && !cancelProc.PendingCanceled; i++)
                    yield return null;

                Assert.That(procedures.IsRunning, Is.False);
                Assert.That(procedures.StackDepth, Is.EqualTo(0));
                Assert.That(cancelProc.PendingCanceled, Is.True,
                    "Stop 应触发当前 Procedure 的 OnExit/base.CancelScope，取消真实帧中 pending 的 TGTask。");
                Assert.That(cancelProc.PendingCompletedNormally, Is.False);

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
        public IEnumerator Stop_AsyncExitCompletesBeforeOnExitCancelsPendingScope_InRealPlayModeFrames()
        {
            var gameObject = new GameObject(nameof(Stop_AsyncExitCompletesBeforeOnExitCancelsPendingScope_InRealPlayModeFrames));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var proc = new AsyncExitCancelScopeProcedure(0.05f);
                procedures.AddProcedure("AsyncExit", proc);

                procedures.Start("AsyncExit");
                yield return null;

                Assert.That(proc.PendingStarted, Is.True);
                Assert.That(proc.PendingCanceled, Is.False);
                Assert.That(procedures.IsRunning, Is.True);

                procedures.Stop();

                Assert.That(procedures.IsRunning, Is.False);
                Assert.That(procedures.IsExiting, Is.True,
                    "Stop 触发异步 OnExitAsync 时，应保持退出中状态直到 OnExitAsync 完成。");
                Assert.That(proc.ExitAsyncStarted, Is.True);
                Assert.That(proc.ExitCount, Is.EqualTo(0),
                    "OnExitAsync 完成前不应调用同步 OnExit，也就不应提前 CancelScope。");
                Assert.That(proc.PendingCanceled, Is.False);

                for (int i = 0; i < 120 && !proc.PendingCanceled; i++)
                    yield return null;

                Assert.That(proc.ExitAsyncCompleted, Is.True,
                    "OnExitAsync 应由真实 Unity 帧中的默认 Scheduler.Delay 推进完成。");
                Assert.That(proc.ExitCount, Is.EqualTo(1));
                Assert.That(proc.PendingCanceled, Is.True,
                    "OnExitAsync 完成后应调用 OnExit/base.CancelScope，取消该 Procedure 名下 pending TGTask。");
                Assert.That(proc.PendingCompletedNormally, Is.False);
                Assert.That(procedures.IsExiting, Is.False);
                Assert.That(procedures.StackDepth, Is.EqualTo(0));

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
        public IEnumerator Shutdown_AsyncEnterInProgress_CancelsTransitionAndProcedureScope()
        {
            var gameObject = new GameObject(nameof(Shutdown_AsyncEnterInProgress_CancelsTransitionAndProcedureScope));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var proc = new AsyncEnterShutdownProcedure();
                procedures.AddProcedure("Loading", proc);

                var transition = procedures.Start("Loading");
                yield return null;

                Assert.That(proc.EnterAsyncStarted, Is.True);
                Assert.That(proc.ScopePendingStarted, Is.True);
                Assert.That(procedures.IsEntering, Is.True);
                Assert.That(transition.IsCompleted, Is.False);

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(transition.IsCompleted, Is.True,
                    "Shutdown 应取消正在进行的异步进入 transition，避免 await 者永久挂起。");
                Assert.Throws<OperationCanceledException>(() => transition.GetAwaiter().GetResult());
                Assert.That(proc.ExitCount, Is.EqualTo(1),
                    "Shutdown 应对已压入栈但仍在异步进入中的 Procedure 调用同步 OnExit。");

                for (int i = 0; i < 10 && !proc.ScopePendingCanceled; i++)
                    yield return null;

                Assert.That(proc.ScopePendingCanceled, Is.True,
                    "Shutdown 期间的 OnExit/base.CancelScope 应取消该 Procedure 已登记的 pending TGTask。");
                Assert.That(proc.ScopePendingCompletedNormally, Is.False);
                Assert.That(proc.EnterAsyncCompleted, Is.False);
                Assert.That(entry == null || !entry.HasHost, Is.True);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PersistentTryGetMonoEntry_SceneUnload_AsyncEnterContinuesAndDrivesUpdate()
        {
            var scene = SceneManager.CreateScene("TryGet_ProcedurePersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(PersistentTryGetMonoEntry_SceneUnload_AsyncEnterContinuesAndDrivesUpdate));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var proc = new AsyncEnterFramesProcedure(30);
                procedures.AddProcedure("Loading", proc);

                var transition = procedures.Start("Loading");
                yield return null;

                Assert.That(proc.EnterAsyncStarted, Is.True);
                Assert.That(proc.EnterAsyncCompleted, Is.False,
                    "测试前提：卸载原场景前，异步进入仍应处于 pending 状态。");
                Assert.That(procedures.IsEntering, Is.True);
                Assert.That(transition.IsCompleted, Is.False);
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(entry.HasHost, Is.True);
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                for (int i = 0; i < 120 && procedures.IsEntering; i++)
                    yield return null;

                Assert.That(procedures.IsEntering, Is.False,
                    "持久 ProcedureHostEntry 卸载原场景后，异步进入应继续由默认 Scheduler 推进完成。");
                Assert.That(proc.EnterAsyncCompleted, Is.True);
                Assert.That(transition.IsCompleted, Is.True);
                Assert.DoesNotThrow(() => transition.GetAwaiter().GetResult(),
                    "持久 Entry 卸载原场景不应取消正在进行的 Procedure transition。");

                int updatesAtEnterDone = proc.UpdateCount;
                for (int i = 0; i < 3; i++)
                    yield return null;

                Assert.That(proc.UpdateCount, Is.GreaterThan(updatesAtEnterDone),
                    "异步进入完成后，持久 Entry 应继续通过真实 Unity Update 驱动当前 Procedure。");

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
        public IEnumerator Stack_PushPausesUnder_PopResumesUnder()
        {
            var gameObject = new GameObject(nameof(Stack_PushPausesUnder_PopResumesUnder));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var bottom = new CountingProcedure();
                var top = new CountingProcedure();
                procedures.AddProcedure("Bottom", bottom);
                procedures.AddProcedure("Top", top);

                procedures.Start("Bottom");
                for (int i = 0; i < 2; i++)
                    yield return null;
                Assert.That(bottom.UpdateCount, Is.GreaterThanOrEqualTo(2));

                procedures.Push("Top");
                Assert.That(bottom.PauseCount, Is.EqualTo(1));
                Assert.That(top.EnterCount, Is.EqualTo(1));
                Assert.That(procedures.StackDepth, Is.EqualTo(2));

                int bottomUpdatesWhilePaused = bottom.UpdateCount;
                for (int i = 0; i < 3; i++)
                    yield return null;
                Assert.That(bottom.UpdateCount, Is.EqualTo(bottomUpdatesWhilePaused),
                    "被 Push 暂停的下层不应收到 OnUpdate");
                Assert.That(top.UpdateCount, Is.GreaterThanOrEqualTo(3), "栈顶应收到 OnUpdate");

                procedures.Pop();
                Assert.That(top.ExitCount, Is.EqualTo(1));
                Assert.That(bottom.ResumeCount, Is.EqualTo(1));
                Assert.That(procedures.StackDepth, Is.EqualTo(1));

                int topUpdatesAtPop = top.UpdateCount;
                int bottomUpdatesAtResume = bottom.UpdateCount;
                for (int i = 0; i < 3; i++)
                    yield return null;
                Assert.That(top.UpdateCount, Is.EqualTo(topUpdatesAtPop), "已 Pop 的栈顶不应再收到 OnUpdate");
                Assert.That(bottom.UpdateCount, Is.GreaterThan(bottomUpdatesAtResume), "恢复的下层应重新收到 OnUpdate");

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
        public IEnumerator Pop_CancelsPoppedProcedurePendingScope_AndResumesUnder()
        {
            var gameObject = new GameObject(nameof(Pop_CancelsPoppedProcedurePendingScope_AndResumesUnder));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<ProcedureHostEntry>();
                yield return null;

                var procedures = entry.Procedures;
                var bottom = new CountingProcedure();
                var top = new CancelScopeProcedure();
                procedures.AddProcedure("Bottom", bottom);
                procedures.AddProcedure("Top", top);

                procedures.Start("Bottom");
                for (int i = 0; i < 2; i++)
                    yield return null;

                procedures.Push("Top");
                yield return null;
                Assert.That(top.PendingStarted, Is.True);
                Assert.That(top.PendingCanceled, Is.False);
                Assert.That(bottom.PauseCount, Is.EqualTo(1));
                Assert.That(procedures.CurrentProcedure, Is.EqualTo("Top"));

                procedures.Pop();
                for (int i = 0; i < 10 && !top.PendingCanceled; i++)
                    yield return null;

                Assert.That(top.PendingCanceled, Is.True,
                    "Pop 应触发被弹出 Procedure 的 OnExit/base.CancelScope，取消其 pending TGTask。");
                Assert.That(top.PendingCompletedNormally, Is.False);
                Assert.That(bottom.ResumeCount, Is.EqualTo(1));
                Assert.That(procedures.CurrentProcedure, Is.EqualTo("Bottom"));
                Assert.That(procedures.StackDepth, Is.EqualTo(1));

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

    public sealed class AsyncEnterFramesProcedure : AsyncProcedureBase
    {
        private readonly int _frames;

        public AsyncEnterFramesProcedure(int frames)
        {
            _frames = frames;
        }

        public bool EnterAsyncStarted { get; private set; }
        public bool EnterAsyncCompleted { get; private set; }
        public int UpdateCount { get; private set; }

        public override async TGTask OnEnterAsync(IProcedureModule module)
        {
            EnterAsyncStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            await scheduler.WaitForFrames(_frames);
            EnterAsyncCompleted = true;
        }

        public override void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime) => UpdateCount++;
    }

    public struct ProcedureEnteredPlayModeEvent
    {
        public string Id;
    }

    public struct ScopedProcedurePlayModeEvent
    {
        public int Value;
    }

    public interface IProcedureEventReceiverModule : IModule
    {
    }

    public sealed class ProcedureEventReceiverModule : IProcedureEventReceiverModule
    {
        private IEventModule _bus;

        public int Priority => -10;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int ReceiveCount { get; private set; }
        public string LastId { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
            _bus = host.EventModule;
            _bus.Subscribe<ProcedureEnteredPlayModeEvent>(OnProcedureEntered);
        }

        public void Shutdown()
        {
            ShutdownCount++;
            _bus?.Unsubscribe<ProcedureEnteredPlayModeEvent>(OnProcedureEntered);
            _bus = null;
        }

        private void OnProcedureEntered(ProcedureEnteredPlayModeEvent evt)
        {
            ReceiveCount++;
            LastId = evt.Id;
        }
    }

    public sealed class ProcedureEventPublishingProcedure : ProcedureBase
    {
        private readonly string _id;

        public ProcedureEventPublishingProcedure(string id)
        {
            _id = id;
        }

        public int EnterCount { get; private set; }

        public override void OnEnter(IProcedureModule module)
        {
            EnterCount++;
            module.Host.EventModule.Publish(new ProcedureEnteredPlayModeEvent { Id = _id });
        }
    }

    public sealed class ScopedEventProcedure : ProcedureBase
    {
        private EventScope _scope;

        public int Total { get; private set; }

        public override void OnEnter(IProcedureModule module)
        {
            var bus = module.Host.EventModule;
            _scope = bus.CreateScope();
            bus.Subscribe<ScopedProcedurePlayModeEvent>(OnScopedEvent, _scope);
        }

        public override void OnExit(IProcedureModule module)
        {
            base.OnExit(module);
            _scope?.Dispose();
            _scope = null;
        }

        private void OnScopedEvent(ScopedProcedurePlayModeEvent evt)
        {
            Total += evt.Value;
        }
    }

    public sealed class AsyncEnterShutdownProcedure : AsyncProcedureBase
    {
        public bool EnterAsyncStarted { get; private set; }
        public bool EnterAsyncCompleted { get; private set; }
        public bool ScopePendingStarted { get; private set; }
        public bool ScopePendingCanceled { get; private set; }
        public bool ScopePendingCompletedNormally { get; private set; }
        public int ExitCount { get; private set; }

        public override void OnEnter(IProcedureModule module)
        {
            RunScopePending(module).Forget();
        }

        public override async TGTask OnEnterAsync(IProcedureModule module)
        {
            EnterAsyncStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            await scheduler.Delay(100f);
            EnterAsyncCompleted = true;
        }

        public override void OnExit(IProcedureModule module)
        {
            ExitCount++;
            base.OnExit(module);
        }

        private async TGTask RunScopePending(IProcedureModule module)
        {
            ScopePendingStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            try
            {
                await scheduler.Delay(100f, CancelToken);
                ScopePendingCompletedNormally = true;
            }
            catch (OperationCanceledException)
            {
                ScopePendingCanceled = true;
            }
        }
    }

    public sealed class AsyncExitCancelScopeProcedure : AsyncProcedureBase
    {
        private readonly float _exitDelaySeconds;

        public AsyncExitCancelScopeProcedure(float exitDelaySeconds)
        {
            _exitDelaySeconds = exitDelaySeconds;
        }

        public bool PendingStarted { get; private set; }
        public bool PendingCanceled { get; private set; }
        public bool PendingCompletedNormally { get; private set; }
        public bool ExitAsyncStarted { get; private set; }
        public bool ExitAsyncCompleted { get; private set; }
        public int ExitCount { get; private set; }

        public override void OnEnter(IProcedureModule module)
        {
            RunPending(module).Forget();
        }

        public override async TGTask OnExitAsync(IProcedureModule module)
        {
            ExitAsyncStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            await scheduler.Delay(_exitDelaySeconds);
            ExitAsyncCompleted = true;
        }

        public override void OnExit(IProcedureModule module)
        {
            ExitCount++;
            base.OnExit(module);
        }

        private async TGTask RunPending(IProcedureModule module)
        {
            PendingStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            try
            {
                await scheduler.Delay(100f, CancelToken);
                PendingCompletedNormally = true;
            }
            catch (OperationCanceledException)
            {
                PendingCanceled = true;
            }
        }
    }

    /// <summary>注册一个空 <see cref="ProcedureModule"/> 的入口；测试在 Awake 完成后通过 <see cref="Procedures"/> 驱动状态机。</summary>
    public sealed class ProcedureHostEntry : TryGetMonoEntry
    {
        public IProcedureModule Procedures { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            var module = new ProcedureModule();
            host.Register<IProcedureModule>(module);
            Procedures = module;
        }
    }

    public sealed class ProcedureEventHostEntry : TryGetMonoEntry
    {
        public ProcedureEventReceiverModule Receiver { get; } = new ProcedureEventReceiverModule();
        public IProcedureModule Procedures { get; private set; }
        public IEventModule EventBus { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            var procedures = new ProcedureModule();
            EventBus = host.EventModule;
            host.Register<IProcedureEventReceiverModule>(Receiver);
            host.Register<IProcedureModule>(procedures);
            Procedures = procedures;
        }
    }

    public sealed class PersistentProcedureHostEntry : TryGetMonoEntry
    {
        public IProcedureModule Procedures { get; private set; }
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            var module = new ProcedureModule();
            host.Register<IProcedureModule>(module);
            Procedures = module;
        }
    }

    /// <summary>同步 Procedure：记录各生命周期回调次数。</summary>
    public sealed class CountingProcedure : ProcedureBase
    {
        public int EnterCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int ExitCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }

        public override void OnEnter(IProcedureModule module) => EnterCount++;
        public override void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime) => UpdateCount++;
        public override void OnExit(IProcedureModule module)
        {
            ExitCount++;
            base.OnExit(module);
        }
        public override void OnPause(IProcedureModule module) => PauseCount++;
        public override void OnResume(IProcedureModule module) => ResumeCount++;
    }

    /// <summary>异步 Procedure：OnEnterAsync 内 await scheduler.Delay 模拟真实跨帧加载。</summary>
    public sealed class AsyncEnterProcedure : AsyncProcedureBase
    {
        private readonly float _delaySeconds;

        public AsyncEnterProcedure(float delaySeconds)
        {
            _delaySeconds = delaySeconds;
        }

        public bool EnterAsyncStarted { get; private set; }
        public bool EnterAsyncCompleted { get; private set; }
        public int UpdateCount { get; private set; }

        public override async TGTask OnEnterAsync(IProcedureModule module)
        {
            EnterAsyncStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            await scheduler.Delay(_delaySeconds);
            EnterAsyncCompleted = true;
        }

        public override void OnUpdate(IProcedureModule module, float deltaTime, float unscaledDeltaTime) => UpdateCount++;
    }

    /// <summary>
    /// 取消 scope Procedure：进入时发起一个长延迟 pending（绑定 <see cref="ProcedureBase.CancelToken"/>），
    /// 离栈时基类 OnExit 调 CancelScope 自动取消，await 处以 <see cref="TGTaskAbortException"/> 结束。
    /// </summary>
    public sealed class CancelScopeProcedure : ProcedureBase
    {
        public bool PendingStarted { get; private set; }
        public bool PendingCanceled { get; private set; }
        public bool PendingCompletedNormally { get; private set; }
        public Type CanceledExceptionType { get; private set; }

        public override void OnEnter(IProcedureModule module)
        {
            RunPending(module).Forget();
        }

        private async TGTask RunPending(IProcedureModule module)
        {
            PendingStarted = true;
            var scheduler = module.Host.Get<ITGTaskScheduler>();
            try
            {
                // 长延迟：正常不会自然完成，只能由离栈时的 CancelScope 取消
                await scheduler.Delay(100f, CancelToken);
                PendingCompletedNormally = true;
            }
            catch (OperationCanceledException ex)
            {
                PendingCanceled = true;
                CanceledExceptionType = ex.GetType();
            }
        }
    }
}
