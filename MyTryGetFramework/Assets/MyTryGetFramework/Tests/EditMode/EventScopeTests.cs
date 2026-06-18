using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// IEventScope / EventScope / EventModule scope 扩展测试。
    /// </summary>
    [TestFixture]
    public class EventScopeTests
    {
        private struct TestEvent { public int Value; }
        private struct AnotherEvent { public string Tag; }

        // ===== EventScope 基础 =====

        [Test]
        public void NewScope_NotDisposed()
        {
            var scope = new EventScope();
            Assert.IsFalse(scope.IsDisposed);
            Assert.AreEqual(0, scope.RegisteredCount);
        }

        [Test]
        public void Register_IncreasesRegisteredCount()
        {
            var scope = new EventScope();
            scope.Register(() => { });
            scope.Register(() => { });
            Assert.AreEqual(2, scope.RegisteredCount);
        }

        [Test]
        public void Register_NullThrows()
        {
            var scope = new EventScope();
            Assert.Throws<ArgumentNullException>(() => scope.Register(null));
        }

        [Test]
        public void Dispose_InvokesAllUnsubscribers_InReverseOrder()
        {
            var scope = new EventScope();
            var order = new List<int>();
            scope.Register(() => order.Add(1));
            scope.Register(() => order.Add(2));
            scope.Register(() => order.Add(3));

            scope.Dispose();

            Assert.AreEqual(new[] { 3, 2, 1 }, order.ToArray(),
                "Dispose 应倒序调 unsubscriber（LIFO，与订阅顺序逆序）");
            Assert.IsTrue(scope.IsDisposed);
            Assert.AreEqual(0, scope.RegisteredCount, "Dispose 后 list 清空");
        }

        [Test]
        public void Dispose_Twice_NoOp()
        {
            var scope = new EventScope();
            int callCount = 0;
            scope.Register(() => callCount++);

            scope.Dispose();
            scope.Dispose();
            scope.Dispose();

            Assert.AreEqual(1, callCount, "重复 Dispose 不应再次触发 unsubscriber");
        }

        [Test]
        public void Register_AfterDispose_ImmediatelyInvokesUnsubscriber()
        {
            var scope = new EventScope();
            scope.Dispose();

            bool invoked = false;
            scope.Register(() => invoked = true);

            Assert.IsTrue(invoked, "scope 已 Dispose 后 Register 应立即执行 unsubscriber 以防泄漏");
            Assert.AreEqual(0, scope.RegisteredCount, "已 Dispose 的 scope 不应再持解绑动作");
        }

        [Test]
        public void Dispose_UnsubscriberThrows_ContinuesOthers()
        {
            var scope = new EventScope();
            var log = new List<string>();
            scope.Register(() => log.Add("first"));
            scope.Register(() => throw new InvalidOperationException("oops"));
            scope.Register(() => log.Add("last"));

            // 倒序：last → throw → first；中间的 throw 应被吞，first 仍要跑
            Assert.DoesNotThrow(() => scope.Dispose());

            Assert.AreEqual(new[] { "last", "first" }, log.ToArray(),
                "throw 的 unsubscriber 应被吞，前面已 Register 的 unsubscriber 继续跑");
        }

        // ===== IEventModule 集成 =====

        [Test]
        public void EventModule_Subscribe_WithScope_HandlerReceivesEvent()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int received = 0;
            using (var scope = host.EventModule.CreateScope())
            {
                host.EventModule.Subscribe<TestEvent>(e => received = e.Value, scope);

                host.EventModule.Publish(new TestEvent { Value = 42 });
                Assert.AreEqual(42, received);
            }

            host.Shutdown();
        }

        [Test]
        public void EventModule_ScopeDispose_HandlerNoLongerCalled()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int callCount = 0;
            var scope = host.EventModule.CreateScope();
            host.EventModule.Subscribe<TestEvent>(_ => callCount++, scope);

            host.EventModule.Publish(new TestEvent { Value = 1 });
            Assert.AreEqual(1, callCount);

            scope.Dispose();
            host.EventModule.Publish(new TestEvent { Value = 2 });
            Assert.AreEqual(1, callCount, "scope.Dispose 后 handler 不应再被触发");

            host.Shutdown();
        }

        [Test]
        public void EventModule_MultipleScopes_IsolatedDispose()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int countA = 0, countB = 0;
            var scopeA = host.EventModule.CreateScope();
            var scopeB = host.EventModule.CreateScope();

            host.EventModule.Subscribe<TestEvent>(_ => countA++, scopeA);
            host.EventModule.Subscribe<TestEvent>(_ => countB++, scopeB);

            host.EventModule.Publish(new TestEvent());
            Assert.AreEqual(1, countA);
            Assert.AreEqual(1, countB);

            scopeA.Dispose();
            host.EventModule.Publish(new TestEvent());
            Assert.AreEqual(1, countA, "scopeA Dispose 后 A handler 不再触发");
            Assert.AreEqual(2, countB, "scopeB 不受影响");

            scopeB.Dispose();
            host.Shutdown();
        }

        [Test]
        public void EventModule_MixedEventTypes_OneScope()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int testCount = 0, anotherCount = 0;
            using (var scope = host.EventModule.CreateScope())
            {
                host.EventModule.Subscribe<TestEvent>(_ => testCount++, scope);
                host.EventModule.Subscribe<AnotherEvent>(_ => anotherCount++, scope);

                host.EventModule.Publish(new TestEvent());
                host.EventModule.Publish(new AnotherEvent());
                Assert.AreEqual(1, testCount);
                Assert.AreEqual(1, anotherCount);
            }

            host.EventModule.Publish(new TestEvent());
            host.EventModule.Publish(new AnotherEvent());
            Assert.AreEqual(1, testCount, "Dispose 后两种事件都不再触发");
            Assert.AreEqual(1, anotherCount);

            host.Shutdown();
        }

        [Test]
        public void EventModule_Subscribe_NullArgs_Throw()
        {
            var host = new ModuleSystem();
            host.Initialize();

            var scope = new EventScope();
            Assert.Throws<ArgumentNullException>(
                () => EventModuleScopeExtensions.Subscribe<TestEvent>(null, _ => { }, scope));
            Assert.Throws<ArgumentNullException>(
                () => host.EventModule.Subscribe<TestEvent>(null, scope));
            Assert.Throws<ArgumentNullException>(
                () => host.EventModule.Subscribe<TestEvent>(_ => { }, null));

            host.Shutdown();
        }

        [Test]
        public void SingleScopeDispose_UnsubscribesMultipleBusHandlers()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int testCount = 0, anotherCount = 0;
            var scope = new EventScope();
            host.EventModule.Subscribe<TestEvent>(_ => testCount++, scope);
            host.EventModule.Subscribe<AnotherEvent>(_ => anotherCount++, scope);

            host.EventModule.Publish(new TestEvent());
            host.EventModule.Publish(new AnotherEvent());
            Assert.AreEqual(1, testCount);
            Assert.AreEqual(1, anotherCount);

            scope.Dispose();
            host.EventModule.Publish(new TestEvent());
            host.EventModule.Publish(new AnotherEvent());
            Assert.AreEqual(1, testCount);
            Assert.AreEqual(1, anotherCount);

            host.Shutdown();
        }

        [Test]
        public void SubscribeWithDisposedScope_DoesNotLeaveHandler()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int callCount = 0;
            var scope = host.EventModule.CreateScope();
            scope.Dispose();

            host.EventModule.Subscribe<TestEvent>(_ => callCount++, scope);
            host.EventModule.Publish(new TestEvent());

            Assert.AreEqual(0, callCount, "已 Dispose 的 scope 下订阅不应残留 handler");
            Assert.AreEqual(0, host.EventModule.GetSubscriberCount<TestEvent>());

            host.Shutdown();
        }

        [Test]
        public void SubscribeWithDisposedScope_DuringPublish_DoesNotLeaveHandlerAfterFlush()
        {
            var host = new ModuleSystem();
            host.Initialize();

            int lateCallCount = 0;
            var scope = host.EventModule.CreateScope();
            scope.Dispose();

            void LateHandler(TestEvent evt)
            {
                lateCallCount++;
            }

            host.EventModule.Subscribe<TestEvent>(_ =>
            {
                host.EventModule.Subscribe<TestEvent>(LateHandler, scope);
            });

            host.EventModule.Publish(new TestEvent());
            host.EventModule.Publish(new TestEvent());

            Assert.AreEqual(0, lateCallCount, "已 Dispose 的 scope 在派发中订阅也不应在 flush 后残留 handler");
            Assert.AreEqual(1, host.EventModule.GetSubscriberCount<TestEvent>());

            host.Shutdown();
        }
    }
}
