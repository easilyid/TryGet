using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 3 — TGTask Version 防过期机制测试（Iter 8 rename 后跟随）。
    /// </summary>
    [TestFixture]
    public class TGTaskVersionTests
    {
        [Test]
        public void Reset_IncrementsVersion()
        {
            var body = new TGTaskBody();
            int v0 = body.Version;
            body.Reset();
            int v1 = body.Version;

            Assert.AreNotEqual(v0, v1, "Reset 必须改变 version");
        }

        [Test]
        public void Reset_RestoresInitialState()
        {
            var body = new TGTaskBody();
            body.SetResult();
            Assert.IsTrue(body.IsCompleted);

            body.Reset();
            Assert.IsFalse(body.IsCompleted);
            Assert.DoesNotThrow(() => body.GetResult());
        }

        [Test]
        public void AwaiterGetResult_ExpiredVersion_Throws()
        {
            var tcs = new TGTaskCompletionSource();
            tcs.SetResult();

            var task = tcs.Task;
            Assert.DoesNotThrow(() => task.GetAwaiter().GetResult(), "原始 version 应能正常 GetResult");

            ExpireBody(task);

            Assert.Throws<TGTaskExpiredException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void AwaiterOnCompleted_ExpiredVersion_Throws()
        {
            var tcs = new TGTaskCompletionSource();
            var task = tcs.Task;

            ExpireBody(task);

            Assert.Throws<TGTaskExpiredException>(() => task.GetAwaiter().OnCompleted(() => { }));
        }

        [Test]
        public void GenericAwaiterGetResult_ExpiredVersion_Throws()
        {
            var tcs = new TGTaskCompletionSource<int>();
            tcs.SetResult(123);
            var task = tcs.Task;

            ExpireBodyGeneric(task);

            Assert.Throws<TGTaskExpiredException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void TcsSetResult_AfterExternalReset_Throws()
        {
            var tcs = new TGTaskCompletionSource();
            ExpireBody(tcs.Task);

            Assert.Throws<TGTaskExpiredException>(() => tcs.SetResult());
        }

        [Test]
        public void MaxVersion_WrapsAroundOnReset()
        {
            var body = new TGTaskBody();

            var versionField = typeof(TGTaskBody).GetField("_version",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(versionField, "_version 字段必须存在");

            versionField.SetValue(body, TGTaskBody.MaxVersion);
            body.Reset();

            Assert.AreEqual(TGTaskBody.MinVersion, body.Version,
                "到达 MaxVersion 后 Reset 应回绕到 MinVersion 而非 int 溢出");
        }

        [Test]
        public void Default_TGTask_NoBodyNoExpiredException()
        {
            var t = default(TGTask);
            Assert.DoesNotThrow(() => t.GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => t.GetAwaiter().OnCompleted(() => { }));
        }

        // ---------- helpers ----------

        private static void ExpireBody(TGTask task)
        {
            if (task.Body is TGTaskBody body)
            {
                body.Reset();
            }
            else
            {
                Assert.Fail("task.Body 不是 TGTaskBody，无法构造 expired 场景");
            }
        }

        private static void ExpireBodyGeneric<T>(TGTask<T> task)
        {
            if (task.Body is TGTaskBody<T> body)
            {
                body.Reset();
            }
            else
            {
                Assert.Fail("task.Body 不是 TGTaskBody<T>，无法构造 expired 场景");
            }
        }
    }
}
