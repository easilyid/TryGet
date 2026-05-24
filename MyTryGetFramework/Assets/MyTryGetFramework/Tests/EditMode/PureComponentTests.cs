using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.9 Iter 2 — IPureComponent / IComponentSystem 测试。
    /// 覆盖 Add/Get/Has/Remove + class/struct component + Aspect 双轨独立性 + Destroy 清理 + Demo IComponentSystem。
    /// </summary>
    [TestFixture]
    public class PureComponentTests
    {
        // ===== Demo components =====

        /// <summary>Class component — 测试引用类型存储。</summary>
        private sealed class HealthComponent : IPureComponent
        {
            public int Current;
            public int Max;
            public HealthComponent(int current, int max) { Current = current; Max = max; }
        }

        /// <summary>Struct component — 测试值类型 boxed 存储。</summary>
        private struct PositionComponent : IPureComponent
        {
            public float X;
            public float Y;
            public PositionComponent(float x, float y) { X = x; Y = y; }
        }

        /// <summary>Demo System — 演示 V0.9 业务显式调度模式（PRD §2.2）。</summary>
        private sealed class HealthSystem : IComponentSystem<HealthComponent>
        {
            public List<string> Log = new List<string>();

            public void OnAttach(Entity entity, HealthComponent component)
            { Log.Add($"attach:{entity.Id}:{component.Current}/{component.Max}"); }

            public void OnDetach(Entity entity, HealthComponent component)
            { Log.Add($"detach:{entity.Id}"); }
        }

        /// <summary>同概念但 Aspect 风格 — 用于"双轨独立"测试。</summary>
        private sealed class HealthAspect : Aspect
        {
            public int Current { get; set; } = 100;
        }

        // ===== Add =====

        [Test]
        public void AddComponent_StoresComponent()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            var hc = new HealthComponent(80, 100);

            entity.AddComponent(hc);

            Assert.AreEqual(1, entity.ComponentCount());
        }

        [Test]
        public void AddComponent_NullThrows()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            Assert.Throws<ArgumentNullException>(() => entity.AddComponent<HealthComponent>(null));
        }

        [Test]
        public void AddComponent_SameType_Twice_Throws()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            entity.AddComponent(new HealthComponent(80, 100));
            Assert.Throws<InvalidOperationException>(() => entity.AddComponent(new HealthComponent(50, 50)));
        }

        [Test]
        public void AddComponent_OnDestroyedEntity_Throws()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            world.DestroyEntity(entity);
            Assert.Throws<InvalidOperationException>(() => entity.AddComponent(new HealthComponent(80, 100)));
        }

        // ===== Get =====

        [Test]
        public void GetComponent_ReturnsStored()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            var hc = new HealthComponent(80, 100);
            entity.AddComponent(hc);

            var got = entity.GetComponent<HealthComponent>();
            Assert.AreSame(hc, got);
            Assert.AreEqual(80, got.Current);
        }

        [Test]
        public void GetComponent_NotPresent_ClassReturnsNull()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            Assert.IsNull(entity.GetComponent<HealthComponent>());
        }

        [Test]
        public void GetComponent_StructComponent_BoxingRoundtrip()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            entity.AddComponent(new PositionComponent(3f, 4f));

            var p = entity.GetComponent<PositionComponent>();
            Assert.AreEqual(3f, p.X);
            Assert.AreEqual(4f, p.Y);
        }

        // ===== Has =====

        [Test]
        public void HasComponent_AfterAdd_True()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            entity.AddComponent(new HealthComponent(80, 100));
            Assert.IsTrue(entity.HasComponent<HealthComponent>());
        }

        [Test]
        public void HasComponent_NotPresent_False()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            Assert.IsFalse(entity.HasComponent<HealthComponent>());
        }

        // ===== Remove =====

        [Test]
        public void RemoveComponent_Present_RemovesAndReturnsTrue()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            entity.AddComponent(new HealthComponent(80, 100));
            Assert.IsTrue(entity.RemoveComponent<HealthComponent>());
            Assert.IsFalse(entity.HasComponent<HealthComponent>());
            Assert.AreEqual(0, entity.ComponentCount());
        }

        [Test]
        public void RemoveComponent_NotPresent_ReturnsFalse()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            Assert.IsFalse(entity.RemoveComponent<HealthComponent>());
        }

        [Test]
        public void RemoveComponent_OnDestroyedEntity_Throws()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            entity.AddComponent(new HealthComponent(80, 100));
            world.DestroyEntity(entity);
            Assert.Throws<InvalidOperationException>(() => entity.RemoveComponent<HealthComponent>());
        }

        // ===== 双轨独立 =====

        [Test]
        public void PureComponent_AndAspect_AreIndependentChannels()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();

            entity.Attach(new HealthAspect()); // Aspect 通道
            entity.AddComponent(new HealthComponent(80, 100)); // PureComponent 通道

            // 各自有自己的查询路径
            Assert.IsTrue(entity.HasAspect<HealthAspect>());
            Assert.IsTrue(entity.HasComponent<HealthComponent>());

            // 不同概念可同时存在（不冲突）
            Assert.AreEqual(1, entity.AspectCount);
            Assert.AreEqual(1, entity.ComponentCount());

            // 移除 Component 不影响 Aspect
            entity.RemoveComponent<HealthComponent>();
            Assert.IsTrue(entity.HasAspect<HealthAspect>());
            Assert.IsFalse(entity.HasComponent<HealthComponent>());
        }

        // ===== Destroy 清理 =====

        [Test]
        public void Destroy_ClearsComponents()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            entity.AddComponent(new HealthComponent(80, 100));
            entity.AddComponent(new PositionComponent(1f, 2f));
            Assert.AreEqual(2, entity.ComponentCount());

            world.DestroyEntity(entity);

            // Destroy 后 Get/Has 仍可调（不抛），Component 已清空
            Assert.IsFalse(entity.HasComponent<HealthComponent>());
            Assert.AreEqual(0, entity.ComponentCount());
        }

        // ===== Demo: 业务显式调度 IComponentSystem =====

        [Test]
        public void IComponentSystem_ExplicitDispatch_Demo()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            var hc = new HealthComponent(80, 100);
            var system = new HealthSystem();

            // V0.9 不自动调度：业务显式调
            entity.AddComponent(hc);
            system.OnAttach(entity, hc);

            // ...运行期某时刻...
            system.OnDetach(entity, hc);
            entity.RemoveComponent<HealthComponent>();

            Assert.AreEqual(2, system.Log.Count);
            StringAssert.StartsWith("attach:", system.Log[0]);
            StringAssert.StartsWith("detach:", system.Log[1]);
        }

        // ===== ComponentCount 跟踪 =====

        [Test]
        public void ComponentCount_TracksAddAndRemove()
        {
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            Assert.AreEqual(0, entity.ComponentCount());

            entity.AddComponent(new HealthComponent(80, 100));
            Assert.AreEqual(1, entity.ComponentCount());

            entity.AddComponent(new PositionComponent(1f, 2f));
            Assert.AreEqual(2, entity.ComponentCount());

            entity.RemoveComponent<HealthComponent>();
            Assert.AreEqual(1, entity.ComponentCount());

            entity.RemoveComponent<PositionComponent>();
            Assert.AreEqual(0, entity.ComponentCount());
        }

        // ===== 懒初始化（不分配 dict 直到 Add） =====

        [Test]
        public void NoAllocation_BeforeFirstAdd()
        {
            // 间接验证：未 Add 时 Has 立刻 false（路径不抛、不分配）
            var world = new EntityWorld("Test");
            var entity = world.CreateEntity();
            Assert.IsFalse(entity.HasComponent<HealthComponent>());
            Assert.AreEqual(0, entity.ComponentCount());
            // RemoveComponent 在未分配时也走"_components == null"短路返回 false
            Assert.IsFalse(entity.RemoveComponent<HealthComponent>());
        }
    }
}
