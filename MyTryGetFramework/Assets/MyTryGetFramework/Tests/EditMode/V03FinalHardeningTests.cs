using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.3 综合验收 Plan agent 提出的高优先级隐患的回归测试。
    /// </summary>
    [TestFixture]
    public class V03FinalHardeningTests
    {
        // —— 高优先级 1：AspectType 重写一致性 assert ——

        private class CleanAspect : Aspect { }

        private class BadlyOverriddenAspect : Aspect
        {
            public override Type AspectType => typeof(CleanAspect); // 故意返回基类/无关类型
        }

        [Test]
        public void Attach_AspectTypeNotEqualGetType_Throws()
        {
            var world = new EntityWorld("Test");
            var e = world.CreateEntity();

            var ex = Assert.Throws<InvalidOperationException>(() => e.Attach(new BadlyOverriddenAspect()));
            StringAssert.Contains("AspectType", ex.Message, "异常消息应明示 AspectType 不一致");

            // 状态完整性：失败后无残留
            Assert.AreEqual(0, e.AspectCount);
            Assert.IsFalse(e.HasAspect<BadlyOverriddenAspect>());
            Assert.IsFalse(e.HasAspect<CleanAspect>());

            world.Shutdown();
        }

        // —— 高优先级 2：Entity.MarkDestroyed OnDetach 抛异常不破坏清理 ——

        private class ThrowingDetachInDestroyAspect : Aspect
        {
            public bool DetachCalled;
            protected internal override void OnDetach()
            {
                DetachCalled = true;
                throw new InvalidOperationException("OnDetach during destroy");
            }
        }

        [Test]
        public void Destroy_OnDetachThrows_EntityStillFullyCleaned()
        {
            var world = new EntityWorld("Test");
            var e = world.CreateEntity();
            var a = new ThrowingDetachInDestroyAspect();
            e.Attach(a);

            Assert.IsTrue(e.HasAspect<ThrowingDetachInDestroyAspect>(), "Attach 成功");

            // Destroy 应不抛（destroy 路径吞 OnDetach 异常）
            Assert.DoesNotThrow(() => world.DestroyEntity(e));

            Assert.IsTrue(a.DetachCalled, "OnDetach 仍被调用");
            Assert.IsTrue(e.IsDestroyed);
            Assert.AreEqual(0, e.AspectCount, "Destroy 后 mask/dict 应被完全清空");
            Assert.IsFalse(e.HasAspect<ThrowingDetachInDestroyAspect>());

            world.Shutdown();
        }

        // —— 中优先级：ProcedureModule.TransitionTo prev.OnExit 抛 → throws ——

        private class ThrowOnExitProcedure : ProcedureBase
        {
            public override void OnExit(IProcedureModule m) => throw new InvalidOperationException("exit boom");
        }

        [Test]
        public void TransitionTo_PrevOnExitThrows_CurrentRemainsPrev()
        {
            var p = new ProcedureModule();
            p.AddProcedure("bad", new ThrowOnExitProcedure());
            p.AddProcedure("good", new TracingProc());
            p.Start("bad");

            Assert.Throws<InvalidOperationException>(() => p.TransitionTo("good"));

            // 切换失败：仍是 bad（OnEnter 没有被 good 调用）
            Assert.AreEqual("bad", p.CurrentState, "OnExit 失败时 current 不应已切换到 next");
            Assert.IsTrue(p.IsRunning);
        }

        // —— 中优先级：OnEnter 内递归 TransitionTo 被阻止 ——

        private class RecursiveTransitionProcedure : ProcedureBase
        {
            public string Target;
            public override void OnEnter(IProcedureModule m)
            {
                if (Target != null) m.TransitionTo(Target);
            }
        }

        [Test]
        public void TransitionTo_RecursiveFromOnEnter_Throws()
        {
            var p = new ProcedureModule();
            var b = new RecursiveTransitionProcedure { Target = "b" };
            var aSrc = new TracingProc();
            p.AddProcedure("a", aSrc);
            p.AddProcedure("b", b);
            p.AddProcedure("c", new TracingProc());

            // 在 b.OnEnter 内调 TransitionTo("c")：a→b→c 嵌套，应抛
            b.Target = "c";
            p.Start("a");

            Assert.Throws<InvalidOperationException>(() => p.TransitionTo("b"),
                "OnEnter 内递归 TransitionTo 应被阻止");
        }

        // —— 中优先级：Stop OnExit 异常被吞咽 ——

        [Test]
        public void Stop_OnExitThrows_StopStillSucceeds()
        {
            var p = new ProcedureModule();
            p.AddProcedure("bad", new ThrowOnExitProcedure());
            p.Start("bad");

            Assert.DoesNotThrow(() => p.Stop());
            Assert.IsFalse(p.IsRunning, "Stop 后仍应为非运行状态（异常被吞）");
            Assert.IsNull(p.CurrentState);
        }

        // —— 辅助 ——

        private class TracingProc : ProcedureBase { }
    }
}
