using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// C4：ProcedureModule 可 await 切换（Start/Push/Pop/Replace 返回 transition TGTask）测试。
    /// 同步流程立即完成；异步流程在 OnEnterAsync/OnExitAsync 全链完成时完成；错误经 await 抛出
    /// （同时仍写入 LastAsyncError 兼容）。
    /// </summary>
    [TestFixture]
    public class ProcedureTransitionTests
    {
        // ---------- 同步流程：transition 立即完成 ----------

        [Test]
        public void Start_SyncProcedure_TransitionCompletesImmediately()
        {
            var module = NewModule(out _);
            module.AddProcedure("p1", new TrackingProc());

            var t = module.Start("p1");

            Assert.IsTrue(t.IsCompleted, "同步流程 Start 应立即完成");
            Assert.DoesNotThrow(() => t.GetAwaiter().GetResult());
            Assert.AreEqual("p1", module.CurrentProcedure);
        }

        [Test]
        public void Push_SyncProcedure_TransitionCompletesImmediately()
        {
            var module = NewModule(out _);
            module.AddProcedure("p1", new TrackingProc());
            module.AddProcedure("p2", new TrackingProc());
            module.Start("p1");

            var t = module.Push("p2");

            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual("p2", module.CurrentProcedure);
            Assert.AreEqual(2, module.StackDepth);
        }

        [Test]
        public void Pop_SyncProcedure_CompletesAndResumesUnder()
        {
            var module = NewModule(out _);
            var under = new TrackingProc();
            module.AddProcedure("under", under);
            module.AddProcedure("top", new TrackingProc());
            module.Start("under");
            module.Push("top");

            var t = module.Pop();

            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual("under", module.CurrentProcedure);
            Assert.AreEqual(1, under.ResumeCount, "Pop 完成时下层应已 OnResume");
        }

        [Test]
        public void Replace_SyncProcedure_TransitionCompletesImmediately()
        {
            var module = NewModule(out _);
            module.AddProcedure("p1", new TrackingProc());
            module.AddProcedure("p2", new TrackingProc());
            module.Start("p1");

            var t = module.Replace("p2");

            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual("p2", module.CurrentProcedure);
            Assert.AreEqual(1, module.StackDepth);
        }

        // ---------- 异步流程：transition 跟随 enter/exit 完成 ----------

        [Test]
        public void Start_AsyncProcedure_PendingUntilEnterAsyncCompletes()
        {
            var module = NewModule(out _);
            var enterTcs = new TGTaskCompletionSource();
            module.AddProcedure("p1", new TcsAsyncProc(enterTcs, null));

            var t = module.Start("p1");

            Assert.IsFalse(t.IsCompleted, "OnEnterAsync 未完成，transition 应 pending");
            enterTcs.SetResult();
            enterTcs.Return();
            Assert.IsTrue(t.IsCompleted, "OnEnterAsync 完成后 transition 应完成");
            Assert.DoesNotThrow(() => t.GetAwaiter().GetResult());
        }

        [Test]
        public void Push_AsyncProcedure_PendingUntilEnterAsyncCompletes()
        {
            var module = NewModule(out _);
            module.AddProcedure("p1", new TrackingProc());
            var enterTcs = new TGTaskCompletionSource();
            module.AddProcedure("p2", new TcsAsyncProc(enterTcs, null));
            module.Start("p1");

            var t = module.Push("p2");

            Assert.IsFalse(t.IsCompleted);
            enterTcs.SetResult();
            enterTcs.Return();
            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual("p2", module.CurrentProcedure);
        }

        [Test]
        public void Pop_AsyncExit_PendingUntilExitAsyncCompletes()
        {
            var module = NewModule(out _);
            var under = new TrackingProc();
            module.AddProcedure("under", under);
            var exitTcs = new TGTaskCompletionSource();
            module.AddProcedure("top", new TcsAsyncProc(null, exitTcs));
            module.Start("under");
            module.Push("top");

            var t = module.Pop();

            Assert.IsFalse(t.IsCompleted, "OnExitAsync 未完成，Pop transition 应 pending");
            Assert.AreEqual(0, under.ResumeCount, "exit 未完成时不应 resume 下层");
            exitTcs.SetResult();
            exitTcs.Return();
            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual(1, under.ResumeCount, "exit 完成后才 resume 下层");
            Assert.AreEqual("under", module.CurrentProcedure);
        }

        [Test]
        public void Replace_AsyncExitThenAsyncEnter_CompletesOnlyAfterBoth()
        {
            var module = NewModule(out _);
            var exitTcs = new TGTaskCompletionSource();
            var enterTcs = new TGTaskCompletionSource();
            module.AddProcedure("prev", new TcsAsyncProc(null, exitTcs)); // enter 同步、exit 异步
            module.AddProcedure("next", new TcsAsyncProc(enterTcs, null)); // enter 异步
            module.Start("prev");

            var t = module.Replace("next");

            Assert.IsFalse(t.IsCompleted, "exit 异步未完成");
            exitTcs.SetResult();
            exitTcs.Return();
            Assert.IsFalse(t.IsCompleted, "exit 完成但 enter 异步未完成，transition 仍 pending");
            enterTcs.SetResult();
            enterTcs.Return();
            Assert.IsTrue(t.IsCompleted, "exit + enter 全链完成后 transition 才完成");
            Assert.AreEqual("next", module.CurrentProcedure);
        }

        // ---------- 错误：经 await 抛出 + 兼容 LastAsyncError ----------

        [Test]
        public void Start_AsyncEnterThrows_TransitionTaskThrows_AndLastAsyncErrorSet()
        {
            var module = NewModule(out _);
            module.AddProcedure("p1", new ThrowingEnterAsyncProc());

            var t = module.Start("p1");

            Assert.IsTrue(t.IsCompleted, "同步抛出路径 transition 立即完成（FromException）");
            var ex = Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult());
            Assert.AreEqual("enter-boom", ex.Message);
            Assert.IsNotNull(module.LastAsyncError, "错误仍写入 LastAsyncError 兼容通道");
            Assert.IsInstanceOf<InvalidOperationException>(module.LastAsyncError);
        }

        [Test]
        public void Replace_AsyncExitThrows_TransitionThrows_DoesNotEnterTarget()
        {
            var module = NewModule(out _);
            module.AddProcedure("prev", new ThrowingExitAsyncProc());
            var nextEnter = new TrackingProc();
            module.AddProcedure("next", nextEnter);
            module.Start("prev");

            var t = module.Replace("next");

            Assert.IsTrue(t.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult());
            Assert.AreEqual(0, nextEnter.EnterCount, "exit 失败时不应进入替换目标");
        }

        // ---------- helpers ----------

        private static ProcedureModule NewModule(out ProcedureModule m)
        {
            m = new ProcedureModule();
            m.OnInit(null);
            return m;
        }

        private sealed class TrackingProc : ProcedureBase
        {
            public int EnterCount, ResumeCount;
            public override void OnEnter(IProcedureModule module) => EnterCount++;
            public override void OnResume(IProcedureModule module) => ResumeCount++;
        }

        /// <summary>enterTcs/exitTcs 为 null 时对应阶段返回 CompletedTask（立即完成）。</summary>
        private sealed class TcsAsyncProc : AsyncProcedureBase
        {
            private readonly TGTaskCompletionSource _enterTcs;
            private readonly TGTaskCompletionSource _exitTcs;

            public TcsAsyncProc(TGTaskCompletionSource enterTcs, TGTaskCompletionSource exitTcs)
            {
                _enterTcs = enterTcs;
                _exitTcs = exitTcs;
            }

            public override TGTask OnEnterAsync(IProcedureModule module)
                => _enterTcs?.Task ?? TGTask.CompletedTask;

            public override TGTask OnExitAsync(IProcedureModule module)
                => _exitTcs?.Task ?? TGTask.CompletedTask;
        }

        private sealed class ThrowingEnterAsyncProc : AsyncProcedureBase
        {
            public override TGTask OnEnterAsync(IProcedureModule module)
                => throw new InvalidOperationException("enter-boom");
        }

        private sealed class ThrowingExitAsyncProc : AsyncProcedureBase
        {
            public override TGTask OnExitAsync(IProcedureModule module)
                => throw new InvalidOperationException("exit-boom");
        }
    }
}
