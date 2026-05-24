using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 9 — 异步原语关键边界测试：
    /// 1. 多层嵌套 async TGTask（2/3 层）值传递 + 异常穿透
    /// 2. Builder 类型 TGTask 第一次 await 后 body 归还到 Pool，struct 副本再次 await 应抛 TGTaskExpiredException
    /// 3. Pool 复用 stress：~10K 次 Rent/Return 后 Pool 容量保持有界 + alloc 量信息（diagnostic，非 hard assert）
    ///
    /// 注意：V0.6 DoD #3（100W await alloc &lt; 1MB）受限于 TGTaskCompletionSource 是 class
    /// （每次 Yield 必 alloc）。完整达成留 V0.6.5+ 池化 tcs，本 Iter 仅给"Pool 对 body 复用生效"
    /// 的硬证据。
    /// </summary>
    [TestFixture]
    public class TGTaskEdgeCaseTests
    {
        [SetUp]
        public void SetUp()
        {
            TGTaskPool.ClearAll();
            TGTaskPool.ClearGeneric<int>();
            TGTaskPool.ClearGeneric<string>();
            TGTaskPool.MaxPoolSize = 64;
        }

        [TearDown]
        public void TearDown()
        {
            TGTaskPool.ClearAll();
            TGTaskPool.ClearGeneric<int>();
            TGTaskPool.ClearGeneric<string>();
            TGTaskPool.MaxPoolSize = 64;
        }

        // ============= 1. 嵌套 async TGTask =============

        [Test]
        public void NestedAsync_TwoLevel_ResultPropagates()
        {
            var tcs = new TGTaskCompletionSource<int>();
            var outer = TwoLevelOuter(tcs);

            Assert.IsFalse(outer.IsCompleted, "深层未完成时 outer 不应完成");

            tcs.SetResult(10);

            Assert.IsTrue(outer.IsCompleted);
            int v = outer.GetAwaiter().GetResult();
            Assert.AreEqual(11, v, "Inner 返回 10，Middle +1，Outer 直接返回，结果应为 11");
        }

        [Test]
        public void NestedAsync_ThreeLevel_ResultPropagates()
        {
            var tcs = new TGTaskCompletionSource<int>();
            var outer = ThreeLevelOuter(tcs);

            Assert.IsFalse(outer.IsCompleted);
            tcs.SetResult(100);
            Assert.IsTrue(outer.IsCompleted);

            int v = outer.GetAwaiter().GetResult();
            Assert.AreEqual(111, v, "100 + 10 (Middle) + 1 (Outer 加) = 111");
        }

        [Test]
        public void NestedAsync_ExceptionInDeepest_PropagatesToTop()
        {
            var tcs = new TGTaskCompletionSource<int>();
            var outer = ThreeLevelOuter(tcs);

            Assert.IsFalse(outer.IsCompleted);
            tcs.SetException(new InvalidOperationException("deepest-boom"));
            Assert.IsTrue(outer.IsCompleted);

            var ex = Assert.Throws<InvalidOperationException>(() => outer.GetAwaiter().GetResult());
            Assert.AreEqual("deepest-boom", ex.Message, "深层异常应原样传到顶层");
        }

        [Test]
        public void NestedAsync_MultipleSiblingAwaits_SequentialOrder()
        {
            var t1 = new TGTaskCompletionSource();
            var t2 = new TGTaskCompletionSource();
            var log = new System.Collections.Generic.List<string>();

            var outer = SequentialAwaiter(t1, t2, log);

            Assert.AreEqual(new[] { "before-t1" }, log.ToArray());
            Assert.IsFalse(outer.IsCompleted);

            t1.SetResult();
            Assert.AreEqual(new[] { "before-t1", "after-t1", "before-t2" }, log.ToArray());
            Assert.IsFalse(outer.IsCompleted);

            t2.SetResult();
            Assert.IsTrue(outer.IsCompleted);
            Assert.AreEqual(new[] { "before-t1", "after-t1", "before-t2", "after-t2" }, log.ToArray());
        }

        // ============= 2. Builder body 归还后再 await 应抛 =============

        [Test]
        public void BuilderTask_SecondAwait_ThrowsExpired()
        {
            var task = NoopBuilderTask();
            // 第一次 await：Builder body 在 finally 归还到 Pool（Reset → version++）
            task.GetAwaiter().GetResult();

            // struct 副本 task 的 Version 还是旧的，Body 引用还活着但 Body.Version 已变
            Assert.Throws<TGTaskExpiredException>(
                () => task.GetAwaiter().GetResult(),
                "Builder TGTask 不支持多次 await（body 已归还到 Pool）");
        }

        [Test]
        public void BuilderTaskOfT_SecondAwait_ThrowsExpired()
        {
            var task = NoopBuilderTaskOfInt();
            int first = task.GetAwaiter().GetResult();
            Assert.AreEqual(42, first);

            Assert.Throws<TGTaskExpiredException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void BuilderTask_AfterFirstAwait_BodyAvailableInPool()
        {
            int before = TGTaskPool.PooledCount;
            var task = NoopBuilderTask();
            task.GetAwaiter().GetResult();

            Assert.GreaterOrEqual(TGTaskPool.PooledCount, before + 1,
                "Builder body 第一次 await 后应进入 Pool");
        }

        [Test]
        public void BuilderTask_OnCompletedTwice_SecondThrowsExpired()
        {
            // 完成后立即 OnCompleted（同步路径）— 第一次 invoke 触发 GetResult 归还
            var task = NoopBuilderTask();
            // 这里第一次 await 已让 body 归还
            task.GetAwaiter().GetResult();

            // 此后任何走 Body 的 awaiter 调用都应识别到 version 失配
            Assert.Throws<TGTaskExpiredException>(
                () => task.GetAwaiter().OnCompleted(() => { }));
        }

        // ============= 3. Pool 复用 stress + GC diagnostic =============

        [Test]
        public void Stress_BuilderBodyReusePool_BoundedSize()
        {
            // 串行 Rent/Return 反复 10K 次，Pool size 应稳定保持 1（每次 Return 都是同一个槽）
            const int N = 10_000;
            for (int i = 0; i < N; i++)
            {
                NoopBuilderTask().GetAwaiter().GetResult();
            }

            Assert.AreEqual(1, TGTaskPool.PooledCount,
                "10K 次串行 Builder TGTask 后 Pool 应只占 1 个槽（完美复用）");
        }

        [Test]
        public void Stress_ConcurrentRent_PoolGrowsThenCapsAtMax()
        {
            TGTaskPool.MaxPoolSize = 8;
            var bodies = new System.Collections.Generic.List<TGTaskBody>();

            // 并行 Rent 32 个（无 Return），Pool 不增长（因为 Rent 走 new path）
            for (int i = 0; i < 32; i++) bodies.Add(TGTaskPool.Rent());
            Assert.AreEqual(0, TGTaskPool.PooledCount);

            // Return 32 个，前 8 个入池，剩余 24 个被 GC（直接丢）
            for (int i = 0; i < 32; i++) TGTaskPool.Return(bodies[i]);
            Assert.AreEqual(8, TGTaskPool.PooledCount, "Pool 应被 MaxPoolSize=8 限制");
        }

        [Test]
        public void Diagnostic_TenKAwaitYield_AllocPrint()
        {
            // V0.6 DoD #3 信息测试（不 hard assert）：测量 10K 次 Yield+Update 的 GC alloc。
            // 当前实现 TGTaskCompletionSource 是 class（每次 Yield alloc 一个 tcs ≈ 32 bytes），
            // 故 10K Yield ≈ 320KB 预期。完整池化推到 V0.6.5。
            var scheduler = new TGTaskScheduler();

            // 预热：填 Pool 到稳定
            for (int i = 0; i < 100; i++)
            {
                var _ = scheduler.Yield();
                scheduler.Update(0.016f, 0.016f);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(true);

            const int N = 10_000;
            for (int i = 0; i < N; i++)
            {
                var _ = scheduler.Yield();
                scheduler.Update(0.016f, 0.016f);
            }

            long after = GC.GetTotalMemory(false);
            long delta = after - before;
            TestContext.WriteLine(
                $"[V0.6 Diagnostic] {N} Yield+Update allocated ~{delta} bytes ({delta / 1024.0:F1} KB)");

            // 软上限：> 50MB 说明 Pool/复用完全没工作（如内存泄漏）
            Assert.Less(delta, 50L * 1024 * 1024,
                $"10K Yield 触发 alloc {delta} bytes 远超合理范围（Pool 完全失效）");
        }

        // ============= async helpers =============

        private static async TGTask<int> ThreeLevelOuter(TGTaskCompletionSource<int> tcs)
        {
            int mid = await ThreeLevelMiddle(tcs);
            return mid + 1;
        }

        private static async TGTask<int> ThreeLevelMiddle(TGTaskCompletionSource<int> tcs)
        {
            int inner = await ThreeLevelInner(tcs);
            return inner + 10;
        }

        private static async TGTask<int> ThreeLevelInner(TGTaskCompletionSource<int> tcs)
        {
            int v = await tcs.Task;
            return v;
        }

        private static async TGTask<int> TwoLevelOuter(TGTaskCompletionSource<int> tcs)
        {
            int v = await TwoLevelInner(tcs);
            return v + 1;
        }

        private static async TGTask<int> TwoLevelInner(TGTaskCompletionSource<int> tcs)
        {
            int v = await tcs.Task;
            return v;
        }

        private static async TGTask SequentialAwaiter(
            TGTaskCompletionSource a, TGTaskCompletionSource b,
            System.Collections.Generic.List<string> log)
        {
            log.Add("before-t1");
            await a.Task;
            log.Add("after-t1");
            log.Add("before-t2");
            await b.Task;
            log.Add("after-t2");
        }

        private static async TGTask NoopBuilderTask()
        {
            await default(SyncCompleteAwaitable);
        }

        private static async TGTask<int> NoopBuilderTaskOfInt()
        {
            await default(SyncCompleteAwaitable);
            return 42;
        }

        private readonly struct SyncCompleteAwaitable
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
