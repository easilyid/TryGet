using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// PoolModule（IPoolModule + 内部 ObjectPool）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class PoolModuleTests
    {
        private class Dummy
        {
            public int Tag;
        }

        private class Other
        {
            public string Name;
        }

        [Test]
        public void Priority_IsHalfTrack()
        {
            var p = new PoolModule();
            Assert.AreEqual(-500, p.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var p = new PoolModule();
            Assert.AreEqual(0, p.DependsOn.Count);
        }

        [Test]
        public void GetOrCreatePool_FirstCall_CreatesPool()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());
            Assert.IsNotNull(pool);
            Assert.AreEqual(0, pool.IdleCount);
        }

        [Test]
        public void GetOrCreatePool_SecondCallSameType_ReturnsSameInstance()
        {
            var p = new PoolModule();
            var pool1 = p.GetOrCreatePool(() => new Dummy());
            var pool2 = p.GetOrCreatePool(() => new Dummy());
            Assert.AreSame(pool1, pool2);
        }

        [Test]
        public void GetOrCreatePool_DifferentTypes_ReturnDifferentPools()
        {
            var p = new PoolModule();
            var poolA = p.GetOrCreatePool(() => new Dummy());
            var poolB = p.GetOrCreatePool(() => new Other());

            Assert.AreNotSame((object)poolA, (object)poolB);
        }

        [Test]
        public void GetOrCreatePool_NullFactory_Throws()
        {
            var p = new PoolModule();
            Assert.Throws<ArgumentNullException>(() => p.GetOrCreatePool<Dummy>(null));
        }

        [Test]
        public void GetOrCreatePool_NegativeInitialSize_Throws()
        {
            var p = new PoolModule();
            Assert.Throws<ArgumentOutOfRangeException>(() => p.GetOrCreatePool(() => new Dummy(), initialSize: -1));
        }

        [Test]
        public void GetOrCreatePool_InitialSize_PrefillsIdleCount()
        {
            var p = new PoolModule();
            int created = 0;
            var pool = p.GetOrCreatePool(() => { created++; return new Dummy(); }, initialSize: 3);

            Assert.AreEqual(3, pool.IdleCount);
            Assert.AreEqual(3, created);
        }

        [Test]
        public void Rent_EmptyPool_CallsFactory()
        {
            var p = new PoolModule();
            int created = 0;
            var pool = p.GetOrCreatePool(() => { created++; return new Dummy(); });

            var item = pool.Rent();

            Assert.IsNotNull(item);
            Assert.AreEqual(1, created);
            Assert.AreEqual(0, pool.IdleCount);
        }

        [Test]
        public void Rent_NonEmptyPool_DoesNotCallFactory()
        {
            var p = new PoolModule();
            int created = 0;
            var pool = p.GetOrCreatePool(() => { created++; return new Dummy(); }, initialSize: 2);
            Assert.AreEqual(2, created);  // initial 预填

            var item = pool.Rent();

            Assert.IsNotNull(item);
            Assert.AreEqual(2, created, "Rent 从池里取，不应再触发工厂");
            Assert.AreEqual(1, pool.IdleCount);
        }

        [Test]
        public void Return_IncrementsIdleCount()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            // C9：先 Rent 再 Return（DEBUG 模式下会检测对象来源）
            var d1 = pool.Rent();
            var d2 = pool.Rent();
            pool.Return(d1);
            pool.Return(d2);

            Assert.AreEqual(2, pool.IdleCount);
        }

        [Test]
        public void Return_InvokesOnReturnHook()
        {
            var p = new PoolModule();
            int resets = 0;
            var pool = p.GetOrCreatePool(() => new Dummy(),
                onReturn: d => { resets++; d.Tag = 0; });

            // C9：先 Rent 再 Return
            var d = pool.Rent();
            d.Tag = 42;
            pool.Return(d);

            Assert.AreEqual(1, resets);
            Assert.AreEqual(0, d.Tag);
        }

        [Test]
        public void Return_NullItem_IsIgnored()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            pool.Return(null);

            Assert.AreEqual(0, pool.IdleCount);
        }

        [Test]
        public void RentReturnCycle_ReusesSameInstance()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            var first = pool.Rent();
            pool.Return(first);
            var second = pool.Rent();

            Assert.AreSame(first, second);
        }

        [Test]
        public void DestroyPool_RemovesExisting_ReturnsTrue()
        {
            var p = new PoolModule();
            p.GetOrCreatePool(() => new Dummy());

            Assert.IsTrue(p.DestroyPool<Dummy>());
        }

        [Test]
        public void DestroyPool_NoSuchType_ReturnsFalse()
        {
            var p = new PoolModule();
            Assert.IsFalse(p.DestroyPool<Dummy>());
        }

        [Test]
        public void DestroyPool_AfterDestroy_GetOrCreateReturnsFreshPool()
        {
            var p = new PoolModule();
            var poolA = p.GetOrCreatePool(() => new Dummy(), initialSize: 2);
            p.DestroyPool<Dummy>();

            var poolB = p.GetOrCreatePool(() => new Dummy());

            Assert.AreNotSame(poolA, poolB);
            Assert.AreEqual(0, poolB.IdleCount, "新池没有继承旧池的预填");
        }

        [Test]
        public void Shutdown_ClearsAllPools()
        {
            var p = new PoolModule();
            p.GetOrCreatePool(() => new Dummy());
            p.GetOrCreatePool(() => new Other());

            p.Shutdown();

            // Shutdown 后再 GetOrCreate 应得到新池而非旧池
            var freshA = p.GetOrCreatePool(() => new Dummy(), initialSize: 2);
            Assert.AreEqual(2, freshA.IdleCount);
        }

        [Test]
        public void IntegratesWithModuleSystem()
        {
            var host = new ModuleSystem();
            var pools = new PoolModule();
            host.Register<IPoolModule>(pools);
            host.Initialize();

            var pool = host.Get<IPoolModule>().GetOrCreatePool(() => new Dummy());
            var d = pool.Rent();
            pool.Return(d);
            Assert.AreEqual(1, pool.IdleCount);

            host.Shutdown();
        }

        // —— 来自 Stage-2 review 建议：double Return + 非 Rent 来源对象 ——

        [Test]
        public void DoubleReturn_SameInstance_IsDocumentedUndefinedBehavior()
        {
#if UNITY_ASSERTIONS || DEBUG
            // C9 DEBUG/UNITY_ASSERTIONS 模式：重复 Return 被检测并抛异常
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());
            var d = pool.Rent(); // C9：必须先 Rent
            d.Tag = 1;

            pool.Return(d);
            Assert.Throws<InvalidOperationException>(() => pool.Return(d),
                "C9: 重复 Return 应抛 InvalidOperationException");
