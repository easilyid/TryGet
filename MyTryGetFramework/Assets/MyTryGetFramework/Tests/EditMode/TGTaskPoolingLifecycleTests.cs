using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// C11 池化生命周期收口测试：消费侧统一归还（Builder + Manual）、
    /// IsCompleted version 校验、tcs 双归还防护、调度器稳态零分配。
    /// </summary>
    [TestFixture]
    public class TGTaskPoolingLifecycleTests
    {
        [SetUp]
        public void SetUp()
        {
            TGTaskPool.ClearAll();
            TGTaskCompletionSource.ClearSourcePool();
        }

        // ----- 消费侧归还：Manual body -----

        [Test]
        public void ManualBody_ReturnedToPool_AfterAwaiterGetResult()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;
            tcs.SetResult();

            Assert.AreEqual(0, TGTaskPool.PooledCount);
            task.GetAwaiter().GetResult();
            Assert.AreEqual(1, TGTaskPool.PooledCount, "Manual body 应在消费侧 GetResult 后回池");
        }

        [Test]
        public void FromException_BodyReturnedToPool_AfterConsumed()
        {
            var task = TGTask.FromException(new InvalidOperationException("x"));

            Assert.AreEqual(0, TGTaskPool.PooledCount);
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(1, TGTaskPool.PooledCount, "FromException 的 body 应在消费后回池");
        }

        [Test]
        public void GenericForget_CompletedFaultedTask_RaisesUnobservedAndReturnsBodyToPool()
        {
            TGTaskPool.ClearGeneric<int>();
            Exception unobserved = null;
            Action<Exception> h = ex => unobserved = ex;
            TGTaskScheduler.UnobservedException += h;
            try
            {
                var task = TGTask<int>.FromException(new InvalidOperationException("x"));

                Assert.AreEqual(0, TGTaskPool.PooledCountOf<int>());
                task.Forget();

                Assert.IsInstanceOf<InvalidOperationException>(unobserved);
                Assert.AreEqual(1, TGTaskPool.PooledCountOf<int>(),
                    "TGTask<T>.Forget 应消费异常并归还泛型 body");
            }
            finally { TGTaskScheduler.UnobservedException -= h; }
        }

        [Test]
        public void GenericForget_PendingFaultedTask_RaisesUnobservedLaterAndReturnsBodyToPool()
        {
            TGTaskPool.ClearGeneric<int>();
            Exception unobserved = null;
            Action<Exception> h = ex => unobserved = ex;
            TGTaskScheduler.UnobservedException += h;
            try
            {
                var tcs = new TGTaskCompletionSource<int>();
                var task = tcs.Task;

                task.Forget();
                Assert.IsNull(unobserved);
                Assert.AreEqual(0, TGTaskPool.PooledCountOf<int>());

                tcs.SetException(new InvalidOperationException("later"));

                Assert.IsInstanceOf<InvalidOperationException>(unobserved);
                Assert.AreEqual(1, TGTaskPool.PooledCountOf<int>(),
                    "pending TGTask<T>.Forget 应在后续完成时消费异常并归还 body");
            }
            finally { TGTaskScheduler.UnobservedException -= h; }
        }

        [Test]
        public void TcsReturn_AfterConsumerReturned_IsNoOp_NoDoubleReturn()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;
            tcs.SetResult();
            task.GetAwaiter().GetResult(); // 消费侧已归还

            tcs.Return(); // version 守卫 → no-op
            Assert.AreEqual(1, TGTaskPool.PooledCount, "tcs.Return 不应造成双重入池");

            // 池中两次 Rent 必须拿到不同 body（防同一实例入池两次的腐败）
            var b1 = TGTaskPool.Rent();
            var b2 = TGTaskPool.Rent();
            Assert.AreNotSame(b1, b2);
        }

        [Test]
        public void TcsReturn_NeverAwaited_StillReturnsBody()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();
            tcs.Return(); // 从未 await：手动归还路径仍有效

            Assert.AreEqual(1, TGTaskPool.PooledCount);
        }

        // ----- IsCompleted version 校验 -----

        [Test]
        public void StaleHandle_IsCompleted_ReturnsTrue_AfterBodyRecycled()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;
            tcs.SetResult();
            task.GetAwaiter().GetResult(); // body 回池（version++）

            // 06/11 bug 类回归：旧句柄不得读到 false（他人状态）
            Assert.IsTrue(task.IsCompleted, "过期句柄 IsCompleted 应视为已完成");
            Assert.Throws<TGTaskExpiredException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void StaleHandle_IsCompleted_True_EvenWhenBodyReusedByPendingTask()
        {
            var tcs1 = new TGTaskCompletionSource();
            var staleTask = tcs1.Task;
            tcs1.SetResult();
            staleTask.GetAwaiter().GetResult(); // body 回池

            // 同一 body 被新任务租走且处于 pending
            var tcs2 = new TGTaskCompletionSource();
            Assert.IsFalse(tcs2.Task.IsCompleted, "新任务 pending");
            Assert.IsTrue(staleTask.IsCompleted, "旧句柄不应读到新任务的 pending 状态");
        }

        // ----- 调度器稳态池命中 -----

        [Test]
        public void Scheduler_YieldLoop_SteadyState_NoPoolGrowth()
        {
            var scheduler = new TGTaskScheduler();

            // 预热一轮，让 body/tcs 进池
            RunOneYieldCycle(scheduler);
            int bodyBaseline = TGTaskPool.PooledCount;
            int tcsBaseline = TGTaskCompletionSource.PooledSourceCount;
            Assert.GreaterOrEqual(bodyBaseline, 1, "预热后 body 池应非空");
            Assert.GreaterOrEqual(tcsBaseline, 1, "预热后 tcs 池应非空");

            for (int i = 0; i < 20; i++)
                RunOneYieldCycle(scheduler);

            Assert.AreEqual(bodyBaseline, TGTaskPool.PooledCount, "稳态循环 body 池计数不应增长（完全复用）");
            Assert.AreEqual(tcsBaseline, TGTaskCompletionSource.PooledSourceCount, "稳态循环 tcs 池计数不应增长（完全复用）");
        }

        private static void RunOneYieldCycle(TGTaskScheduler scheduler)
        {
            bool resumed = false;
            AwaitYield(scheduler, () => resumed = true).Forget();
            // Yield 注册到 NextFrame，下一次 Update 触发
            scheduler.Update(0.016f, 0.016f);
            Assert.IsTrue(resumed, "Yield 应在下一次 Update 恢复");
        }

        private static async TGTask AwaitYield(TGTaskScheduler scheduler, Action onResumed)
        {
            await scheduler.Yield();
            onResumed();
        }

        [Test]
        public void Scheduler_WaitForFramesZero_CompletesImmediately_ZeroAllocation()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.WaitForFrames(0);

            Assert.IsTrue(task.IsCompleted);
            Assert.DoesNotThrow(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(0, TGTaskPool.PooledCount, "0 帧等待不应租用 body");
        }

        [Test]
        public void Scheduler_Shutdown_PendingTasksCanceled_TcsRecycled()
        {
            var scheduler = new TGTaskScheduler();
            var task = scheduler.Delay(10f);

            scheduler.Shutdown();

            Assert.IsTrue(task.IsCompleted);
            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.GreaterOrEqual(TGTaskCompletionSource.PooledSourceCount, 1, "Shutdown 后 tcs 应回收");
        }

        // ----- Timer 扩展池化 -----

        [Test]
        public void TimerWaitAsync_SteadyState_NoPoolGrowth()
        {
            var timer = new TimerModule();

            RunOneTimerCycle(timer);
            int bodyBaseline = TGTaskPool.PooledCount;
            int tcsBaseline = TGTaskCompletionSource.PooledSourceCount;

            for (int i = 0; i < 10; i++)
                RunOneTimerCycle(timer);

            Assert.AreEqual(bodyBaseline, TGTaskPool.PooledCount);
            Assert.AreEqual(tcsBaseline, TGTaskCompletionSource.PooledSourceCount);
        }

        private static void RunOneTimerCycle(TimerModule timer)
        {
            bool resumed = false;
            AwaitTimer(timer, () => resumed = true).Forget();
            timer.Update(0.05f, 0.05f); // 0.03s 到期
            Assert.IsTrue(resumed);
        }

        private static async TGTask AwaitTimer(ITimerModule timer, Action onResumed)
        {
            await timer.WaitAsync(0.03f);
            onResumed();
        }
    }
}
