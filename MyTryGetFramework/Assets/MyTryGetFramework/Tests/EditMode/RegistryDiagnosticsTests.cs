using NUnit.Framework;
using System;
using TryGet;

namespace TryGet.Tests
{
    /// <summary>
    /// C5 — Registry 诊断快照 + 重复注册策略测试。
    ///
    /// 测试范围：ModuleRegistry / EventHandlerRegistry 的 Count / Snapshot / ApplyAll 幂等性。
    /// C5 采用 allow-multiple 策略（与现有 EventTests.cs 的 AllowsLegacyDuplicates 一致），
    /// 重复检测留给 ModuleSystem.Register / EventModule.Subscribe 层。
    /// </summary>
    [TestFixture]
    public class RegistryDiagnosticsTests
    {
        [TearDown]
        public void TearDown()
        {
            ModuleRegistry.ClearForTests();
            EventHandlerRegistry.ClearForTests();
        }

        // ========== ModuleRegistry ==========

        [Test]
        public void ModuleRegistry_Snapshot_EmptyWhenNoRegistrations()
        {
            var snapshot = ModuleRegistry.Snapshot();
            Assert.AreEqual(0, snapshot.Count);
        }

        [Test]
        public void ModuleRegistry_Snapshot_ReturnsStructuredInfo()
        {
            ModuleRegistry.ClearForTests(); // 防止生成器自动注册污染
            // 模拟生成器调双轨 API
            ModuleRegistry.RegisterWithMetadata(typeof(DummyModule), typeof(IDummyModule), "TestAsm");
            ModuleRegistry.Register(host => { });

            var snapshot = ModuleRegistry.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual(typeof(DummyModule), snapshot[0].ImplementationType);
            Assert.AreEqual(typeof(IDummyModule), snapshot[0].ServiceType);
            Assert.AreEqual("TestAsm", snapshot[0].SourceAssembly);
        }

        [Test]
        public void ModuleRegistry_Snapshot_ShowsUnknownForLegacyRegistrations()
        {
            ModuleRegistry.ClearForTests();
            // 旧生成器（只调 Register、未调 RegisterWithMetadata）
            ModuleRegistry.Register(host => { });

            var snapshot = ModuleRegistry.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.IsNull(snapshot[0].ImplementationType);
            Assert.IsNull(snapshot[0].ServiceType);
            Assert.AreEqual("unknown", snapshot[0].SourceAssembly);
        }

        [Test]
        public void ModuleRegistry_AllowsMultipleSameService()
        {
            ModuleRegistry.ClearForTests();
            // C5 策略：allow-multiple（与 EventTests.AllowsLegacyDuplicates 一致）
            ModuleRegistry.RegisterWithMetadata(typeof(DummyModule), typeof(IDummyModule), "Asm1");
            ModuleRegistry.Register(host => { });
            ModuleRegistry.RegisterWithMetadata(typeof(DummyModule), typeof(IDummyModule), "Asm2");
            ModuleRegistry.Register(host => { });

            Assert.AreEqual(2, ModuleRegistry.Count);
            var snapshot = ModuleRegistry.Snapshot();
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual("Asm1", snapshot[0].SourceAssembly);
            Assert.AreEqual("Asm2", snapshot[1].SourceAssembly);
        }

        [Test]
        public void ModuleRegistry_ApplyAll_IsIdempotent()
        {
            ModuleRegistry.ClearForTests();
            ModuleRegistry.RegisterWithMetadata(typeof(DummyModule), typeof(IDummyModule), "TestAsm");
            ModuleRegistry.Register(host => { });

            var snapshot1 = ModuleRegistry.Snapshot();

            var host1 = new ModuleSystem();
            ModuleRegistry.ApplyAll(host1);

            var snapshot2 = ModuleRegistry.Snapshot();
            Assert.AreEqual(snapshot1.Count, snapshot2.Count);
            Assert.AreEqual(snapshot1[0].ImplementationType, snapshot2[0].ImplementationType);
        }

        // ========== EventHandlerRegistry ==========

        [Test]
        public void EventHandlerRegistry_Snapshot_ReturnsStructuredInfo()
        {
            EventHandlerRegistry.ClearForTests();
            EventHandlerRegistry.RegisterWithMetadata("TestHandler.OnEvent", typeof(DummyEvent), "TestAsm");
            EventHandlerRegistry.Register(bus => { });

            var snapshot = EventHandlerRegistry.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("TestHandler.OnEvent", snapshot[0].HandlerSignature);
            Assert.AreEqual(typeof(DummyEvent), snapshot[0].EventType);
            Assert.AreEqual("TestAsm", snapshot[0].SourceAssembly);
        }

        [Test]
        public void EventHandlerRegistry_Snapshot_ShowsUnknownForLegacyRegistrations()
        {
            EventHandlerRegistry.ClearForTests();
            EventHandlerRegistry.Register(bus => { });

            var snapshot = EventHandlerRegistry.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("unknown", snapshot[0].HandlerSignature);
            Assert.IsNull(snapshot[0].EventType);
            Assert.AreEqual("unknown", snapshot[0].SourceAssembly);
        }

        [Test]
        public void EventHandlerRegistry_AllowsMultipleSameHandler()
        {
            // 与 EventTests.AllowsLegacyDuplicates 一致
            // 强制清理（避免生成器自动注册或其他测试残留）
            EventHandlerRegistry.ClearForTests();

            EventHandlerRegistry.RegisterWithMetadata("TestHandler.OnEvent", typeof(DummyEvent), "Asm1");
            EventHandlerRegistry.Register(bus => { });
            EventHandlerRegistry.RegisterWithMetadata("TestHandler.OnEvent", typeof(DummyEvent), "Asm2");
            EventHandlerRegistry.Register(bus => { });

            Assert.AreEqual(2, EventHandlerRegistry.Count);
        }

        [Test]
        public void EventHandlerRegistry_ApplyAll_IsIdempotent()
        {
            EventHandlerRegistry.ClearForTests();
            EventHandlerRegistry.RegisterWithMetadata("TestHandler.OnEvent", typeof(DummyEvent), "TestAsm");
            EventHandlerRegistry.Register(bus => { });

            var snapshot1 = EventHandlerRegistry.Snapshot();

            var bus1 = new EventModule();
            EventHandlerRegistry.ApplyAll(bus1);

            var snapshot2 = EventHandlerRegistry.Snapshot();
            Assert.AreEqual(snapshot1.Count, snapshot2.Count);
            Assert.AreEqual(snapshot1[0].HandlerSignature, snapshot2[0].HandlerSignature);
        }

        // ========== Test Fixtures ==========

        private interface IDummyModule : IModule { }
        private class DummyModule : IDummyModule
        {
            public int Priority => 0;
            public System.Collections.Generic.IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        private struct DummyEvent { }
    }
}