#else
            // Release 模式：V0.2 不强制检测，但记录行为：IdleCount 会被错误地增加 2，
            // 之后 Rent 两次会得到同一引用。调用方负责避免重复 Return（见 IObjectPool.Return XML 注释）。
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());
            var d = new Dummy { Tag = 1 };

            pool.Return(d);
            pool.Return(d);

            Assert.AreEqual(2, pool.IdleCount, "未定义行为：IdleCount 计数会偏高");
            var first = pool.Rent();
            var second = pool.Rent();
            Assert.AreSame(first, second, "未定义行为：两次 Rent 给出同一实例");
#endif
        }

        [Test]
        public void ReturnObjectNotFromRent_IsAccepted_NoCrash()
        {
#if UNITY_ASSERTIONS || DEBUG
            // C9 DEBUG/UNITY_ASSERTIONS 模式：Return 未 Rent 的对象被检测并抛异常
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            Assert.Throws<InvalidOperationException>(() => pool.Return(new Dummy()),
                "C9: Return 未 Rent 的对象应抛 InvalidOperationException");
#else
            // Release 模式：池不验证来源（V0.2 简化），手动 new 的对象 Return 进去也接受。
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            pool.Return(new Dummy());

            Assert.AreEqual(1, pool.IdleCount);
#endif
        }
    }
}
