using NUnit.Framework;
using TryGet;

namespace TryGet.Tests
{
    /// <summary>
    /// C9 — Pool 诊断快照测试。
    ///
    /// 验证 GetDiagnostics() 返回准确的计数器（Rent/Return/Hit/Miss/Peak/Active/Idle）。
    /// </summary>
    [TestFixture]
    public class PoolDiagnosticsTests
    {
        private class Dummy { public int Tag; }

        [Test]
        public void GetDiagnostics_InitialState_AllZero()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(0, diag.TotalRented);
            Assert.AreEqual(0, diag.TotalReturned);
            Assert.AreEqual(0, diag.CurrentActive);
            Assert.AreEqual(0, diag.IdleCount);
            Assert.AreEqual(0, diag.PeakActive);
            Assert.AreEqual(0, diag.HitCount);
            Assert.AreEqual(0, diag.MissCount);
        }

        [Test]
        public void GetDiagnostics_AfterRent_CountersIncrement()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            pool.Rent();
            pool.Rent();

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(2, diag.TotalRented);
            Assert.AreEqual(0, diag.TotalReturned);
            Assert.AreEqual(2, diag.CurrentActive);
            Assert.AreEqual(0, diag.IdleCount);
            Assert.AreEqual(2, diag.PeakActive);
            Assert.AreEqual(0, diag.HitCount, "池内无对象，全是 miss");
            Assert.AreEqual(2, diag.MissCount);
        }

        [Test]
        public void GetDiagnostics_AfterReturn_CurrentActiveDecreases()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            var d1 = pool.Rent();
            var d2 = pool.Rent();
            pool.Return(d1);

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(2, diag.TotalRented);
            Assert.AreEqual(1, diag.TotalReturned);
            Assert.AreEqual(1, diag.CurrentActive);
            Assert.AreEqual(1, diag.IdleCount);
            Assert.AreEqual(2, diag.PeakActive);
        }

        [Test]
        public void GetDiagnostics_HitCount_IncrementsWhenRentFromIdle()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            var d = pool.Rent();
            pool.Return(d);
            pool.Rent(); // hit from idle

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(2, diag.TotalRented);
            Assert.AreEqual(1, diag.HitCount, "第二次 Rent 是 hit");
            Assert.AreEqual(1, diag.MissCount, "第一次 Rent 是 miss");
        }

        [Test]
        public void GetDiagnostics_PeakActive_TracksPeak()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            var d1 = pool.Rent();
            var d2 = pool.Rent();
            var d3 = pool.Rent();
            pool.Return(d1);
            pool.Return(d2);
            pool.Return(d3);

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(3, diag.PeakActive, "峰值是 3 个同时活跃");
            Assert.AreEqual(0, diag.CurrentActive, "全部归还后当前活跃为 0");
            Assert.AreEqual(3, diag.IdleCount);
        }

        [Test]
        public void GetDiagnostics_WithInitialSize_PrewarmCountsAsMiss()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy(), initialSize: 2);

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(0, diag.TotalRented, "预热不算 Rent");
            Assert.AreEqual(2, diag.IdleCount);
            Assert.AreEqual(0, diag.MissCount, "预热不算 miss");
            Assert.AreEqual(0, diag.HitCount);
        }

        [Test]
        public void GetDiagnostics_RentingPrewarmedObject_CountsAsHit()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy(), initialSize: 2);

            pool.Rent();

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(1, diag.TotalRented);
            Assert.AreEqual(1, diag.CurrentActive);
            Assert.AreEqual(1, diag.IdleCount);
            Assert.AreEqual(1, diag.PeakActive);
            Assert.AreEqual(1, diag.HitCount, "从 initialSize 预热对象 Rent 应计为 hit");
            Assert.AreEqual(0, diag.MissCount);
        }

        [Test]
        public void GetDiagnostics_MultipleRentReturnCycles_AccumulatesCounters()
        {
            var p = new PoolModule();
            var pool = p.GetOrCreatePool(() => new Dummy());

            for (int i = 0; i < 5; i++)
            {
                var d = pool.Rent();
                pool.Return(d);
            }

            var diag = pool.GetDiagnostics();

            Assert.AreEqual(5, diag.TotalRented);
            Assert.AreEqual(5, diag.TotalReturned);
            Assert.AreEqual(0, diag.CurrentActive);
            Assert.AreEqual(1, diag.IdleCount, "只有 1 个对象被反复使用");
            Assert.AreEqual(1, diag.PeakActive);
            Assert.AreEqual(1, diag.MissCount, "第一次是 miss");
            Assert.AreEqual(4, diag.HitCount, "后续 4 次是 hit");
        }
    }
}
