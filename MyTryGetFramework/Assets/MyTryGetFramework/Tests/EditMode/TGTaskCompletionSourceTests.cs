using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 2 — <see cref="TGTaskCompletionSource"/> 测试（Iter 8 rename 后跟随）。
    /// </summary>
    [TestFixture]
    public class TGTaskCompletionSourceTests
    {
        [Test]
        public void SetResult_BeforeAwait_CompletesImmediately()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();

            var task = tcs.Task;
            Assert.IsTrue(task.IsCompleted);
            task.GetAwaiter().GetResult();
        }

        [Test]
        public void SetResult_AfterAwait_FiresContinuation()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;

            bool continuationFired = false;
            task.GetAwaiter().OnCompleted(() => continuationFired = true);

            Assert.IsFalse(continuationFired);
            tcs.SetResult();
            Assert.IsTrue(continuationFired);
        }

        [Test]
        public void SetException_ThrowsOnAwait()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;
            tcs.SetException(new InvalidOperationException("boom"));

            var ex = Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual("boom", ex.Message);
        }

        [Test]
        public void SetException_NullThrows()
        {
            var tcs = new TGTaskCompletionSource();
            Assert.Throws<ArgumentNullException>(() => tcs.SetException(null));
        }

        [Test]
        public void SetCanceled_ThrowsOperationCanceledOnAwait()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;
            tcs.SetCanceled();

            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void RepeatedSetResult_SilentlyIgnored()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();
            Assert.DoesNotThrow(() => tcs.SetResult());
            Assert.IsTrue(tcs.Task.IsCompleted);
        }

        [Test]
        public void SetExceptionAfterSetResult_SilentlyIgnored()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();

            Assert.DoesNotThrow(() => tcs.SetException(new Exception("ignored")));
            Assert.DoesNotThrow(() => tcs.Task.GetAwaiter().GetResult());
        }

        // ----------------- Generic version -----------------

        [Test]
        public void GenericTcs_SetResult_ReturnsValue()
        {
            var tcs = new TGTaskCompletionSource<string>();
            tcs.SetResult("hello");

            Assert.IsTrue(tcs.Task.IsCompleted);
            Assert.AreEqual("hello", tcs.Task.GetAwaiter().GetResult());
        }

        [Test]
        public void GenericTcs_SetResult_AfterAwait_FiresContinuation()
        {
            var tcs = new TGTaskCompletionSource<int>();
            var task = tcs.Task;

            int receivedValue = 0;
            task.GetAwaiter().OnCompleted(() => receivedValue = task.GetAwaiter().GetResult());

            tcs.SetResult(42);
            Assert.AreEqual(42, receivedValue);
        }

        [Test]
        public void GenericTcs_SetException_PropagatesOnAwait()
        {
            var tcs = new TGTaskCompletionSource<int>();
            tcs.SetException(new InvalidOperationException("typed-boom"));

            Assert.Throws<InvalidOperationException>(() => tcs.Task.GetAwaiter().GetResult());
        }

        [Test]
        public void GenericTcs_SetCanceled_ThrowsOnAwait()
        {
            var tcs = new TGTaskCompletionSource<int>();
            tcs.SetCanceled();

            Assert.Throws<OperationCanceledException>(() => tcs.Task.GetAwaiter().GetResult());
        }

        // ----------------- async TGTask + tcs interop -----------------

        [Test]
        public void AsyncTGTask_AwaitingTcs_WaitsThenResumes()
        {
            var tcs = new TGTaskCompletionSource();
            bool stage1 = false, stage2 = false;

            var outer = OuterBody(tcs, () => stage1 = true, () => stage2 = true);

            Assert.IsTrue(stage1);
            Assert.IsFalse(stage2);
            Assert.IsFalse(outer.IsCompleted);

            tcs.SetResult();

            Assert.IsTrue(stage2);
            Assert.IsTrue(outer.IsCompleted);
        }

        [Test]
        public void AsyncTGTask_AwaitingTcs_PropagatesException()
        {
            var tcs = new TGTaskCompletionSource();
            var outer = OuterBodyExpectException(tcs);

            Assert.IsFalse(outer.IsCompleted);
            tcs.SetException(new InvalidOperationException("from-tcs"));

            Assert.IsTrue(outer.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => outer.GetAwaiter().GetResult());
        }

        // ----------------- async helpers -----------------

        private static async TGTask OuterBody(TGTaskCompletionSource tcs, Action stage1Hook, Action stage2Hook)
        {
            stage1Hook();
            await tcs.Task;
            stage2Hook();
        }

        private static async TGTask OuterBodyExpectException(TGTaskCompletionSource tcs)
        {
            await tcs.Task;
        }
    }
}
