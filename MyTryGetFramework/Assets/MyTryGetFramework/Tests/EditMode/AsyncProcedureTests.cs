using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 7 — IAsyncProcedure + ProcedureModule 异步路径测试（Iter 8 rename 后跟随）。
    /// </summary>
    [TestFixture]
    public class AsyncProcedureTests
    {
        [Test]
        public void AsyncProcedure_SyncCompletePath_NoAsyncPhase()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var proc = new SyncCompletingAsyncProc();
            module.AddProcedure("p1", proc);

            module.Start("p1");

            Assert.IsTrue(proc.OnEnterCalled);
            Assert.IsTrue(proc.OnEnterAsyncCalled);
            Assert.IsFalse(module.IsEntering, "完成的 task 不应让 IsEntering=true");
        }

        [Test]
        public void AsyncProcedure_PendingTask_IsEnteringTrue()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var tcs = new TGTaskCompletionSource();
            var proc = new TcsControlledAsyncProc(tcs);
            module.AddProcedure("p1", proc);

            module.Start("p1");

            Assert.IsTrue(module.IsEntering, "tcs 未完成应让 IsEntering=true");

            tcs.SetResult();
            tcs.Return();

            Assert.IsFalse(module.IsEntering, "tcs 完成后 IsEntering 应回 false");
        }

        [Test]
        public void AsyncProcedure_DuringIsEntering_UpdateSkipped()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var tcs = new TGTaskCompletionSource();
            var proc = new TcsControlledAsyncProc(tcs);
            module.AddProcedure("p1", proc);

            module.Start("p1");

            module.Update(0.016f, 0.016f);
            Assert.AreEqual(0, proc.OnUpdateCount, "异步 Entering 阶段 OnUpdate 应跳过");

            tcs.SetResult();
            tcs.Return();

            module.Update(0.016f, 0.016f);
            Assert.AreEqual(1, proc.OnUpdateCount);
        }

        [Test]
        public void AsyncProcedure_DuringIsEntering_ReplaceThrows()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var tcs = new TGTaskCompletionSource();
            module.AddProcedure("p1", new TcsControlledAsyncProc(tcs));
            module.AddProcedure("p2", new ProcedureBaseStub());

            module.Start("p1");

            Assert.Throws<InvalidOperationException>(() => module.Replace("p2"));
        }

        [Test]
        public void AsyncProcedure_ThrowsInOnEnterAsync_RecordsLastAsyncError()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var proc = new ThrowingAsyncProc();
            module.AddProcedure("p1", proc);

            module.Start("p1");

            Assert.IsNotNull(module.LastAsyncError);
            Assert.IsInstanceOf<InvalidOperationException>(module.LastAsyncError);
            Assert.IsFalse(module.IsEntering, "异常后 IsEntering 必须回 false");
        }

        [Test]
        public void Replace_AsyncExit_GoesThroughOnExitAsync()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var prev = new SyncCompletingAsyncProc();
            var next = new ProcedureBaseStub();
            module.AddProcedure("prev", prev);
            module.AddProcedure("next", next);

            module.Start("prev");
            module.Replace("next");

            Assert.IsTrue(prev.OnExitAsyncCalled);
            Assert.IsTrue(prev.OnExitCalled);
            Assert.AreEqual("next", module.CurrentProcedure);
        }

        [Test]
        public void Replace_PendingExitAsync_IsExitingTrue()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var exitTcs = new TGTaskCompletionSource();
            var prev = new TcsControlledAsyncProc(default, exitTcs);
            module.AddProcedure("prev", prev);
            module.AddProcedure("next", new ProcedureBaseStub());

            module.Start("prev");
            module.Replace("next");

            Assert.IsTrue(module.IsExiting);
            Assert.AreEqual("prev", module.CurrentProcedure);

            exitTcs.SetResult();
            exitTcs.Return();

            Assert.IsFalse(module.IsExiting);
            Assert.AreEqual("next", module.CurrentProcedure);
        }

        [Test]
        public void Stop_AsyncProcedure_TriggersOnExitAsync()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var proc = new SyncCompletingAsyncProc();
            module.AddProcedure("p1", proc);

            module.Start("p1");
            module.Stop();

            Assert.IsTrue(proc.OnExitAsyncCalled);
            Assert.IsTrue(proc.OnExitCalled);
            Assert.IsFalse(module.IsRunning);
        }

        [Test]
        public void Stop_DuringIsEntering_ForceCleanup()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var tcs = new TGTaskCompletionSource();
            var proc = new TcsControlledAsyncProc(tcs);
            module.AddProcedure("p1", proc);

            module.Start("p1");
            Assert.IsTrue(module.IsEntering);

            module.Stop();

            Assert.IsFalse(module.IsRunning);
            Assert.IsFalse(module.IsEntering);
        }

        [Test]
        public void Stop_PendingReplace_DoesNotEnterReplacementAfterExitCompletes()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var exitTcs = new TGTaskCompletionSource();
            var prev = new TrackingExitAsyncProc(exitTcs);
            var next = new EnterCountingProcedure();
            module.AddProcedure("prev", prev);
            module.AddProcedure("next", next);

            module.Start("prev");
            module.Replace("next");
            module.Stop();

            Assert.IsFalse(module.IsRunning);
            exitTcs.SetResult();
            exitTcs.Return();

            Assert.IsTrue(prev.OnExitCalled);
            Assert.AreEqual(0, next.EnterCount);
            Assert.IsFalse(module.IsRunning);
            Assert.IsFalse(module.IsExiting);
        }

        [Test]
        public void Stop_PendingExitAsync_CallsOnExitAfterAsyncCompletes()
        {
            var module = new ProcedureModule();
            module.OnInit(null);

            var exitTcs = new TGTaskCompletionSource();
            var proc = new TrackingExitAsyncProc(exitTcs);
            module.AddProcedure("p1", proc);

            module.Start("p1");
            module.Stop();

            Assert.IsTrue(proc.OnExitAsyncCalled);
            Assert.IsFalse(proc.OnExitCalled);
            Assert.IsTrue(module.IsExiting);
            Assert.IsFalse(module.IsRunning);

            exitTcs.SetResult();
            exitTcs.Return();

            Assert.IsTrue(proc.OnExitCalled);
            Assert.IsFalse(module.IsExiting);
        }

        // ----------------- helpers -----------------

        private sealed class SyncCompletingAsyncProc : AsyncProcedureBase
        {
            public bool OnEnterCalled, OnEnterAsyncCalled, OnExitCalled, OnExitAsyncCalled;

            public override void OnEnter(IProcedureModule module) { OnEnterCalled = true; }
            public override void OnExit(IProcedureModule module) { OnExitCalled = true; }

            public override TGTask OnEnterAsync(IProcedureModule module)
            {
                OnEnterAsyncCalled = true;
                return TGTask.CompletedTask;
            }

            public override TGTask OnExitAsync(IProcedureModule module)
            {
                OnExitAsyncCalled = true;
                return TGTask.CompletedTask;
            }
        }

        /// <summary>
        /// 由外部 tcs 控制完成时机的 async procedure。enterTcs.Task 用于 OnEnterAsync，exitTcs.Task 用于 OnExitAsync。
        /// 若任一 tcs 为 null，对应阶段返回 <see cref="TGTask.CompletedTask"/>（立即完成）。
        /// </summary>
        private sealed class TcsControlledAsyncProc : AsyncProcedureBase
        {
            private readonly TGTaskCompletionSource _enterTcs;
            private readonly TGTaskCompletionSource _exitTcs;

            public int OnUpdateCount;

            public TcsControlledAsyncProc(TGTaskCompletionSource enterTcs = null, TGTaskCompletionSource exitTcs = null)
            {
                _enterTcs = enterTcs;
                _exitTcs = exitTcs;
            }

            public override void OnUpdate(IProcedureModule module, float dt, float udt)
            {
                OnUpdateCount++;
            }

            public override TGTask OnEnterAsync(IProcedureModule module)
                => _enterTcs?.Task ?? TGTask.CompletedTask;

            public override TGTask OnExitAsync(IProcedureModule module)
                => _exitTcs?.Task ?? TGTask.CompletedTask;
        }

        private sealed class ThrowingAsyncProc : AsyncProcedureBase
        {
            public override TGTask OnEnterAsync(IProcedureModule module)
            {
                throw new InvalidOperationException("from-onenterasync");
            }
        }

        private sealed class TrackingExitAsyncProc : AsyncProcedureBase
        {
            private readonly TGTaskCompletionSource _exitTcs;

            public bool OnExitAsyncCalled;
            public bool OnExitCalled;

            public TrackingExitAsyncProc(TGTaskCompletionSource exitTcs)
            {
                _exitTcs = exitTcs;
            }

            public override TGTask OnExitAsync(IProcedureModule module)
            {
                OnExitAsyncCalled = true;
                return _exitTcs.Task;
            }

            public override void OnExit(IProcedureModule module)
            {
                OnExitCalled = true;
            }
        }

        private sealed class EnterCountingProcedure : ProcedureBase
        {
            public int EnterCount;

            public override void OnEnter(IProcedureModule module)
            {
                EnterCount++;
            }
        }

        private sealed class ProcedureBaseStub : ProcedureBase { }
    }
}
