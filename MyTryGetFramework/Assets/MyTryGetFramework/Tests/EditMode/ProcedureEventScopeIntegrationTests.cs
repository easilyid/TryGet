using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// Procedure ↔ EventScope 集成测试：验证「正确使用 EventScope 托管订阅」时，
    /// Procedure 离栈（Stop/Replace/Pop）自动解绑事件订阅，不泄漏到下一个 Procedure。
    ///
    /// 背景（非 bug，是 owner-managed 契约）：
    /// ProcedureBase.OnExit 默认调 CancelScope()，只取消 TGTask pending（ADR-0021），
    /// 【不托管 Event 订阅】。Event 订阅的托管是 EventScope 的独立职责。IProcedure XML 注释明确：
    /// 「子类若 override OnExit，请调 base.OnExit(module) 或自行调用 CancelScope」——
    /// EventScope 同理需子类在 OnExit 主动 Dispose。
    ///
    /// 注：IEventModule 是 ModuleSystem 内置服务（通过 host.EventModule 暴露，非 Register 注册），
    /// IEventModule 不继承 IModule。
    ///
    /// 本测试固化「正确模式」：Procedure 在 OnEnter 创建 EventScope 并订阅，OnExit Dispose scope。
    /// 验证 Stop/Replace 后 bus.GetSubscriberCount 归零。这是绿灯测试（证明契约正确工作）。
    /// </summary>
    [TestFixture]
    public class ProcedureEventScopeIntegrationTests
    {
        private struct FooEvent { public int V; }
        private struct ProcedureEnteredEvent { public string Id; }

        private interface IProcedureEventProbeModule : IModule { }

        private sealed class ProcedureEventProbeModule : IProcedureEventProbeModule
        {
            public int Priority => 0;
            public System.Collections.Generic.IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public int ReceiveCount;
            public string LastId;
            private IEventModule _bus;

            public void OnInit(IModuleSystem host)
            {
                _bus = host.EventModule;
                _bus.Subscribe<ProcedureEnteredEvent>(OnProcedureEntered);
            }

            public void Shutdown()
            {
                _bus?.Unsubscribe<ProcedureEnteredEvent>(OnProcedureEntered);
                _bus = null;
            }

            private void OnProcedureEntered(ProcedureEnteredEvent evt)
            {
                ReceiveCount++;
                LastId = evt.Id;
            }
        }

        private sealed class PublishingProcedure : ProcedureBase
        {
            private readonly string _id;
            public PublishingProcedure(string id) { _id = id; }
            public override void OnEnter(IProcedureModule m)
            {
                m.Host.EventModule.Publish(new ProcedureEnteredEvent { Id = _id });
            }
        }

        /// <summary>正确使用 EventScope 的 Procedure：OnEnter 订阅，OnExit Dispose scope。</summary>
        private class ScopedProcedure : ProcedureBase
        {
            private EventScope _scope;
            private readonly Action<FooEvent> _handler;
            public int ReceiveCount;

            public ScopedProcedure(Action<FooEvent> handler = null)
            {
                _handler = handler;
            }

            public override void OnEnter(IProcedureModule m)
            {
                ReceiveCount = 0;
                var bus = m.Host.EventModule;
                _scope = bus.CreateScope();
                bus.Subscribe<FooEvent>(OnFoo, _scope);
            }

            private void OnFoo(FooEvent e)
            {
                ReceiveCount++;
                _handler?.Invoke(e);
            }

            public override void OnExit(IProcedureModule m)
            {
                base.OnExit(m); // 取消 TGTask scope
                _scope?.Dispose(); // 解绑 Event 订阅 —— owner 责任
                _scope = null;
            }
        }

        /// <summary>错误对照：不 Dispose scope 的 Procedure（演示泄漏，仅作对照，非主测）。</summary>
        private class LeakyProcedure : ProcedureBase
        {
            private EventScope _scope;
            public int ReceiveCount;

            public override void OnEnter(IProcedureModule m)
            {
                var bus = m.Host.EventModule;
                _scope = bus.CreateScope();
                bus.Subscribe<FooEvent>(e => ReceiveCount++, _scope);
            }
            // 故意不 Dispose scope —— 演示泄漏
        }

        private static (ModuleSystem host, IEventModule bus, ProcedureModule proc) BuildHost()
        {
            var host = new ModuleSystem();
            var bus = host.EventModule; // 内置事件总线，无需 Register
            var proc = new ProcedureModule();
            host.Register<IProcedureModule>(proc);
            host.Initialize();
            return (host, bus, proc);
        }

        [Test]
        public void Procedure_OnEnterPublishesEvent_HandlersInOtherModuleReceive()
        {
            var host = new ModuleSystem();
            var probe = new ProcedureEventProbeModule();
            var proc = new ProcedureModule();
            host.Register<IProcedureEventProbeModule>(probe);
            host.Register<IProcedureModule>(proc);
            host.Initialize();
            proc.AddProcedure("boot", new PublishingProcedure("boot"));

            proc.Start("boot").GetAwaiter().GetResult();

            Assert.AreEqual(1, probe.ReceiveCount);
            Assert.AreEqual("boot", probe.LastId);

            host.Shutdown();
        }

        /// <summary>
        /// 正确模式：Start scoped procedure → Publish 收到 → Stop 离栈 → 订阅解绑、再 Publish 不收到。
        /// </summary>
        [Test]
        public void ScopedProcedure_Stop_Unsubscribes_NoLeak()
        {
            var (host, bus, proc) = BuildHost();
            var p = new ScopedProcedure();
            proc.AddProcedure("a", p);

            proc.Start("a").GetAwaiter().GetResult();
            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>(), "OnEnter 后订阅生效");

            bus.Publish(new FooEvent { V = 1 });
            Assert.AreEqual(1, p.ReceiveCount, "订阅期间收到事件");

            proc.Stop(); // 触发 OnExit → Dispose scope → 解绑

            Assert.AreEqual(0, bus.GetSubscriberCount<FooEvent>(),
                "Stop 后 EventScope.Dispose 解绑，订阅归零，无泄漏");
            Assert.AreEqual(1, p.ReceiveCount, "离栈后再 Publish 不应收到");

            bus.Publish(new FooEvent { V = 2 });
            Assert.AreEqual(1, p.ReceiveCount, "离栈后事件不再送达已退出的 procedure");

            host.Shutdown();
        }

        /// <summary>
        /// Replace 切换：旧 procedure 离栈解绑、新 procedure 订阅，旧 handler 不泄漏到新流程。
        /// </summary>
        [Test]
        public void ScopedProcedure_Replace_OldUnsubscribes_NewSubscribes()
        {
            var (host, bus, proc) = BuildHost();
            var a = new ScopedProcedure();
            var b = new ScopedProcedure();
            proc.AddProcedure("a", a);
            proc.AddProcedure("b", b);

            proc.Start("a").GetAwaiter().GetResult();
            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>(), "a 订阅后 count=1");

            proc.Replace("b").GetAwaiter().GetResult(); // a 离栈(OnExit→Dispose scope)，b 入栈(OnEnter→订阅)

            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>(),
                "Replace 后：a 解绑、b 订阅，count 仍为 1（不是 2，证明 a 的订阅已解绑无泄漏）");

            bus.Publish(new FooEvent { V = 1 });
            Assert.AreEqual(0, a.ReceiveCount, "a 已离栈，不应收到");
            Assert.AreEqual(1, b.ReceiveCount, "b 应收到");

            host.Shutdown();
        }

        /// <summary>
        /// 栈 Push/Pop：下层 procedure 被 Pause（不 OnExit，scope 保留），Pop 恢复后订阅仍在。
        /// 验证 Pause 不解绑（OnPause 不 Dispose scope），Pop 后下层仍能收事件。
        /// </summary>
        [Test]
        public void ScopedProcedure_PushPause_KeepsSubscription_PopResumes()
        {
            var (host, bus, proc) = BuildHost();
            var bottom = new ScopedProcedure();
            var top = new ScopedProcedure();
            proc.AddProcedure("bottom", bottom);
            proc.AddProcedure("top", top);

            proc.Start("bottom").GetAwaiter().GetResult();
            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>(), "bottom 订阅 count=1");

            proc.Push("top").GetAwaiter().GetResult(); // bottom 被 Pause（不 OnExit，scope 保留）
            Assert.AreEqual(2, bus.GetSubscriberCount<FooEvent>(),
                "Push 后 bottom 被暂停但订阅保留 + top 订阅，count=2（Pause 不解绑）");

            proc.Pop().GetAwaiter().GetResult(); // top 离栈(OnExit→Dispose)，bottom 恢复(OnResume)

            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>(),
                "Pop 后 top 解绑，bottom 订阅仍在，count=1");

            bus.Publish(new FooEvent { V = 1 });
            Assert.AreEqual(1, bottom.ReceiveCount, "Pop 恢复后 bottom 仍收事件（订阅未因 Push 丢失）");
            Assert.AreEqual(0, top.ReceiveCount, "top 已离栈不收");

            host.Shutdown();
        }

        /// <summary>
        /// 对照测试（文档化泄漏）：不用 EventScope、直接 bus.Subscribe 且 OnExit 不 Unsubscribe → 泄漏。
        /// 此测试证明「不用 EventScope 托管」会泄漏，从而说明 EventScope 正确模式的必要性。
        /// 注意：这是对照，断言泄漏发生（绿灯证明「不正确使用确实会泄漏」，非框架 bug）。
        /// </summary>
        [Test]
        public void LeakyProcedure_WithoutScopeDispose_DoesLeak_Documented()
        {
            var (host, bus, proc) = BuildHost();
            var p = new LeakyProcedure();
            proc.AddProcedure("a", p);

            proc.Start("a").GetAwaiter().GetResult();
            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>());

            proc.Stop(); // OnExit 不 Dispose scope → 订阅残留

            Assert.AreEqual(1, bus.GetSubscriberCount<FooEvent>(),
                "对照：不用 EventScope 托管(或忘 Dispose)时，Stop 后订阅【会泄漏】。" +
                "这正是 EventScope owner-managed 模式存在的理由 —— 非框架 bug，是使用契约。");

            host.Shutdown();
        }
    }
}
