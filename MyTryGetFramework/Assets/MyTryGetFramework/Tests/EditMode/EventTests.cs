using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// IEventModule 全局事件测试。
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
            var bus = new EventModule();
            int received = 0;

            bus.Subscribe<DamageEvent>(evt => { received = evt.Amount; });
            bus.Publish(new DamageEvent { Amount = 25 });

            Assert.AreEqual(25, received);
        }

        [Test]
        public void Publish_UnsubscribedHandler_NotCalled()
        {
            var bus = new EventModule();
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
            var bus = new EventModule();
            int count1 = 0, count2 = 0;

            bus.Subscribe<SpawnEvent>(_ => count1++);
            bus.Subscribe<SpawnEvent>(_ => count2++);
            bus.Publish(new SpawnEvent { EntityId = 1 });

            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Publish_InvokesInSubscribeOrder()
        {
            var bus = new EventModule();
            string order = string.Empty;

            bus.Subscribe<DamageEvent>(_ => order += "A");
            bus.Subscribe<DamageEvent>(_ => order += "B");
            bus.Subscribe<DamageEvent>(_ => order += "C");

            bus.Publish(new DamageEvent());

            Assert.AreEqual("ABC", order);
        }

        [Test]
        public void Publish_NoSubscribers_NoOp()
        {
            var bus = new EventModule();

            Assert.DoesNotThrow(() => bus.Publish(new DamageEvent { Amount = 1 }));
        }

        [Test]
        public void Publish_DifferentEventTypes_AreIsolated()
        {
            var bus = new EventModule();
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
            var bus = new EventModule();

            void Handler(DamageEvent evt) { }

            bus.Subscribe<DamageEvent>(Handler);

            Assert.Throws<InvalidOperationException>(() => bus.Subscribe<DamageEvent>(Handler));
        }

        [Test]
        public void Unsubscribe_NullOrMissing_NoOp()
        {
            var bus = new EventModule();

            Assert.DoesNotThrow(() => bus.Unsubscribe<DamageEvent>(null));

            void Handler(DamageEvent evt) { }
            Assert.DoesNotThrow(() => bus.Unsubscribe<DamageEvent>(Handler));
        }

        [Test]
        public void Publish_UnsubscribeDuringDispatch_AffectsNextPublishOnly()
        {
            var bus = new EventModule();
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
            var bus = new EventModule();
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
        public void Publish_NestedPublish_PendingChangesFlushAtSafeBoundary()
        {
            var bus = new EventModule();
            int firstCount = 0;
            int secondCount = 0;
            int nestedCount = 0;
            bool nestedPublished = false;

            void Second(DamageEvent evt)
            {
                secondCount++;
            }

            void First(DamageEvent evt)
            {
                firstCount++;
                bus.Unsubscribe<DamageEvent>(Second);
                if (!nestedPublished)
                {
                    nestedPublished = true;
                    bus.Publish(new DamageEvent());
                }
            }

            void Nested(SpawnEvent evt)
            {
                nestedCount++;
            }

            bus.Subscribe<DamageEvent>(First);
            bus.Subscribe<DamageEvent>(Second);
            bus.Subscribe<SpawnEvent>(Nested);

            bus.Publish(new DamageEvent());
            bus.Publish(new DamageEvent());
            bus.Publish(new SpawnEvent());

            Assert.AreEqual(3, firstCount);
            Assert.AreEqual(2, secondCount, "Unsubscribe during outer publish must not affect nested same-event publish until the safe flush boundary.");
            Assert.AreEqual(1, nestedCount);
        }

        [Test]
        public void Publish_UnsubscribeThenResubscribeDuringDispatch_AffectsNextPublishOnly()
        {
            var bus = new EventModule();
            int callCount = 0;

            void Handler(DamageEvent evt)
            {
                callCount++;
                bus.Unsubscribe<DamageEvent>(Handler);
                bus.Subscribe<DamageEvent>(Handler);
            }

            bus.Subscribe<DamageEvent>(Handler);

            Assert.DoesNotThrow(() => bus.Publish(new DamageEvent()));
            Assert.DoesNotThrow(() => bus.Publish(new DamageEvent()));

            Assert.AreEqual(2, callCount);
            Assert.AreEqual(1, bus.GetSubscriberCount<DamageEvent>());
        }

        [Test]
        public void Publish_LastHandlerUnsubscribesDuringDispatch_RemovesEventTypeFromDiagnostics()
        {
            var bus = new EventModule();

            void Handler(DamageEvent evt)
            {
                bus.Unsubscribe<DamageEvent>(Handler);
            }

            bus.Subscribe<DamageEvent>(Handler);
            bus.Publish(new DamageEvent());

            Assert.AreEqual(0, bus.GetSubscriberCount<DamageEvent>());
            CollectionAssert.DoesNotContain(bus.GetEventTypes(), typeof(DamageEvent));
        }

        [Test]
        public void Publish_NestedPublish_AccumulatesExceptionsUntilOuterBoundary()
        {
            var bus = new EventModule();
            bool nestedPublished = false;

            void First(DamageEvent evt)
            {
                if (!nestedPublished)
                {
                    nestedPublished = true;
                    bus.Publish(new DamageEvent());
                }

                throw new InvalidOperationException("outer");
            }

            void Second(DamageEvent evt)
            {
                throw new ArgumentException("inner-or-outer");
            }

            bus.Subscribe<DamageEvent>(First);
            bus.Subscribe<DamageEvent>(Second);

            bus.Publish(new DamageEvent());

            Assert.AreEqual(4, bus.GetLastPublishExceptions<DamageEvent>().Count);
        }

        [Test]
        public void Publish_HandlerThrows_ContinuesAndReports()
        {
            var bus = new EventModule();
            int callCount = 0;

            bus.Subscribe<DamageEvent>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<DamageEvent>(_ => callCount++);

            Assert.DoesNotThrow(() => bus.Publish(new DamageEvent()));
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(1, bus.GetLastPublishExceptions<DamageEvent>().Count);
            Assert.IsInstanceOf<InvalidOperationException>(bus.GetLastPublishExceptions<DamageEvent>()[0]);
        }

        [Test]
        public void Publish_HandlerThrows_PendingChangesStillFlush()
        {
            var bus = new EventModule();
            int secondCount = 0;
            int thirdCount = 0;

            void Second(DamageEvent evt)
            {
                secondCount++;
            }

            void First(DamageEvent evt)
            {
                bus.Unsubscribe<DamageEvent>(Second);
                bus.Subscribe<DamageEvent>(_ => thirdCount++);
                throw new InvalidOperationException("boom");
            }

            bus.Subscribe<DamageEvent>(First);
            bus.Subscribe<DamageEvent>(Second);

            bus.Publish(new DamageEvent());
            bus.Publish(new DamageEvent());

            Assert.AreEqual(1, secondCount, "Pending unsubscribe must flush even when a handler throws.");
            Assert.AreEqual(1, thirdCount, "Pending subscribe must flush even when a handler throws.");
        }

        [Test]
        public void Publish_SteadyState_DoesNotAllocateSnapshot()
        {
            var bus = new EventModule();
            int count = 0;
            bus.Subscribe<DamageEvent>(_ => count++);
            bus.Subscribe<DamageEvent>(_ => count++);
            bus.Subscribe<DamageEvent>(_ => count++);

            bus.Publish(new DamageEvent());
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++)
                bus.Publish(new DamageEvent());
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "Steady-state Publish should not allocate a ToArray snapshot.");
            Assert.AreEqual(27, count);
        }

        [Test]
        public void Diagnostics_ReportSubscriberCountsAndEventTypes()
        {
            var bus = new EventModule();

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
            Action<IEventModule> registration = bus => bus.Subscribe<DamageEvent>(_ => { });

            // C5：只调 Register（未调 RegisterWithMetadata）模拟旧生成器
            EventHandlerRegistry.Register(registration);

            Assert.AreEqual(1, EventHandlerRegistry.Count);
            var snapshot = EventHandlerRegistry.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            // 旧生成代码未调 RegisterWithMetadata，Snapshot 显示 unknown
            Assert.AreEqual("unknown", snapshot[0].HandlerSignature);
            Assert.IsNull(snapshot[0].EventType);
            Assert.AreEqual("unknown", snapshot[0].SourceAssembly);

            EventHandlerRegistry.ClearForTests();
        }

        [Test]
        public void EventHandlerRegistry_Register_AllowsLegacyDuplicates()
        {
            EventHandlerRegistry.ClearForTests();
            Action<IEventModule> registration = bus => bus.Subscribe<DamageEvent>(_ => { });

            EventHandlerRegistry.Register(registration);
            EventHandlerRegistry.Register(registration);

            Assert.AreEqual(2, EventHandlerRegistry.Count);

            EventHandlerRegistry.ClearForTests();
        }

        [Test]
        public void EventHandlerRegistry_ApplyAll_FailFastByDefault()
        {
            EventHandlerRegistry.ClearForTests();
            var bus = new EventModule();
            int applied = 0;

            EventHandlerRegistry.Register(_ => throw new InvalidOperationException("boom"));
            EventHandlerRegistry.Register(_ => applied++);

            Assert.Throws<InvalidOperationException>(() => EventHandlerRegistry.ApplyAll(bus));
            Assert.AreEqual(0, applied);

            EventHandlerRegistry.ClearForTests();
        }
    }
}
