using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// Event 测试：Entity-level 和 World-level 事件 (ADR-0010)。
    /// </summary>
    [TestFixture]
    public class EventTests
    {
        private struct DamageEvent
        {
            public int Amount;
        }

        private struct SpawnEvent
        {
            public EntityId EntityId;
        }

        private class HealthAspect : Aspect
        {
            public int Current { get; set; } = 100;

            public void TakeDamage(int amount)
            {
                Current -= amount;
                EventDispatcher?.Publish(new DamageEvent { Amount = amount });
            }
        }

        #region Entity-level Event

        [Test]
        public void EntityEvent_SubscriberReceivesEvent()
        {
            var world = new EntityWorld("Test");
            Entity entity = world.CreateEntity();
            int received = 0;

            entity.Subscribe<DamageEvent>(evt => { received = evt.Amount; });

            var health = new HealthAspect();
            entity.Attach(health);
            health.TakeDamage(25);

            Assert.AreEqual(25, received);

            world.Shutdown();
        }

        [Test]
        public void EntityEvent_UnsubscribedHandler_NotCalled()
        {
            var world = new EntityWorld("Test");
            Entity entity = world.CreateEntity();
            int callCount = 0;

            void Handler(DamageEvent evt) => callCount++;
            entity.Subscribe<DamageEvent>(Handler);
            entity.Unsubscribe<DamageEvent>(Handler);

            var health = new HealthAspect();
            entity.Attach(health);
            health.TakeDamage(10);

            Assert.AreEqual(0, callCount);

            world.Shutdown();
        }

        #endregion

        #region World-level Event

        [Test]
        public void WorldEvent_SubscriberReceivesEvent()
        {
            var world = new EntityWorld("Test");
            int receivedCount = 0;

            world.EventBus.Subscribe<SpawnEvent>(evt => { receivedCount++; });
            world.EventBus.Publish(new SpawnEvent { EntityId = new EntityId(1, 1) });

            Assert.AreEqual(1, receivedCount);

            world.Shutdown();
        }

        [Test]
        public void WorldEvent_UnsubscribedHandler_NotCalled()
        {
            var world = new EntityWorld("Test");
            int callCount = 0;

            void Handler(SpawnEvent evt) => callCount++;
            world.EventBus.Subscribe<SpawnEvent>(Handler);
            world.EventBus.Unsubscribe<SpawnEvent>(Handler);

            world.EventBus.Publish(new SpawnEvent { EntityId = new EntityId(1, 1) });

            Assert.AreEqual(0, callCount);

            world.Shutdown();
        }

        [Test]
        public void WorldEvent_MultipleSubscribers_AllReceive()
        {
            var world = new EntityWorld("Test");
            int count1 = 0, count2 = 0;

            world.EventBus.Subscribe<SpawnEvent>(_ => count1++);
            world.EventBus.Subscribe<SpawnEvent>(_ => count2++);
            world.EventBus.Publish(new SpawnEvent());

            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);

            world.Shutdown();
        }

        #endregion
    }
}
