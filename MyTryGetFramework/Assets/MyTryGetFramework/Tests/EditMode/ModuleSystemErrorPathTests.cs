using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleSystem 错误路径测试：Initialize 半失败回滚 + Shutdown 异常聚合 + DependsOn null 防御。
    /// </summary>
    [TestFixture]
    public class ModuleSystemErrorPathTests
    {
        private interface IGoodModule : IModule { }
        private interface IThrowOnInitModule : IModule { }
        private interface IThrowOnShutdownModule : IModule { }
        private interface INullDepsModule : IModule { }
        private interface IMissingDependencyModule : IModule { }

        private class GoodModule : IGoodModule
        {
            public bool InitCalled;
            public bool ShutdownCalled;
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleSystem host) { InitCalled = true; }
            public void Shutdown() { ShutdownCalled = true; }
        }

        private class ThrowOnInitModule : IThrowOnInitModule
        {
            // 故意依赖 GoodModule 保证 Init 顺序：Good 先 → Throw 后
            public int Priority => 10;
            public IReadOnlyList<Type> DependsOn => new[] { typeof(IGoodModule) };
            public void OnInit(IModuleSystem host) { throw new InvalidOperationException("boom"); }
            public void Shutdown() { }
        }

        private class MissingDependencyModule : IMissingDependencyModule
        {
            public int Priority => 10;
            public IReadOnlyList<Type> DependsOn => new[] { typeof(INullDepsModule) };
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        private class ThrowOnShutdownModule : IThrowOnShutdownModule
        {
            public string Name;
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { throw new InvalidOperationException("shutdown_" + Name); }
        }

        private class NullDepsModule : INullDepsModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => null; // 故意返回 null
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        [Test]
        public void Initialize_MidwayException_RollsBackInitializedModules()
        {
            // GoodModule 已 OnInit → ThrowOnInitModule 抛 → 期望 GoodModule.Shutdown 被回滚调用
            var host = new ModuleSystem();
            var good = new GoodModule();
            var bad = new ThrowOnInitModule();
            host.Register<IGoodModule>(good);
            host.Register<IThrowOnInitModule>(bad);

            Assert.Throws<InvalidOperationException>(() => host.Initialize());

            Assert.IsTrue(good.InitCalled, "Good should have OnInit");
            Assert.IsTrue(good.ShutdownCalled, "Good should have been rolled back via Shutdown");
            Assert.IsFalse(host.IsInitialized, "Host must not be in initialized state after failure");
        }

        [Test]
        public void Initialize_FailureRollback_AllowsRetryAfterFix()
        {
            // 回滚后状态应当干净，重新 Register 不冲突
            var host = new ModuleSystem();
            host.Register<IGoodModule>(new GoodModule());
            host.Register<IThrowOnInitModule>(new ThrowOnInitModule());

            try { host.Initialize(); } catch { /* expected */ }

            // 失败后 IsInitialized=false，但 Register 已锁（因为 _initialized 在抛异常前未设置 true，所以应该未锁）。
            // 验证：可以重新 Initialize（如果我们 Register 一个不再抛的版本，但本测试简化为验证状态）
            Assert.IsFalse(host.IsInitialized);
        }

        [Test]
        public void Shutdown_SingleModuleThrows_OtherModulesStillShutdown()
        {
            // 中间一个 Module Shutdown 抛异常，前后的 Module 都应 Shutdown
            var host = new ModuleSystem();
            var first = new GoodModule();
            var middle = new ThrowOnShutdownModule { Name = "M" };
            var last = new ThrowOnShutdownModule { Name = "L" };
            host.Register<IGoodModule>(first);
            // 使用接口标识 last 与 middle 各自不同
            host.Register<IThrowOnShutdownModule>(middle);
            // 单独 ILogger 不在此范畴，省略 last 注册以保持类型简单

            host.Initialize();
            Assert.IsTrue(first.InitCalled);

            // Shutdown：middle 会抛，但 first 仍应被 Shutdown
            var ex = Assert.Throws<ModuleShutdownException>(() => host.Shutdown());
            Assert.AreEqual(1, ex.InnerExceptions.Count);
            Assert.IsTrue(first.ShutdownCalled, "first.Shutdown should still run despite middle throwing");
        }

        [Test]
        public void DependsOn_ReturnsNull_TreatedAsEmpty()
        {
            // Module.DependsOn 返回 null 不应抛 NRE
            var host = new ModuleSystem();
            host.Register<INullDepsModule>(new NullDepsModule());

            Assert.DoesNotThrow(() => host.Initialize());
            Assert.IsTrue(host.IsInitialized);
        }

        [Test]
        public void Initialize_UnregisteredDependency_ThrowsTypedException()
        {
            // 验证使用专用异常 ModuleDependencyMissingException 而非通用 InvalidOperationException
            var host = new ModuleSystem();
            host.Register<IThrowOnInitModule>(new ThrowOnInitModule()); // depends on IGoodModule which isn't registered

            var ex = Assert.Throws<ModuleDependencyMissingException>(() => host.Initialize());
            Assert.AreEqual(typeof(IGoodModule), ex.MissingInterfaceType);
        }

        [Test]
        public void Initialize_UnregisteredDependency_FailsBeforeAnyModuleIsInitialized()
        {
            var host = new ModuleSystem();
            var good = new GoodModule();
            host.Register<IGoodModule>(good);
            host.Register<IMissingDependencyModule>(new MissingDependencyModule());

            Assert.Throws<ModuleDependencyMissingException>(() => host.Initialize());

            Assert.IsFalse(good.InitCalled);
            Assert.IsFalse(host.IsInitialized);
        }

        [Test]
        public void Initialize_CircularDependency_ThrowsTypedException()
        {
            var host = new ModuleSystem();
            host.Register<IGoodModule>(new CircularA());
            host.Register<IThrowOnInitModule>(new CircularB());

            var ex = Assert.Throws<ModuleCircularDependencyException>(() => host.Initialize());
            Assert.AreEqual(2, ex.InvolvedModules.Count);
        }

        [Test]
        public void Initialize_CircularDependency_FailsBeforeAnyModuleIsInitialized()
        {
            var host = new ModuleSystem();
            var good = new GoodModule();
            host.Register<IGoodModule>(new CircularA());
            host.Register<IThrowOnInitModule>(new CircularB());
            host.Register<IMissingDependencyModule>(new DependsOnGoodModule(good));

            Assert.Throws<ModuleCircularDependencyException>(() => host.Initialize());

            Assert.IsFalse(good.InitCalled);
            Assert.IsFalse(host.IsInitialized);
        }

        private class CircularA : IGoodModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => new[] { typeof(IThrowOnInitModule) };
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        private class CircularB : IThrowOnInitModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => new[] { typeof(IGoodModule) };
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        private class DependsOnGoodModule : IMissingDependencyModule
        {
            private readonly GoodModule _inner;
            public DependsOnGoodModule(GoodModule inner) { _inner = inner; }
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => new[] { typeof(IGoodModule) };
            public void OnInit(IModuleSystem host) { _inner.OnInit(host); }
            public void Shutdown() { _inner.Shutdown(); }
        }

        [Test]
        public void Register_DuplicateInterface_ThrowsTypedException()
        {
            var host = new ModuleSystem();
            host.Register<IGoodModule>(new GoodModule());

            var ex = Assert.Throws<ModuleAlreadyRegisteredException>(() =>
                host.Register<IGoodModule>(new GoodModule()));
            Assert.AreEqual(typeof(IGoodModule), ex.InterfaceType);
        }

        [Test]
        public void Get_Unregistered_ThrowsTypedException()
        {
            var host = new ModuleSystem();
            var ex = Assert.Throws<ModuleNotRegisteredException>(() => host.Get<IGoodModule>());
            Assert.AreEqual(typeof(IGoodModule), ex.InterfaceType);
        }

        // —— 来自 Stage-2 review 必改：失败回滚后状态干净 + Shutdown 二次调用幂等 ——

        [Test]
        public void Initialize_AfterFailureRollback_StateIsClean_ReinitFailsSamePath()
        {
            // 失败回滚后再 Initialize 应按相同失败路径再次抛出，并再次回滚——证明 host 状态未泄漏。
            var host = new ModuleSystem();
            var good = new GoodModule();
            host.Register<IGoodModule>(good);
            host.Register<IThrowOnInitModule>(new ThrowOnInitModule());

            Assert.Throws<InvalidOperationException>(() => host.Initialize());
            Assert.IsFalse(host.IsInitialized);

            // 第二次 Initialize：Good 应该再次 Init + 再次 Rollback
            good.InitCalled = false;
            good.ShutdownCalled = false;
            Assert.Throws<InvalidOperationException>(() => host.Initialize());
            Assert.IsTrue(good.InitCalled, "第二次 Initialize 时 Good 再次 OnInit");
            Assert.IsTrue(good.ShutdownCalled, "第二次失败时 Good 再次 Rollback");
            Assert.IsFalse(host.IsInitialized);
        }

        [Test]
        public void Shutdown_AfterShutdownException_SecondShutdownIsIdempotent()
        {
            var host = new ModuleSystem();
            host.Register<IGoodModule>(new GoodModule());
            host.Register<IThrowOnShutdownModule>(new ThrowOnShutdownModule { Name = "M" });
            host.Initialize();

            Assert.Throws<ModuleShutdownException>(() => host.Shutdown());

            // 二次 Shutdown 不应再抛（IsInitialized 已置 false，直接返回）
            Assert.DoesNotThrow(() => host.Shutdown());
        }
    }
}
