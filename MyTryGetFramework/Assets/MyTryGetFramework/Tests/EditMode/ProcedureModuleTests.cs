using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ProcedureModule（跨帧流程状态机）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class ProcedureModuleTests
    {
        /// <summary>追踪生命周期事件的测试 Procedure。</summary>
        private class TracingProcedure : ProcedureBase
        {
            public List<string> Log;
            public string Name;
            public int UpdateCount;
            public float LastDelta;

            public TracingProcedure(List<string> log, string name)
            {
                Log = log;
                Name = name;
            }

            public override void OnEnter(IProcedureModule m)
            {
                Log?.Add($"enter:{Name}");
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                UpdateCount++;
                LastDelta = dt;
                Log?.Add($"update:{Name}");
            }

            public override void OnExit(IProcedureModule m)
            {
                Log?.Add($"exit:{Name}");
            }
        }

        [Test]
        public void Priority_BetweenEntityWorldAndUserModules()
        {
            var p = new ProcedureModule();
            Assert.AreEqual(-200, p.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var p = new ProcedureModule();
            Assert.AreEqual(0, p.DependsOn.Count);
        }

        [Test]
        public void NotStarted_IsRunningFalse_CurrentProcedureNull()
        {
            var p = new ProcedureModule();
            Assert.IsFalse(p.IsRunning);
            Assert.IsNull(p.CurrentProcedure);
        }

        [Test]
        public void AddProcedure_NullId_Throws()
        {
            var p = new ProcedureModule();
            Assert.Throws<ArgumentException>(() => p.AddProcedure(null, new TracingProcedure(null, "x")));
            Assert.Throws<ArgumentException>(() => p.AddProcedure("", new TracingProcedure(null, "x")));
        }

        [Test]
        public void AddProcedure_NullProcedure_Throws()
        {
            var p = new ProcedureModule();
            Assert.Throws<ArgumentNullException>(() => p.AddProcedure("foo", null));
        }

        [Test]
        public void AddProcedure_Duplicate_Throws()
        {
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(null, "a"));
            Assert.Throws<InvalidOperationException>(() =>
                p.AddProcedure("a", new TracingProcedure(null, "a2")));
        }

        [Test]
        public void Start_TriggersInitialEnter()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("boot", new TracingProcedure(log, "boot"));

            p.Start("boot");

            Assert.AreEqual(new[] { "enter:boot" }, log.ToArray());
            Assert.IsTrue(p.IsRunning);
            Assert.AreEqual("boot", p.CurrentProcedure);
        }

        [Test]
        public void Start_NotRegistered_Throws()
        {
            var p = new ProcedureModule();
            Assert.Throws<InvalidOperationException>(() => p.Start("missing"));
        }

        [Test]
        public void Start_AlreadyStarted_Throws()
        {
            var p = new ProcedureModule();
            p.AddProcedure("boot", new TracingProcedure(null, "boot"));
            p.Start("boot");

            Assert.Throws<InvalidOperationException>(() => p.Start("boot"));
        }

        [Test]
        public void Update_DispatchesToCurrentProcedureOnly()
        {
            var p = new ProcedureModule();
            var a = new TracingProcedure(null, "a");
            var b = new TracingProcedure(null, "b");
            p.AddProcedure("a", a);
            p.AddProcedure("b", b);
            p.Start("a");

            p.Update(0.016f, 0.020f);
            p.Update(0.016f, 0.020f);

            Assert.AreEqual(2, a.UpdateCount);
            Assert.AreEqual(0, b.UpdateCount, "未激活 procedure 不应收到 OnUpdate");
            Assert.AreEqual(0.016f, a.LastDelta);
        }

        [Test]
        public void Update_NotStarted_IsSilent()
        {
            var p = new ProcedureModule();
            Assert.DoesNotThrow(() => p.Update(0.016f, 0.016f));
        }

        [Test]
        public void Replace_TriggersExitThenEnter()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(log, "a"));
            p.AddProcedure("b", new TracingProcedure(log, "b"));
            p.Start("a");
            log.Clear();

            p.Replace("b");

            Assert.AreEqual(new[] { "exit:a", "enter:b" }, log.ToArray());
            Assert.AreEqual("b", p.CurrentProcedure);
        }

        [Test]
        public void Replace_SameState_ExitThenEnterAgain()
        {
            // 同状态切换：视为 Exit → Enter 重启
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(log, "a"));
            p.Start("a");
            log.Clear();

            p.Replace("a");

            Assert.AreEqual(new[] { "exit:a", "enter:a" }, log.ToArray());
            Assert.AreEqual("a", p.CurrentProcedure);
        }

        [Test]
        public void Replace_NotStarted_Throws()
        {
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(null, "a"));
            Assert.Throws<InvalidOperationException>(() => p.Replace("a"));
        }

        [Test]
        public void Replace_NotRegistered_Throws()
        {
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(null, "a"));
            p.Start("a");
            Assert.Throws<InvalidOperationException>(() => p.Replace("missing"));
        }

        [Test]
        public void Stop_TriggersExit()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(log, "a"));
            p.Start("a");
            log.Clear();

            p.Stop();

            Assert.AreEqual(new[] { "exit:a" }, log.ToArray());
            Assert.IsFalse(p.IsRunning);
            Assert.IsNull(p.CurrentProcedure);
        }

        [Test]
        public void Stop_WhenNotStarted_IsSilent()
        {
            var p = new ProcedureModule();
            Assert.DoesNotThrow(() => p.Stop());
        }

        [Test]
        public void Stop_AllowsRestart()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(log, "a"));

            p.Start("a");
            p.Stop();
            log.Clear();
            p.Start("a");

            Assert.AreEqual(new[] { "enter:a" }, log.ToArray());
        }

        [Test]
        public void Shutdown_CallsExitOnCurrentProcedure_SwallowsExceptions()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new TracingProcedure(log, "a"));
            p.Start("a");
            log.Clear();

            p.Shutdown();

            Assert.AreEqual(new[] { "exit:a" }, log.ToArray());
            Assert.IsFalse(p.IsRunning);
        }

        [Test]
        public void Procedure_CanTransitionFromOnUpdate()
        {
            // 实际游戏场景：Procedure 在 OnUpdate 中根据条件触发 Replace
            var log = new List<string>();
            var p = new ProcedureModule();

            var boot = new ConditionalTransition("boot", "login", 2, log);
            var login = new TracingProcedure(log, "login");
            p.AddProcedure("boot", boot);
            p.AddProcedure("login", login);
            p.Start("boot");
            log.Clear();

            p.Update(0.016f, 0.016f);  // boot tick 1
            p.Update(0.016f, 0.016f);  // boot tick 2 → 触发 transition

            // 进入 login 后 boot 不再 update
            p.Update(0.016f, 0.016f);

            // 期望：update:boot, update:boot, exit:boot, enter:login, update:login
            Assert.Contains("exit:boot", log);
            Assert.Contains("enter:login", log);
            Assert.AreEqual("login", p.CurrentProcedure);
        }

        [Test]
        public void IntegratesWithModuleHost()
        {
            // ModuleHost 驱动 ProcedureModule.Update
            var host = new ModuleHost();
            var pm = new ProcedureModule();
            var log = new List<string>();
            pm.AddProcedure("boot", new TracingProcedure(log, "boot"));
            host.Register<IProcedureModule>(pm);
            host.Initialize();

            pm.Start("boot");
            host.Update(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);

            int updateBoots = 0;
            foreach (var entry in log) if (entry == "update:boot") updateBoots++;
            Assert.AreEqual(2, updateBoots);

            host.Shutdown();
            Assert.Contains("exit:boot", log);
        }

        // ==================== V2.0 Procedure Stack Tests ====================

        private class StackProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            private readonly string _name;

            public StackProcedure(List<string> log, string name) { _log = log; _name = name; }

            public override void OnEnter(IProcedureModule m) => _log.Add($"enter:{_name}");
            public override void OnExit(IProcedureModule m) => _log.Add($"exit:{_name}");
            public override void OnPause(IProcedureModule m) => _log.Add($"pause:{_name}");
            public override void OnResume(IProcedureModule m) => _log.Add($"resume:{_name}");
            public override void OnUpdate(IProcedureModule m, float dt, float ud) => _log.Add($"update:{_name}");
        }

        [Test]
        public void Push_PausesCurrentAndEntersTarget()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("game", new StackProcedure(log, "game"));
            p.AddProcedure("pause", new StackProcedure(log, "pause"));
            p.Start("game");
            log.Clear();

            p.Push("pause");

            Assert.AreEqual(new[] { "pause:game", "enter:pause" }, log.ToArray());
            Assert.AreEqual("pause", p.CurrentProcedure);
            Assert.AreEqual(2, p.StackDepth);
        }

        [Test]
        public void Pop_ExitsTopAndResumesUnder()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("game", new StackProcedure(log, "game"));
            p.AddProcedure("pause", new StackProcedure(log, "pause"));
            p.Start("game");
            p.Push("pause");
            log.Clear();

            p.Pop();

            Assert.AreEqual(new[] { "exit:pause", "resume:game" }, log.ToArray());
            Assert.AreEqual("game", p.CurrentProcedure);
            Assert.AreEqual(1, p.StackDepth);
        }

        [Test]
        public void PushPop_Nested_RestoresOuterOnly()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("game", new StackProcedure(log, "game"));
            p.AddProcedure("pause", new StackProcedure(log, "pause"));
            p.AddProcedure("settings", new StackProcedure(log, "settings"));
            p.Start("game");

            p.Push("pause");
            p.Push("settings");
            Assert.AreEqual(3, p.StackDepth);
            Assert.AreEqual("settings", p.CurrentProcedure);

            log.Clear();
            p.Pop();
            Assert.AreEqual(new[] { "exit:settings", "resume:pause" }, log.ToArray());
            Assert.AreEqual("pause", p.CurrentProcedure);

            log.Clear();
            p.Pop();
            Assert.AreEqual(new[] { "exit:pause", "resume:game" }, log.ToArray());
            Assert.AreEqual("game", p.CurrentProcedure);
        }

        [Test]
        public void Replace_ExitsTopAndEntersNew_StackDepthUnchanged()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new StackProcedure(log, "a"));
            p.AddProcedure("b", new StackProcedure(log, "b"));
            p.Start("a");
            log.Clear();

            p.Replace("b");

            Assert.AreEqual(new[] { "exit:a", "enter:b" }, log.ToArray());
            Assert.AreEqual(1, p.StackDepth);
            Assert.AreEqual("b", p.CurrentProcedure);
        }

        [Test]
        public void Update_OnlyTopOfStackReceives()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("game", new StackProcedure(log, "game"));
            p.AddProcedure("pause", new StackProcedure(log, "pause"));
            p.Start("game");
            p.Push("pause");
            log.Clear();

            p.Update(0.016f, 0.016f);

            Assert.AreEqual(new[] { "update:pause" }, log.ToArray());
        }

        [Test]
        public void Pop_EmptyStack_Throws()
        {
            var p = new ProcedureModule();
            Assert.Throws<InvalidOperationException>(() => p.Pop());
        }

        [Test]
        public void Push_NotStarted_Throws()
        {
            var p = new ProcedureModule();
            p.AddProcedure("a", new StackProcedure(new List<string>(), "a"));
            Assert.Throws<InvalidOperationException>(() => p.Push("a"));
        }

        [Test]
        public void Stop_ExitsAllStackInReverseOrder()
        {
            var log = new List<string>();
            var p = new ProcedureModule();
            p.AddProcedure("a", new StackProcedure(log, "a"));
            p.AddProcedure("b", new StackProcedure(log, "b"));
            p.AddProcedure("c", new StackProcedure(log, "c"));
            p.Start("a");
            p.Push("b");
            p.Push("c");
            log.Clear();

            p.Stop();

            Assert.AreEqual(new[] { "exit:c", "exit:b", "exit:a" }, log.ToArray());
            Assert.AreEqual(0, p.StackDepth);
            Assert.IsFalse(p.IsRunning);
        }

        // 帮助类：在第 N 次 Update 时触发 Replace
        private class ConditionalTransition : ProcedureBase
        {
            private readonly string _name;
            private readonly string _target;
            private readonly int _triggerOnTick;
            private readonly List<string> _log;
            private int _ticks;

            public ConditionalTransition(string name, string target, int triggerOnTick, List<string> log)
            {
                _name = name;
                _target = target;
                _triggerOnTick = triggerOnTick;
                _log = log;
            }

            public override void OnEnter(IProcedureModule m) => _log.Add($"enter:{_name}");

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                _log.Add($"update:{_name}");
                _ticks++;
                if (_ticks >= _triggerOnTick)
                    m.Replace(_target);
            }

            public override void OnExit(IProcedureModule m) => _log.Add($"exit:{_name}");
        }
    }
}
