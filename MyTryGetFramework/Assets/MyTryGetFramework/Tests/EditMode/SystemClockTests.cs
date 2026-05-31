using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.7 Iter 1 — IClock + SystemClock 测试。
    /// </summary>
    [TestFixture]
    public class SystemClockTests
    {
        [Test]
        public void NewClock_AllValuesZero()
        {
            var clock = new SystemClock();
            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.AreEqual(0f, clock.UnscaledDeltaTime);
            Assert.AreEqual(0.0, clock.ElapsedTime);
            Assert.AreEqual(0.0, clock.UnscaledElapsedTime);
            Assert.AreEqual(0L, clock.FrameCount);
        }

        [Test]
        public void Update_SetsCurrentDeltaTime()
        {
            var clock = new SystemClock();
            clock.Update(0.016f, 0.020f);

            Assert.AreEqual(0.016f, clock.DeltaTime);
            Assert.AreEqual(0.020f, clock.UnscaledDeltaTime);
        }

        [Test]
        public void Update_AccumulatesElapsedTime()
        {
            var clock = new SystemClock();
            clock.Update(0.5f, 0.5f);
            clock.Update(0.3f, 0.3f);
            clock.Update(0.2f, 0.2f);

            Assert.AreEqual(1.0, clock.ElapsedTime, 0.0001);
            Assert.AreEqual(1.0, clock.UnscaledElapsedTime, 0.0001);
        }

        [Test]
        public void Update_ScaledAndUnscaledTrackedIndependently()
        {
            var clock = new SystemClock();
            // timeScale=0 模拟：scaled dt=0，unscaled dt 仍走
            clock.Update(0f, 0.016f);
            clock.Update(0f, 0.016f);
            clock.Update(0f, 0.016f);

            Assert.AreEqual(0.0, clock.ElapsedTime, 0.0001);
            Assert.AreEqual(0.048, clock.UnscaledElapsedTime, 0.0001);
        }

        [Test]
        public void Update_IncrementsFrameCount()
        {
            var clock = new SystemClock();
            Assert.AreEqual(0L, clock.FrameCount);

            for (int i = 1; i <= 100; i++)
            {
                clock.Update(0.016f, 0.016f);
                Assert.AreEqual(i, clock.FrameCount);
            }
        }

        [Test]
        public void Shutdown_ResetsAllState()
        {
            var clock = new SystemClock();
            clock.Update(0.016f, 0.020f);
            clock.Update(0.016f, 0.020f);

            clock.Shutdown();

            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.AreEqual(0f, clock.UnscaledDeltaTime);
            Assert.AreEqual(0.0, clock.ElapsedTime);
            Assert.AreEqual(0.0, clock.UnscaledElapsedTime);
            Assert.AreEqual(0L, clock.FrameCount);
        }

        [Test]
        public void ModuleSystem_RegisterAndDrive()
        {
            var host = new ModuleSystem();
            host.Register<IClock>(new SystemClock());
            host.Initialize();

            var clock = host.Get<IClock>();
            Assert.AreEqual(0L, clock.FrameCount);

            host.Update(0.016f, 0.016f);
            Assert.AreEqual(1L, clock.FrameCount);
            Assert.AreEqual(0.016f, clock.DeltaTime, 0.0001f);
            Assert.AreEqual(0.016, clock.ElapsedTime, 0.0001);

            host.Update(0.020f, 0.020f);
            Assert.AreEqual(2L, clock.FrameCount);
            Assert.AreEqual(0.020f, clock.DeltaTime, 0.0001f);
            Assert.AreEqual(0.036, clock.ElapsedTime, 0.0001);

            host.Shutdown();
        }

        [Test]
        public void ModuleSystem_SystemClockPriority_BeforeOtherUpdateModules()
        {
            // SystemClock.Priority=-900 应早于业务 Module Update。
            // 验证：注册一个 sentinel Module，sentinel 在 Update 时读 clock.FrameCount，
            // 应能读到 +1 后的最新值（说明 clock 已 Update 完）。
            var host = new ModuleSystem();
            var sentinel = new FrameCountSentinel();
            host.Register<IClock>(new SystemClock());
            host.Register<IFrameCountSentinel>(sentinel);
            host.Initialize();

            host.Update(0.016f, 0.016f);

            Assert.AreEqual(1L, sentinel.LastObservedFrameCount,
                "Sentinel 在自己 Update 时应已观察到 clock.FrameCount=1（说明 SystemClock 先于 sentinel）");

            host.Shutdown();
        }

        // ----- helpers -----

        private interface IFrameCountSentinel : IModule, IUpdateModule
        {
            long LastObservedFrameCount { get; }
        }

        private sealed class FrameCountSentinel : IFrameCountSentinel
        {
            public int Priority => -100; // 业务级，晚于 SystemClock=-900
            public System.Collections.Generic.IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

            public long LastObservedFrameCount { get; private set; }
            private IClock _clock;

            public void OnInit(IModuleSystem host) { _clock = host.Get<IClock>(); }
            public void Shutdown() { _clock = null; LastObservedFrameCount = 0; }
            public void Update(float dt, float udt) { LastObservedFrameCount = _clock.FrameCount; }
        }
    }
}
