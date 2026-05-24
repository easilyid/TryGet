using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 4 — TGTaskPool 真池化测试（Iter 8 rename 后跟随）。
    /// </summary>
    [TestFixture]
    public class TGTaskPoolTests
    {
        [SetUp]
        public void Setup()
        {
            TGTaskPool.ClearAll();
            TGTaskPool.ClearGeneric<int>();
            TGTaskPool.ClearGeneric<string>();
            TGTaskPool.MaxPoolSize = 64;
        }

        [TearDown]
        public void Teardown()
        {
            TGTaskPool.ClearAll();
            TGTaskPool.ClearGeneric<int>();
            TGTaskPool.ClearGeneric<string>();
            TGTaskPool.MaxPoolSize = 64;
        }

        [Test]
        public void Rent_FromEmptyPool_AllocatesNew()
        {
            Assert.AreEqual(0, TGTaskPool.PooledCount);
            var body = TGTaskPool.Rent();
            Assert.NotNull(body);
            Assert.AreEqual(0, TGTaskPool.PooledCount);
        }

        [Test]
        public void Return_PutsBodyIntoPool()
        {
            var body = TGTaskPool.Rent();
            TGTaskPool.Return(body);
            Assert.AreEqual(1, TGTaskPool.PooledCount);
        }

        [Test]
        public void Rent_AfterReturn_ReusesSameBody()
        {
            var b1 = TGTaskPool.Rent();
            TGTaskPool.Return(b1);
            var b2 = TGTaskPool.Rent();
            Assert.AreSame(b1, b2);
        }

        [Test]
        public void Return_AboveMaxPoolSize_DoesNotPool()
        {
            TGTaskPool.MaxPoolSize = 2;
            var b1 = TGTaskPool.Rent();
            var b2 = TGTaskPool.Rent();
            var b3 = TGTaskPool.Rent();

            TGTaskPool.Return(b1);
            TGTaskPool.Return(b2);
            Assert.AreEqual(2, TGTaskPool.PooledCount);

            TGTaskPool.Return(b3);
            Assert.AreEqual(2, TGTaskPool.PooledCount);
        }

        [Test]
        public void Return_Null_NoOp()
        {
            Assert.DoesNotThrow(() => TGTaskPool.Return(null));
            Assert.AreEqual(0, TGTaskPool.PooledCount);
        }

        [Test]
        public void GenericPool_IsIsolatedPerT()
        {
            var bi = TGTaskPool.Rent<int>();
            TGTaskPool.Return(bi);
            Assert.AreEqual(1, TGTaskPool.PooledCountOf<int>());
            Assert.AreEqual(0, TGTaskPool.PooledCountOf<string>());

            var bs = TGTaskPool.Rent<string>();
            TGTaskPool.Return(bs);
            Assert.AreEqual(1, TGTaskPool.PooledCountOf<int>());
            Assert.AreEqual(1, TGTaskPool.PooledCountOf<string>());
        }

        [Test]
        public void GenericPool_ReusesSameBody()
        {
            var b1 = TGTaskPool.Rent<int>();
            TGTaskPool.Return(b1);
            var b2 = TGTaskPool.Rent<int>();
            Assert.AreSame(b1, b2);
        }

        // ---------- 真实 async path 验证 ----------

        [Test]
        public void AsyncTGTask_AfterAwait_BuilderBodyAutoReturned()
        {
            int beforePool = TGTaskPool.PooledCount;
            var t = NoopAsyncTGTask();
            t.GetAwaiter().GetResult();

            Assert.GreaterOrEqual(TGTaskPool.PooledCount, beforePool + 1);
        }

        [Test]
        public void AsyncTGTaskOfT_AfterAwait_BuilderBodyAutoReturned()
        {
            int before = TGTaskPool.PooledCountOf<int>();
            var t = NoopAsyncTGTaskOfInt();
            var v = t.GetAwaiter().GetResult();
            Assert.AreEqual(7, v);
            Assert.GreaterOrEqual(TGTaskPool.PooledCountOf<int>(), before + 1);
        }

        [Test]
        public void RepeatedAsyncTGTask_PoolReusesBody()
        {
            for (int i = 0; i < 100; i++)
            {
                NoopAsyncTGTask().GetAwaiter().GetResult();
            }
            Assert.AreEqual(1, TGTaskPool.PooledCount);
        }

        [Test]
        public void AsyncTGTask_ThrowingBody_StillReturnsToPool()
        {
            int before = TGTaskPool.PooledCount;
            try
            {
                ThrowingAsyncTGTask().GetAwaiter().GetResult();
                Assert.Fail("应抛异常");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.GreaterOrEqual(TGTaskPool.PooledCount, before + 1);
        }

        [Test]
        public void Tcs_Return_PutsBodyBackToPool()
        {
            int before = TGTaskPool.PooledCount;
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();
            tcs.Return();

            Assert.GreaterOrEqual(TGTaskPool.PooledCount, before + 1);
        }

        [Test]
        public void Tcs_AfterReturn_ThrowsOnSetResult()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();
            tcs.Return();

            Assert.Throws<TGTaskExpiredException>(() => tcs.SetResult());
        }

        [Test]
        public void Tcs_WithoutReturn_BodyLeakedButNoCrash()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();
            tcs.Task.GetAwaiter().GetResult();
        }

        // ---------- async helpers ----------

        private static async TGTask NoopAsyncTGTask()
        {
            await default(SyncCompleteAwaitable);
        }

        private static async TGTask<int> NoopAsyncTGTaskOfInt()
        {
            await default(SyncCompleteAwaitable);
            return 7;
        }

        private static async TGTask ThrowingAsyncTGTask()
        {
            await default(SyncCompleteAwaitable);
            throw new InvalidOperationException("boom-from-async");
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
