using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// Entity 组合测试：Aspect 挂载/卸载、唯一性约束。
    /// </summary>
    [TestFixture]
    public class EntityAspectTests
    {
        private class HealthAspect : Aspect
        {
            public int Current { get; set; } = 100;
            public int Max { get; set; } = 100;

            public void Clamp()
            {
                if (Current < 0) Current = 0;
                if (Current > Max) Current = Max;
            }
        }

        private class MoveAspect : Aspect
        {
            public float Speed { get; set; } = 5f;
        }

        [Test]
        public void AttachAspect_EntityHasAspect()
        {
            var world = new EntityWorld("Test");
            Entity entity = world.CreateEntity();
            var health = new HealthAspect();

            entity.Attach(health);

            Assert.IsTrue(entity.HasAspect<HealthAspect>());
            Assert.AreEqual(health, entity.GetAspect<HealthAspect>());
            Assert.AreEqual(1, entity.AspectCount);

            world.Shutdown();
        }

        [Test]
        public void DetachAspect_EntityNoLongerHasAspect()
        {
            var world = new EntityWorld("Test");
            Entity entity = world.CreateEntity();
            var health = new HealthAspect();

            entity.Attach(health);
            bool detached = entity.Detach(health);

            Assert.IsTrue(detached);
            Assert.IsFalse(entity.HasAspect<HealthAspect>());
            Assert.AreEqual(0, entity.AspectCount);

            world.Shutdown();
        }

        [Test]
        public void AttachDuplicateAspectType_Throws()
        {
            var world = new EntityWorld("Test");
            Entity entity = world.CreateEntity();
            entity.Attach(new HealthAspect());

            Assert.Throws<InvalidOperationException>(() =>
            {
                entity.Attach(new HealthAspect());
            });

            world.Shutdown();
        }

        [Test]
        public void MultipleAspectTypes_Coexist()
        {
            var world = new EntityWorld("Test");
            Entity entity = world.CreateEntity();
            entity.Attach(new HealthAspect());
            entity.Attach(new MoveAspect());

            Assert.IsTrue(entity.HasAspect<HealthAspect>());
            Assert.IsTrue(entity.HasAspect<MoveAspect>());
            Assert.AreEqual(2, entity.AspectCount);

            world.Shutdown();
        }

        [Test]
        public void AspectLocalBehavior_OperatesOnOwnState()
        {
            var health = new HealthAspect { Current = 150, Max = 100 };
            health.Clamp();

            Assert.AreEqual(100, health.Current);
        }
    }
}
