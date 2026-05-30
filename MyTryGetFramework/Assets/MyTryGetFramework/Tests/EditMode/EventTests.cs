using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// IEventBus 全局事件测试。
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
            public int EntityId;
        }

        private struct OtherEvent
        {
            public int Value;
        }

        [Test]
        public void Publish_SubscriberReceivesEvent()
        {
            var bus = new EventBus();
            int received = 0;

            bus.Subscribe<DamageEvent>(evt => { received = evt.Amount; });
            bus.Publish(new DamageEvent { Amount = 25 });

            Assert.AreEqual(25, received);
        }

        [Test]
        public void Publish_UnsubscribedHandler_NotCalled()
        {
            var bus = new EventBus();
            int callCount = 0;

            void Handler(DamageEvent evt) => callCount++;
            bus.Subscribe<DamageEvent>(Handler);
            bus.Unsubscribe<DamageEvent>(Handler);

            bus.Publish(new DamageEvent { Amount = 10 });

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllReceive()
        {
            var bus = new EventBus();
            int count1 = 0, count2 = 0;

            bus.Subscribe<SpawnEvent>(_ => count1++);
            bus.Subscribe<SpawnEvent>(_ => count2++);
            bus.Publish(new SpawnEvent { EntityId = 1 });

            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Publish_DifferentEventTypes_AreIsolated()
        {
            var bus = new EventBus();
            int damageCount = 0;
            int spawnCount = 0;

            bus.Subscribe<DamageEvent>(_ => damageCount++);
            bus.Subscribe<SpawnEvent>(_ => spawnCount++);

            bus.Publish(new DamageEvent { Amount = 5 });

            Assert.AreEqual(1, damageCount);
            Assert.AreEqual(0, spawnCount);
        }

        [Test]
        public void Subscribe_DuplicateHandler_Throws()
        {
            var bus = new EventBus();

            void Handler(DamageEvent evt) { }

            bus.Subscribe<DamageEvent>(Handler);

            Assert.Throws<System.InvalidOperationException>(() => bus.Subscribe<DamageEvent>(Handler));
        }

        [Test]
        public void Publish_UnsubscribeDuringDispatch_AffectsNextPublishOnly()
        {
            var bus = new EventBus();
            int firstCount = 0;
            int secondCount = 0;

            void First(DamageEvent evt)
            {
                firstCount++;
                bus.Unsubscribe<DamageEvent>(Second);
            }

            void Second(DamageEvent evt)
            {
                secondCount++;
            }

            bus.Subscribe<DamageEvent>(First);
            bus.Subscribe<DamageEvent>(Second);

            bus.Publish(new DamageEvent());
            bus.Publish(new DamageEvent());

            Assert.AreEqual(2, firstCount);
            Assert.AreEqual(1, secondCount);
        }

        [Test]
        public void Publish_SubscribeDuringDispatch_AffectsNextPublishOnly()
        {
            var bus = new EventBus();
            int firstCount = 0;
            int secondCount = 0;

            void Second(DamageEvent evt)
            {
                secondCount++;
            }

            void First(DamageEvent evt)
            {
                firstCount++;
                if (bus.GetSubscriberCount<DamageEvent>() == 1)
                    bus.Subscribe<DamageEvent>(Second);
            }

            bus.Subscribe<DamageEvent>(First);

            bus.Publish(new DamageEvent());
            bus.Publish(new DamageEvent());

            Assert.AreEqual(2, firstCount);
            Assert.AreEqual(1, secondCount);
        }

        [Test]
        public void Publish_HandlerThrows_StopsDispatchAndPropagates()
        {
            var bus = new EventBus();
            int callCount = 0;

            bus.Subscribe<DamageEvent>(_ => throw new System.InvalidOperationException("boom"));
            bus.Subscribe<DamageEvent>(_ => callCount++);

            Assert.Throws<System.InvalidOperationException>(() => bus.Publish(new DamageEvent()));
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Diagnostics_ReportSubscriberCountsAndEventTypes()
        {
            var bus = new EventBus();

            bus.Subscribe<DamageEvent>(_ => { });
            bus.Subscribe<SpawnEvent>(_ => { });

            Assert.AreEqual(1, bus.GetSubscriberCount<DamageEvent>());
            Assert.AreEqual(1, bus.GetSubscriberCount<SpawnEvent>());
            Assert.AreEqual(0, bus.GetSubscriberCount<OtherEvent>());
            CollectionAssert.AreEquivalent(new[] { typeof(DamageEvent), typeof(SpawnEvent) }, bus.GetEventTypes());
        }

        [Test]
        public void EventHandlerRegistry_Snapshot_ReturnsRegisteredHandlers()
        {
            EventHandlerRegistry.ClearForTests();
            Action<IEventBus> registration = bus => bus.Subscribe<DamageEvent>(_ => { });

            EventHandlerRegistry.Register(registration);

            Assert.AreEqual(1, EventHandlerRegistry.Count);
            CollectionAssert.AreEqual(new[] { registration }, EventHandlerRegistry.Snapshot());

            EventHandlerRegistry.ClearForTests();
        }
    }
}
