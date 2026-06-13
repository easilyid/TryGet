using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// C2 收尾项测试：HandlerException 上报钩子 + Publish 递归深度护栏。
    /// </summary>
    [TestFixture]
    public class EventModuleHardeningTests
    {
        private struct PingEvent
        {
            public int Value;
        }

        private struct PongEvent
        {
            public int Value;
        }

        // ----- HandlerException 上报 -----

        [Test]
        public void HandlerException_RaisedWithEventTypeAndException()
        {
            var bus = new EventModule();
            Type reportedType = null;
            Exception reportedException = null;

            bus.HandlerException += (type, ex) => { reportedType = type; reportedException = ex; };
            bus.Subscribe<PingEvent>(_ => throw new InvalidOperationException("boom"));

            bus.Publish(new PingEvent());

            Assert.AreEqual(typeof(PingEvent), reportedType);
            Assert.IsInstanceOf<InvalidOperationException>(reportedException);
            Assert.AreEqual("boom", reportedException.Message);
        }

        [Test]
        public void HandlerException_RaisedOncePerFailingHandler_OthersStillRun()
        {
            var bus = new EventModule();
            int reportCount = 0;
            int healthyCalls = 0;

            bus.HandlerException += (_, _) => reportCount++;
            bus.Subscribe<PingEvent>(_ => throw new Exception("first"));
            bus.Subscribe<PingEvent>(_ => healthyCalls++);
            bus.Subscribe<PingEvent>(_ => throw new Exception("second"));

            bus.Publish(new PingEvent());

            Assert.AreEqual(2, reportCount);
            Assert.AreEqual(1, healthyCalls);
        }

        [Test]
        public void HandlerException_HookThrows_DoesNotInterruptDispatch()
        {
            var bus = new EventModule();
            int healthyCalls = 0;

            bus.HandlerException += (_, _) => throw new Exception("hook itself is broken");
            bus.Subscribe<PingEvent>(_ => throw new Exception("handler error"));
            bus.Subscribe<PingEvent>(_ => healthyCalls++);

            Assert.DoesNotThrow(() => bus.Publish(new PingEvent()));
            Assert.AreEqual(1, healthyCalls);
        }

        [Test]
        public void HandlerException_NoSubscriber_ExceptionStillCollected()
        {
            var bus = new EventModule();
            bus.Subscribe<PingEvent>(_ => throw new Exception("unreported"));

            Assert.DoesNotThrow(() => bus.Publish(new PingEvent()));
            Assert.AreEqual(1, bus.GetLastPublishExceptions<PingEvent>().Count);
        }

        // ----- 递归深度护栏 -----

        [Test]
        public void Publish_SelfRecursive_StoppedByDepthGuard_NoStackOverflow()
        {
            var bus = new EventModule();
            int invocations = 0;

            bus.Subscribe<PingEvent>(evt =>
            {
                invocations++;
                bus.Publish(new PingEvent { Value = evt.Value + 1 });
            });

            // 顶层 Publish 不抛：深度超限的异常发生在嵌套 handler 内，被异常隔离捕获
            Assert.DoesNotThrow(() => bus.Publish(new PingEvent()));

            Assert.AreEqual(EventModule.MaxPublishDepth, invocations);
        }

        [Test]
        public void Publish_MutualRecursion_StoppedByDepthGuard()
        {
            var bus = new EventModule();
            Exception reported = null;

            bus.HandlerException += (_, ex) => reported ??= ex;
            bus.Subscribe<PingEvent>(_ => bus.Publish(new PongEvent()));
            bus.Subscribe<PongEvent>(_ => bus.Publish(new PingEvent()));

            Assert.DoesNotThrow(() => bus.Publish(new PingEvent()));
            Assert.IsInstanceOf<InvalidOperationException>(reported);
        }

        [Test]
        public void Publish_AfterDepthGuardTriggered_WorksNormally()
        {
            var bus = new EventModule();
            bus.Subscribe<PingEvent>(_ => bus.Publish(new PingEvent()));
            bus.Publish(new PingEvent()); // 触发护栏

            int received = 0;
            bus.Subscribe<PongEvent>(evt => received = evt.Value);
            bus.Publish(new PongEvent { Value = 7 });

            Assert.AreEqual(7, received);
        }

        [Test]
        public void Publish_NestedWithinLimit_Allowed()
        {
            var bus = new EventModule();
            int pongReceived = 0;

            bus.Subscribe<PingEvent>(_ => bus.Publish(new PongEvent { Value = 1 }));
            bus.Subscribe<PongEvent>(_ => pongReceived++);

            bus.Publish(new PingEvent());

            Assert.AreEqual(1, pongReceived);
            Assert.AreEqual(0, bus.GetLastPublishExceptions<PingEvent>().Count);
        }
    }
}
