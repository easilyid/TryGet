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
        public void ModuleSystem_RegistersAndDrives()
        {
            var host = new ModuleSystem();
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

        // ============= V2.2 Phase-aware Tests =============

        [Test]
        public void Yield_WithPhase_LateUpdate_RunsInLateUpdate()
        {
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            bool executed = false;
            AsyncYieldPhase(scheduler, FramePhase.LateUpdate, () => executed = true);

            // EarlyUpdate, FixedUpdate, Update 都不应执行
            host.EarlyUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);

            host.FixedUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);

            host.Update(0.016f, 0.016f);
            Assert.IsFalse(executed);

            // LateUpdate 应该执行
            host.LateUpdate(0.016f, 0.016f);
            Assert.IsTrue(executed);
        }

        [Test]
        public void Yield_WithPhase_EndOfFrame_RunsInEndOfFrame()
        {
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            bool executed = false;
            AsyncYieldPhase(scheduler, FramePhase.EndOfFrame, () => executed = true);

            // 前面所有 Phase 都不应执行
            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);

            // EndOfFrame 应该执行
            host.EndOfFrame(0.016f, 0.016f);
            Assert.IsTrue(executed);
        }

        [Test]
        public void DelayUntilPhase_WaitsForTargetPhase()
        {
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            bool executed = false;
            AsyncDelayUntilPhase(scheduler, FramePhase.LateUpdate, () => executed = true);

            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            Assert.IsFalse(executed);

            host.LateUpdate(0.016f, 0.016f);
            Assert.IsTrue(executed);
        }

        [Test]
        public void Delay_WithPhase_RunsInSpecifiedPhase()
        {
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            bool executed = false;
            AsyncDelayPhase(scheduler, 0.045f, FramePhase.FixedUpdate, () => executed = true);

            // 第一帧：时间不够 (0.016)
            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);

            // 第二帧：时间不够 (0.032)
            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);

            // 第三帧：时间够了 (0.048 > 0.045)，在 FixedUpdate 执行
            host.EarlyUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);  // EarlyUpdate 还不执行

            host.FixedUpdate(0.016f, 0.016f);
            Assert.IsTrue(executed);  // FixedUpdate 执行
        }

        [Test]
        public void WaitForFrames_WithPhase_CountsFramesInThatPhase()
        {
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            bool executed = false;
            AsyncWaitFramesPhase(scheduler, 2, FramePhase.EarlyUpdate, () => executed = true);

            // 第一帧
            host.EarlyUpdate(0.016f, 0.016f);
            Assert.IsFalse(executed);

            // 第二帧
            host.EarlyUpdate(0.016f, 0.016f);
            Assert.IsTrue(executed);
        }

        [Test]
        public void MultiplePhases_IndependentQueues()
        {
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            bool earlyExecuted = false, updateExecuted = false, lateExecuted = false;

            AsyncYieldPhase(scheduler, FramePhase.EarlyUpdate, () => earlyExecuted = true);
            AsyncYieldPhase(scheduler, FramePhase.Update, () => updateExecuted = true);
            AsyncYieldPhase(scheduler, FramePhase.LateUpdate, () => lateExecuted = true);

            // 依次执行各 Phase
            host.EarlyUpdate(0.016f, 0.016f);
            Assert.IsTrue(earlyExecuted);
            Assert.IsFalse(updateExecuted);
            Assert.IsFalse(lateExecuted);

            host.Update(0.016f, 0.016f);
            Assert.IsTrue(updateExecuted);
            Assert.IsFalse(lateExecuted);

            host.LateUpdate(0.016f, 0.016f);
            Assert.IsTrue(lateExecuted);
        }

        [Test]
        public void WaitForFrames_MultiplePhases_DoesNotCountMultipleTimes()
        {
            // 验证修复：一帧内调用多个 Phase 不会让 frameCount 重复递增
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            var task = scheduler.WaitForFrames(2);

            // 第一帧：调用所有 5 个 Phase
            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);
            host.EndOfFrame(0.016f, 0.016f);

            // 任务不应在第一帧完成（frameCount 应该只 +1，不是 +5）
            Assert.IsFalse(task.IsCompleted, "WaitForFrames(2) should not complete in the same frame");

            // 第二帧：再次调用所有 Phase
            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);
            host.EndOfFrame(0.016f, 0.016f);

            // 现在应该完成（frameCount = 2）
            Assert.IsTrue(task.IsCompleted, "WaitForFrames(2) should complete after 2 full frames");
        }

        [Test]
        public void Delay_MultiplePhases_DoesNotAccumulateMultipleTimes()
        {
            // 验证修复：一帧内调用多个 Phase 不会让 elapsedTime 累加多次
            var host = new ModuleSystem();
            var scheduler = new TGTaskScheduler();
            host.Register<ITGTaskScheduler>(scheduler);
            host.Initialize();

            var task = scheduler.Delay(0.1f);  // 100ms

            // 第一帧：调用所有 5 个 Phase，每个传入 0.016f (16ms)
            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);
            host.EndOfFrame(0.016f, 0.016f);

            // 任务不应完成（elapsedTime 应该只 +16ms，不是 +80ms）
            Assert.IsFalse(task.IsCompleted, "Delay(0.1f) should not complete after 16ms");

            // 再调用 6 帧（每帧 16ms × 6 = 96ms，总计 112ms）
            for (int i = 0; i < 6; i++)
            {
                host.EarlyUpdate(0.016f, 0.016f);
                host.FixedUpdate(0.016f, 0.016f);
                host.Update(0.016f, 0.016f);
                host.LateUpdate(0.016f, 0.016f);
                host.EndOfFrame(0.016f, 0.016f);
            }

            // 现在应该完成（总时间 = 7 × 16ms = 112ms > 100ms）
            Assert.IsTrue(task.IsCompleted, "Delay(0.1f) should complete after 112ms");
        }

        [Test]
        public void ProcessPhase_FrameBoundary_DetectedByPhaseOrder()
        {
            var scheduler = new TGTaskScheduler();

            // 完整 Early -> Fixed -> Update -> Late -> End 循环只应计为 1 帧
            scheduler.EarlyUpdate(0.016f, 0.016f);
            Assert.AreEqual(1, scheduler.FrameCount);

            scheduler.FixedUpdate(0.016f, 0.016f);
            scheduler.Update(0.016f, 0.016f);
            scheduler.LateUpdate(0.016f, 0.016f);
            scheduler.EndOfFrame(0.016f, 0.016f);
            Assert.AreEqual(1, scheduler.FrameCount);

            // 重复 Update 调用按“再次进入新帧”处理
            scheduler.Update(0.016f, 0.016f);
            Assert.AreEqual(2, scheduler.FrameCount);

            scheduler.Update(0.016f, 0.016f);
            Assert.AreEqual(3, scheduler.FrameCount);

            // Phase 回退也应视为新帧
            scheduler.FixedUpdate(0.016f, 0.016f);
            Assert.AreEqual(4, scheduler.FrameCount);
        }

        // ---- Phase-aware async helpers ----
        private static async TGTask AsyncYieldPhase(ITGTaskScheduler scheduler, FramePhase phase, Action onReached)
        {
            await scheduler.Yield(phase);
            onReached();
        }

        private static async TGTask AsyncDelayUntilPhase(ITGTaskScheduler scheduler, FramePhase phase, Action onReached)
        {
            await scheduler.DelayUntilPhase(phase);
            onReached();
        }

        private static async TGTask AsyncDelayPhase(ITGTaskScheduler scheduler, float seconds, FramePhase phase, Action onReached)
        {
            await scheduler.Delay(seconds, phase);
            onReached();
        }

        private static async TGTask AsyncWaitFramesPhase(ITGTaskScheduler scheduler, int frames, FramePhase phase, Action onReached)
        {
            await scheduler.WaitForFrames(frames, phase);
            onReached();
        }
    }
}

