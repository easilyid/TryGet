using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// Fake Adapter 测试 (Issue-18)：验证核心运行时可以被非 Unity 的 Adapter 驱动，
    /// 且不产生对 Unity API 的依赖。
    /// </summary>
    [TestFixture]
    public class FakeAdapterTests
    {
        /// <summary>
        /// 一个纯 C# 的 Fake Adapter，模拟外部运行时驱动 World。
        /// 不依赖任何 Unity API。
        /// </summary>
        private class FakeWorldAdapter : IWorldAdapter
        {
            public World World { get; private set; }
            public int TickCount { get; private set; }
            public bool IsDisposed { get; private set; }

            public void Initialize(World world)
            {
                World = world;
                world.Start();
            }

            public void Tick()
            {
                if (World != null && World.State == WorldState.Running)
                {
                    World.Update();
                    TickCount++;
                }
            }

            public void Dispose()
            {
                World?.Shutdown();
                IsDisposed = true;
            }
        }

        private class HealthAspect : Aspect
        {
            public int Current { get; set; } = 100;
        }

        private class CountSystem : SystemBase
        {
            public int ExecuteCount;

            protected override void OnCreate()
            {
                Query = Query.Create().WithAll<HealthAspect>().Build();
            }

            protected override void Execute(IReadOnlyList<Entity> entities)
            {
                ExecuteCount++;
            }
        }

        [Test]
        public void FakeAdapter_DrivesWorldLifecycle()
        {
            var world = new World("FakeTest");
            Entity entity = world.CreateEntity();
            entity.Attach(new HealthAspect());

            var system = new CountSystem();
            world.RegisterSystem(system, Phase.Update);

            var adapter = new FakeWorldAdapter();
            adapter.Initialize(world);

            Assert.AreEqual(WorldState.Running, world.State);

            adapter.Tick();
            adapter.Tick();
            adapter.Tick();

            Assert.AreEqual(3, adapter.TickCount);
            Assert.AreEqual(3, system.ExecuteCount);

            adapter.Dispose();

            Assert.IsTrue(adapter.IsDisposed);
            Assert.AreEqual(WorldState.Shutdown, world.State);
        }

        [Test]
        public void FakeAdapter_NoUnityDependency()
        {
            // 此测试本身在 noEngineReferences=true 的 asmdef 中编译通过
            // 即证明核心运行时和 Adapter 接口不依赖 Unity。
            var world = new World("PureCSharp");
            var adapter = new FakeWorldAdapter();
            adapter.Initialize(world);
            adapter.Tick();
            adapter.Dispose();

            Assert.AreEqual(WorldState.Shutdown, world.State);
        }
    }
}
