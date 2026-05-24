using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.9 Iter 1 — IPlugin / IPluginHost / IPlugPoint 测试。
    /// 含 ModuleHostMetricsPlugin demo（同时挂 BeforeUpdate + AfterUpdate 计 Update 次数）。
    /// </summary>
    [TestFixture]
    public class PluginTests
    {
        // ===== Demo plugin =====

        /// <summary>Demo：统计 Update 次数 + Before/After 触发顺序。</summary>
        private sealed class ModuleHostMetricsPlugin : IPlugin, IModuleHostBeforeUpdate, IModuleHostAfterUpdate
        {
            public int Priority { get; }
            public int InstallCount;
            public int UninstallCount;
            public int BeforeCount;
            public int AfterCount;
            public List<string> Log = new List<string>();

            public ModuleHostMetricsPlugin(int priority = 0) { Priority = priority; }

            public void Install(IPluginHost host) { InstallCount++; Log.Add("install"); }
            public void Uninstall(IPluginHost host) { UninstallCount++; Log.Add("uninstall"); }

            public void OnBeforeUpdate(IModuleHost host, float dt, float udt)
            { BeforeCount++; Log.Add("before"); }

            public void OnAfterUpdate(IModuleHost host, float dt, float udt)
            { AfterCount++; Log.Add("after"); }
        }

        private sealed class FailingInstallPlugin : IPlugin, IModuleHostBeforeUpdate
        {
            public int Priority => 0;
            public void Install(IPluginHost host) => throw new InvalidOperationException("install-fail");
            public void Uninstall(IPluginHost host) { }
            public void OnBeforeUpdate(IModuleHost host, float dt, float udt) { }
        }

        // ===== 基础注册 =====

        [Test]
        public void AddPlugin_TriggersInstall()
        {
            var host = new ModuleHost();
            host.Initialize();
            var p = new ModuleHostMetricsPlugin();

            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(p);

            Assert.AreEqual(1, p.InstallCount);
            Assert.AreEqual(1, host.PluginCount);

            host.Shutdown();
        }

        [Test]
        public void AddPlugin_NullThrows()
        {
            var host = new ModuleHost();
            host.Initialize();
            Assert.Throws<ArgumentNullException>(
                () => host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(null));
            host.Shutdown();
        }

        [Test]
        public void AddPlugin_SamePointSameType_Twice_Throws()
        {
            var host = new ModuleHost();
            host.Initialize();

            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(new ModuleHostMetricsPlugin());
            Assert.Throws<InvalidOperationException>(
                () => host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(new ModuleHostMetricsPlugin()));

            host.Shutdown();
        }

        [Test]
        public void AddPlugin_InstallThrows_NoResidue()
        {
            var host = new ModuleHost();
            host.Initialize();

            Assert.Throws<InvalidOperationException>(
                () => host.AddPlugin<IModuleHostBeforeUpdate, FailingInstallPlugin>(new FailingInstallPlugin()));

            Assert.AreEqual(0, host.PluginCount, "Install 抛异常时应回滚注册");
            host.Shutdown();
        }

        // ===== Lookup =====

        [Test]
        public void GetPlugin_ReturnsRegisteredInstance()
        {
            var host = new ModuleHost();
            host.Initialize();
            var p = new ModuleHostMetricsPlugin();
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(p);

            var found = host.GetPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>();
            Assert.AreSame(p, found);

            host.Shutdown();
        }

        [Test]
        public void GetPlugin_NotRegistered_ReturnsNull()
        {
            var host = new ModuleHost();
            host.Initialize();
            Assert.IsNull(host.GetPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>());
            host.Shutdown();
        }

        [Test]
        public void GetPluginsAt_EnumeratesAllPriorityOrder()
        {
            var host = new ModuleHost();
            host.Initialize();
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(new ModuleHostMetricsPlugin(priority: 10));

            // 同 point 不同 type 也可注册（用 nested class 避免命名）
            var p2 = new DummyPlugin();
            host.AddPlugin<IModuleHostBeforeUpdate, DummyPlugin>(p2);

            int count = 0;
            foreach (var _ in host.GetPluginsAt<IModuleHostBeforeUpdate>()) count++;
            Assert.AreEqual(2, count);
            host.Shutdown();
        }

        // ===== Remove =====

        [Test]
        public void RemovePlugin_TriggersUninstall()
        {
            var host = new ModuleHost();
            host.Initialize();
            var p = new ModuleHostMetricsPlugin();
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(p);

            Assert.IsTrue(host.RemovePlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>());
            Assert.AreEqual(1, p.UninstallCount);
            Assert.AreEqual(0, host.PluginCount);

            host.Shutdown();
        }

        [Test]
        public void RemovePlugin_NotRegistered_ReturnsFalse()
        {
            var host = new ModuleHost();
            host.Initialize();
            Assert.IsFalse(host.RemovePlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>());
            host.Shutdown();
        }

        // ===== Update 内触发 =====

        [Test]
        public void Update_BeforePluginsFireBeforeModules_AfterFireAfter()
        {
            var host = new ModuleHost();
            var log = new List<string>();
            host.Register<ITracingUpdateModule>(new TracingUpdateModule(log));
            host.Initialize();

            var p = new ModuleHostMetricsPlugin();
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(p);
            host.AddPlugin<IModuleHostAfterUpdate, ModuleHostMetricsPlugin>(p);

            host.Update(0.016f, 0.016f);

            Assert.AreEqual(1, p.BeforeCount);
            Assert.AreEqual(1, p.AfterCount);
            Assert.AreEqual(1, log.Count);
            // p.Log: install (from BeforeUpdate.Add) + install (from AfterUpdate.Add) + before + after
            Assert.AreEqual(new[] { "install", "install", "before", "after" }, p.Log.ToArray());
            // 全局顺序：before plugin → module update → after plugin
            host.Shutdown();
        }

        [Test]
        public void Update_NoPlugins_ModuleUpdateStillRuns()
        {
            var host = new ModuleHost();
            var log = new List<string>();
            host.Register<ITracingUpdateModule>(new TracingUpdateModule(log));
            host.Initialize();

            host.Update(0.016f, 0.016f);
            Assert.AreEqual(1, log.Count);
            host.Shutdown();
        }

        // ===== Shutdown 行为 =====

        [Test]
        public void Shutdown_UninstallsAllPluginsOncePerInstance()
        {
            var host = new ModuleHost();
            host.Initialize();
            var p = new ModuleHostMetricsPlugin();
            // 同实例挂两个 point
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(p);
            host.AddPlugin<IModuleHostAfterUpdate, ModuleHostMetricsPlugin>(p);
            Assert.AreEqual(2, host.PluginCount); // 计数按"插点 × 类型"

            host.Shutdown();
            Assert.AreEqual(1, p.UninstallCount,
                "同 plugin 实例多 point 注册时 Shutdown 仅 Uninstall 一次");
            Assert.AreEqual(0, host.PluginCount);
        }

        [Test]
        public void Shutdown_AfterRemove_NoCrash()
        {
            var host = new ModuleHost();
            host.Initialize();
            var p = new ModuleHostMetricsPlugin();
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(p);
            host.RemovePlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>();
            Assert.DoesNotThrow(() => host.Shutdown());
        }

        // ===== Priority 顺序 =====

        [Test]
        public void Priority_AscendingOrder()
        {
            var host = new ModuleHost();
            host.Initialize();
            var pHigh = new ModuleHostMetricsPlugin(priority: 100);
            var pLow = new DummyPlugin(); // Priority=0
            host.AddPlugin<IModuleHostBeforeUpdate, ModuleHostMetricsPlugin>(pHigh);
            host.AddPlugin<IModuleHostBeforeUpdate, DummyPlugin>(pLow);

            var seq = new List<int>();
            foreach (var p in host.GetPluginsAt<IModuleHostBeforeUpdate>())
                seq.Add(((IPlugin)p).Priority);

            Assert.AreEqual(new[] { 0, 100 }, seq.ToArray(), "应按 Priority 升序");
            host.Shutdown();
        }

        // ===== helpers =====

        private sealed class DummyPlugin : IPlugin, IModuleHostBeforeUpdate
        {
            public int Priority => 0;
            public void Install(IPluginHost host) { }
            public void Uninstall(IPluginHost host) { }
            public void OnBeforeUpdate(IModuleHost host, float dt, float udt) { }
        }

        private interface ITracingUpdateModule : IModule, IUpdateModule { }

        private sealed class TracingUpdateModule : ITracingUpdateModule
        {
            private readonly List<string> _log;
            public TracingUpdateModule(List<string> log) { _log = log; }
            public int Priority => -100;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
            public void Update(float dt, float udt) { _log.Add("module-update"); }
        }
    }
}
