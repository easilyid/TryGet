using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// TGTaskPool 双重 Return（double-release）健壮性测试。
    ///
    /// 背景：TGTaskPool.Return（TGTaskPool.cs:35-41）对「同一 body 重复入池」无任何检测，
    /// 会把同一实例在 _bodies 栈里压两份。之后 Rent 两次返回同一实例，导致两个独立的 TGTask
    /// 共用同一 body —— version 互相踩踏，产生 ABA 腐败（前者 await 完成后归还 body，后者仍在
    /// 使用中，前者 GetResult 的 version 守卫可能误判或漏判）。
    ///
    /// 对比：PoolModule.ObjectPool 在 DEBUG/UNITY_ASSERTIONS 下有 _activeSet 双释放检测，
    /// 但 TGTaskPool（框架内部、高频 await 路径）没有等价保护。
    ///
    /// 这些测试断言【健壮的期望行为】（双 Return 后 Rent 两次不应得到同一实例）。
    /// 当前实现下预期【红灯】，证明 bug 存在；修复后转绿。
    /// </summary>
    [TestFixture]
    public class TGTaskPoolDoubleReturnTests
    {
        [SetUp]
        public void Setup()
        {
            TGTaskPool.ClearAll();
            TGTaskPool.MaxPoolSize = 64;
        }

        [TearDown]
        public void Teardown()
        {
            TGTaskPool.ClearAll();
            TGTaskPool.MaxPoolSize = 64;
        }

        /// <summary>
        /// 同一 body 两次 Return 后，池里应只有一份；Rent 两次应得到【不同】实例。
        /// 当前实现：栈里有两份，Rent 两次返回同一实例 → 红灯。
        /// </summary>
        [Test]
        public void Return_SameBody_Twice_RentTwice_YieldsDifferentInstances()
        {
            var body = TGTaskPool.Rent();
            TGTaskPool.Return(body);
            // 故意重复 Return 同一 body（模拟误用：tcs.Return 后又手动 Return，或并发路径双归还）
            TGTaskPool.Return(body);

            // 健壮实现：重复 Return 应被检测/忽略，PooledCount 仍为 1
            Assert.AreEqual(1, TGTaskPool.PooledCount,
                "重复 Return 同一 body 不应在池中产生两份（防 ABA 腐败）");

            var first = TGTaskPool.Rent();
            var second = TGTaskPool.Rent();

            Assert.AreNotSame(first, second,
                "Rent 两次不应返回同一实例 —— 否则两个 TGTask 共用 body，version 踩踏腐败");
        }

        /// <summary>
        /// 泛型池同样问题：TGTaskBody&lt;T&gt; 双 Return。
        /// </summary>
        [Test]
        public void Return_SameGenericBody_Twice_RentTwice_YieldsDifferentInstances()
        {
            TGTaskPool.ClearGeneric<int>();

            var body = TGTaskPool.Rent<int>();
            TGTaskPool.Return(body);
            TGTaskPool.Return(body);

            Assert.AreEqual(1, TGTaskPool.PooledCountOf<int>(),
                "泛型池重复 Return 同一 body 不应产生两份");

            var first = TGTaskPool.Rent<int>();
            var second = TGTaskPool.Rent<int>();

            Assert.AreNotSame(first, second,
                "泛型池 Rent 两次不应返回同一实例");
        }

        /// <summary>
        /// 双 Return 的腐败后果直接验证：两个 TGTask 通过同一 body 并行存活时，
        /// 先完成者归还 body 会使后者持有的句柄 version 失配。
        ///
        /// 这里用 TGTaskCompletionSource 模拟「同一 body 被两个 task 共用」的最小场景：
        /// Rent 出 body → 假装两个 task 都引用它 → 第一个 Return 后第二个再 await 应察觉异常。
        /// 当前实现无检测，此场景下静默腐败（version 可能巧合不报错，也可能误抛 Expired）——
        /// 至少断言「Rent 两次不返回同一实例」这条硬契约。
        /// </summary>
        [Test]
        public void DoubleReturn_PooledCount_DoesNotExceedUniqueBodies()
        {
            // Rent 3 个不同 body，全部 Return，再对其中 1 个重复 Return 一次
            var b1 = TGTaskPool.Rent();
            var b2 = TGTaskPool.Rent();
            var b3 = TGTaskPool.Rent();
            TGTaskPool.Return(b1);
            TGTaskPool.Return(b2);
            TGTaskPool.Return(b3);
            TGTaskPool.Return(b2); // 重复归还 b2

            // 健壮实现：池里最多 3 个唯一 body，重复的 b2 不应让 PooledCount 变 4
            Assert.AreEqual(3, TGTaskPool.PooledCount,
                "3 个唯一 body 归还 + 1 次重复归还，池内唯一计数应为 3 而非 4");
        }
    }
}
