using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// PoolModule.Return 的 onReturn 回调异常隔离测试。
    ///
    /// 背景：PoolModule.ObjectPool.Return（PoolModule.cs:113-128）执行顺序为：
    ///   1. DEBUG: _activeSet.Remove(item) —— 先从 active 集合移除
    ///   2. _onReturn?.Invoke(item) —— 调用户回调（【无 try/catch】）
    ///   3. _idle.Push(item) —— 入栈
    ///   4. _totalReturned++
    ///
    /// 若 onReturn 抛异常，步骤 3/4 不执行：对象已从 _activeSet 移除（DEBUG）但未入 _idle，
    /// 进入「既不 active 也不 idle」的泄漏态，且异常冒泡到调用方。这与 TimerModule 的「回调异常
    /// 聚合成 AggregateException、同帧其他 timer 不受影响」哲学不一致。
    ///
    /// 这些测试断言【健壮的期望行为】：onReturn 抛异常时对象仍正确入池（或至少不进入泄漏态），
    /// 不影响后续 Rent/Return。当前实现下预期【红灯】；修复后转绿。
    /// </summary>
    [TestFixture]
    public class PoolOnReturnExceptionTests
    {
        private class Dummy
        {
            public int Tag;
        }

        /// <summary>
        /// onReturn 抛异常时，Return 应隔离异常、对象仍入池（IdleCount 增）。
        /// 当前实现：异常抛穿，IdleCount 不增，测试在 Assert.DoesNotThrow 处红灯。
        /// </summary>
        [Test]
        public void Return_OnReturnThrows_ObjectStillReturnedToPool()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy(),
                onReturn: d => throw new InvalidOperationException("onReturn boom"));

            var d = pool.Rent();

            Assert.DoesNotThrow(() => pool.Return(d),
                "onReturn 回调异常应被隔离，不应冒泡到调用方");

            Assert.AreEqual(1, pool.IdleCount,
                "onReturn 异常后对象仍应入池，不进入泄漏态");
        }

        /// <summary>
        /// onReturn 异常不影响该对象后续被复用：Return（异常隔离后）→ Rent 应拿到同一实例。
        /// </summary>
        [Test]
        public void Return_OnReturnThrows_ObjectReusableAfterIsolatedReturn()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy(),
                onReturn: d => throw new InvalidOperationException("onReturn boom"));

            var first = pool.Rent();
            pool.Return(first); // 即使 onReturn 抛，对象应入池

            var second = pool.Rent();

            Assert.AreSame(first, second,
                "onReturn 异常隔离后，对象应正常回收复用");
        }

        /// <summary>
        /// 一个对象的 onReturn 异常不应影响其他对象的归还。
        /// 当前实现：第一个 Return 抛出后调用方若不 catch 就到不了第二个 Return；
        /// 即使调用方 catch，第一个对象已泄漏（DEBUG 下不在 _activeSet 也不在 _idle）。
        /// </summary>
        [Test]
        public void Return_OnReturnThrows_OtherReturnsUnaffected()
        {
            int throwOnTag = 1;
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy(),
                onReturn: d =>
                {
                    if (d.Tag == throwOnTag)
                        throw new InvalidOperationException("onReturn boom for tag 1");
                });

            var d1 = pool.Rent(); d1.Tag = 1;
            var d2 = pool.Rent(); d2.Tag = 2;

            // d1 的 onReturn 会抛，但应被隔离；d2 正常归还
            try { pool.Return(d1); } catch { /* 当前实现会抛，调用方被迫 catch —— 健壮实现不应抛 */ }

            Assert.DoesNotThrow(() => pool.Return(d2),
                "d1 的 onReturn 异常不应影响 d2 的归还");

            Assert.AreEqual(2, pool.IdleCount,
                "两个对象都应入池（d1 的 onReturn 异常被隔离）");
        }

        /// <summary>
        /// DEBUG 下验证泄漏态：onReturn 抛异常后，该对象既不在 active 也不在 idle。
        /// 通过 GetDiagnostics 间接观察：Return 未完成 → totalReturned 不增。
        /// 健壮实现：onReturn 异常隔离后 totalReturned 应自增。
        /// </summary>
        [Test]
        public void Return_OnReturnThrows_TotalReturnedStillIncrements()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy(),
                onReturn: d => throw new InvalidOperationException("onReturn boom"));

            var d = pool.Rent();

            try { pool.Return(d); } catch { /* 当前实现抛 */ }

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(1, diag.TotalReturned,
                "onReturn 异常隔离后，TotalReturned 应自增（对象完成归还语义）");
        }
    }
}
