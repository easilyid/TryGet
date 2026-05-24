using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 5 — <see cref="TGTaskScheduler"/> 测试（Iter 8 rename 后跟随）。
    /// V0.6 Iter 8 增加 UnobservedException 钩子测试。
    /// </summary>
    [TestFixture]
    public class TGTaskSchedulerTests
    {
        [Test]
        public void Yield_CompletesOnNextUpdate()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.Yield();

            Assert.IsFalse(task.IsCompleted);
            Assert.AreEqual(1, scheduler.PendingYieldCount);

            scheduler.Update(0.016f, 0.016f);

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(0, scheduler.PendingYieldCount);
        }

        [Test]
        public void Yield_TwoYieldsInSameFrame_BothCompleteNext()
        {
            var scheduler = new TGTaskScheduler();
            var t1 = scheduler.Yield();
            var t2 = scheduler.Yield();

            scheduler.Update(0.016f, 0.016f);

            Assert.IsTrue(t1.IsCompleted);
            Assert.IsTrue(t2.IsCompleted);
        }

        [Test]
        public void Yield_AcrossTwoFrames_OnlyFirstCompletes()
        {
            var scheduler = new TGTaskScheduler();
            var t1 = scheduler.Yield();

            scheduler.Update(0.016f, 0.016f);
            Assert.IsTrue(t1.IsCompleted);

            var t2 = scheduler.Yield();
            Assert.IsFalse(t2.IsCompleted);

            scheduler.Update(0.016f, 0.016f);
            Assert.IsTrue(t2.IsCompleted);
        }

        [Test]
        public void Delay_OnePoint5Seconds_CompletesAfterAccumulatedDt()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.Delay(1.5f);

            scheduler.Update(0.5f, 0.5f);
            Assert.IsFalse(task.IsCompleted);

            scheduler.Update(0.5f, 0.5f);
            Assert.IsFalse(task.IsCompleted);

            scheduler.Update(0.5f, 0.5f);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void Delay_NegativeSeconds_Throws()
        {
            var scheduler = new TGTaskScheduler();
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Delay(-0.1f));
        }

        [Test]
        public void Delay_Zero_BehavesLikeYield()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.Delay(0f);

            Assert.IsFalse(task.IsCompleted);
            scheduler.Update(0.016f, 0.016f);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void WaitForFrames_Three_CompletesAfterThreeUpdates()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.WaitForFrames(3);

            Assert.IsFalse(task.IsCompleted);
            scheduler.Update(0.016f, 0.016f);
            Assert.IsFalse(task.IsCompleted);
            scheduler.Update(0.016f, 0.016f);
            Assert.IsFalse(task.IsCompleted);
            scheduler.Update(0.016f, 0.016f);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void WaitForFrames_Zero_CompletesImmediately()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.WaitForFrames(0);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void WaitForFrames_Negative_Throws()
        {
            var scheduler = new TGTaskScheduler();
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.WaitForFrames(-1));
        }

        [Test]
        public void Shutdown_CancelsAllPendingTasks()
        {
            var scheduler = new TGTaskScheduler();
            var t1 = scheduler.Yield();
            var t2 = scheduler.Delay(5f);
            var t3 = scheduler.WaitForFrames(10);

            scheduler.Shutdown();

            Assert.Throws<OperationCanceledException>(() => t1.GetAwaiter().GetResult());
            Assert.Throws<OperationCanceledException>(() => t2.GetAwaiter().GetResult());
            Assert.Throws<OperationCanceledException>(() => t3.GetAwaiter().GetResult());
        }

        [Test]
        public void AsyncTGTask_AwaitingDelay_ResumesAfterUpdate()
        {
            var scheduler = new TGTaskScheduler();
            bool reachedStage2 = false;

            var outer = AsyncBodyAwaitingDelay(scheduler, () => reachedStage2 = true);

            Assert.IsFalse(reachedStage2);
            Assert.IsFalse(outer.IsCompleted);

            scheduler.Update(0.5f, 0.5f);
            scheduler.Update(0.5f, 0.5f);

            Assert.IsTrue(reachedStage2);
            Assert.IsTrue(outer.IsCompleted);
        }

        [Test]
        public void ModuleHost_RegistersAndDrives()
        {
            var host = new ModuleHost();
            host.Register<ITGTaskScheduler>(new TGTaskScheduler());
            host.Initialize();

            var scheduler = host.Get<ITGTaskScheduler>();
            var task = scheduler.Yield();
            Assert.IsFalse(task.IsCompleted);

            host.Update(0.016f, 0.016f);
            Assert.IsTrue(task.IsCompleted);

            host.Shutdown();
        }

        // ===================== V0.6 Iter 8: UnobservedException 钩子 =====================

        [Test]
        public void UnobservedException_Forget_OnThrowingTask_InvokesHandler()
        {
            Exception captured = null;
            Action<Exception> handler = ex => captured = ex;
            TGTaskScheduler.UnobservedException += handler;

            try
            {
                var task = ThrowingAsyncTask();
                task.Forget();
                Assert.IsNotNull(captured, "Forget 后的异常应进入 UnobservedException 钩子");
                Assert.IsInstanceOf<InvalidOperationException>(captured);
            }
            finally
            {
                TGTaskScheduler.UnobservedException -= handler;
            }
        }

        [Test]
        public void UnobservedException_Forget_OnPendingThrowingTask_InvokesHandlerLater()
        {
            Exception captured = null;
            Action<Exception> handler = ex => captured = ex;
            TGTaskScheduler.UnobservedException += handler;

            try
            {
                var tcs = new TGTaskCompletionSource();
                tcs.Task.Forget();

                Assert.IsNull(captured, "tcs 完成前不应触发钩子");

                tcs.SetException(new InvalidOperationException("deferred"));

                Assert.IsNotNull(captured, "tcs.SetException 后钩子应被触发");
            }
            finally
            {
                TGTaskScheduler.UnobservedException -= handler;
            }
        }

        [Test]
        public void UnobservedException_Forget_OnSuccessfulTask_DoesNotInvokeHandler()
        {
            Exception captured = null;
            Action<Exception> handler = ex => captured = ex;
            TGTaskScheduler.UnobservedException += handler;

            try
            {
                var tcs = new TGTaskCompletionSource();
                tcs.SetResult();
                tcs.Task.Forget();

                Assert.IsNull(captured, "成功完成的 Forget task 不应触发钩子");
            }
            finally
            {
                TGTaskScheduler.UnobservedException -= handler;
            }
        }

        // ---- async helpers ----
        private static async TGTask AsyncBodyAwaitingDelay(ITGTaskScheduler scheduler, Action onReached)
        {
            await scheduler.Delay(1f);
            onReached();
        }

        private static async TGTask ThrowingAsyncTask()
        {
            await default(NoopAwaitable);
            throw new InvalidOperationException("forget-this");
        }

        private readonly struct NoopAwaitable
        {
            public Awaiter GetAwaiter() => default;
            public readonly struct Awaiter : System.Runtime.CompilerServices.ICriticalNotifyCompletion
            {
                public bool IsCompleted => true;
                public void GetResult() { }
                public void OnCompleted(Action c) => c?.Invoke();
                public void UnsafeOnCompleted(Action c) => c?.Invoke();
            }
        }
    }
}
