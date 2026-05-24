using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 1 — TGTask 异步原语骨架的 smoke 测试（Iter 8 rename ITask → TGTask 后跟随）。
    /// </summary>
    [TestFixture]
    public class TGTaskCompilationSmokeTests
    {
        [Test]
        public void AsyncTGTask_WithoutAwait_CompletesImmediately()
        {
            var task = NoAwaitBody();
            Assert.IsTrue(task.IsCompleted, "无 await 的 async TGTask 必须立即完成");
            task.GetAwaiter().GetResult();
        }

        [Test]
        public void AsyncTGTask_OfT_WithoutAwait_ReturnsValue()
        {
            var task = NoAwaitBodyOfT();
            Assert.IsTrue(task.IsCompleted);
            int v = task.GetAwaiter().GetResult();
            Assert.AreEqual(42, v);
        }

        [Test]
        public void AsyncTGTask_ThrowingBody_PropagatesException()
        {
            var task = ThrowingBody();
            Assert.IsTrue(task.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void Default_TGTask_IsCompletedAndSafe()
        {
            var t = default(TGTask);
            Assert.IsTrue(t.IsCompleted);
            t.GetAwaiter().GetResult();
        }

        [Test]
        public void Default_TGTaskOfT_ReturnsDefault()
        {
            var t = default(TGTask<int>);
            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual(0, t.GetAwaiter().GetResult());
        }

        [Test]
        public void CompletedTask_IsCompletedAndSafe()
        {
            var t = TGTask.CompletedTask;
            Assert.IsTrue(t.IsCompleted);
            Assert.DoesNotThrow(() => t.GetAwaiter().GetResult());
        }

        [Test]
        public void FromResult_ReturnsValueImmediately()
        {
            var t = TGTask<int>.FromResult(99);
            Assert.IsTrue(t.IsCompleted);
            Assert.AreEqual(99, t.GetAwaiter().GetResult());
        }

        [Test]
        public void FromException_ThrowsOnAwait()
        {
            var t = TGTask.FromException(new InvalidOperationException("explicit"));
            Assert.IsTrue(t.IsCompleted);
            var ex = Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult());
            Assert.AreEqual("explicit", ex.Message);
        }

        [Test]
        public void FromException_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TGTask.FromException(null));
            Assert.Throws<ArgumentNullException>(() => TGTask<int>.FromException(null));
        }

        [Test]
        public void FromCanceled_ThrowsOperationCanceled()
        {
            var t = TGTask.FromCanceled();
            Assert.Throws<OperationCanceledException>(() => t.GetAwaiter().GetResult());

            var tOfT = TGTask<int>.FromCanceled();
            Assert.Throws<OperationCanceledException>(() => tOfT.GetAwaiter().GetResult());
        }

        [Test]
        public void Forget_OnCompletedTask_NoCrash()
        {
            var t = NoAwaitBody();
            t.Forget();
        }

        // ---------- async helpers ----------

        private static async TGTask NoAwaitBody()
        {
            await default(NoopAwaitable);
            return;
        }

        private static async TGTask<int> NoAwaitBodyOfT()
        {
            await default(NoopAwaitable);
            return 42;
        }

        private static async TGTask ThrowingBody()
        {
            await default(NoopAwaitable);
            throw new InvalidOperationException("expected");
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
