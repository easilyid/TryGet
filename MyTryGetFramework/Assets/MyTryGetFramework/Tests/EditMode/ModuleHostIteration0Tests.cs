using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleHost 迭代 0 测试：Register / Get / TryGet 基础契约。
    /// </summary>
    [TestFixture]
    public class ModuleHostIteration0Tests
    {
        private interface IFooModule : IModule { }
        private interface IBarModule : IModule { }

        private class FooModule : IFooModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
        }

        private class BarModule : IBarModule
        {
            public int Priority => 10;
            public IReadOnlyList<Type> DependsOn => new[] { typeof(IFooModule) };
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
        }

        [Test]
        public void Register_AddsModule()
        {
            var host = new ModuleHost();
            var foo = new FooModule();

            host.Register<IFooModule>(foo);

            Assert.AreEqual(foo, host.Get<IFooModule>());
        }

        [Test]
        public void Register_TwoModulesDifferentInterfaces_BothRegistered()
        {
            var host = new ModuleHost();
            var foo = new FooModule();
            var bar = new BarModule();

            host.Register<IFooModule>(foo);
            host.Register<IBarModule>(bar);

            Assert.AreEqual(foo, host.Get<IFooModule>());
            Assert.AreEqual(bar, host.Get<IBarModule>());
        }

        [Test]
        public void Register_SameInterfaceTwice_Throws()
        {
            var host = new ModuleHost();
            host.Register<IFooModule>(new FooModule());

            Assert.Throws<InvalidOperationException>(() =>
                host.Register<IFooModule>(new FooModule()));
        }

        [Test]
        public void Register_NullModule_Throws()
        {
            var host = new ModuleHost();

            Assert.Throws<ArgumentNullException>(() =>
                host.Register<IFooModule>(null));
        }

        [Test]
        public void Register_ConcreteTypeNotInterface_Throws()
        {
            // 强制使用接口类型注册（ADR-0011 纪律），用具体类型注册应被拒绝。
            var host = new ModuleHost();
            var foo = new FooModule();

            Assert.Throws<ArgumentException>(() =>
                host.Register<FooModule>(foo));
        }

        [Test]
        public void Get_UnregisteredInterface_Throws()
        {
            var host = new ModuleHost();

            Assert.Throws<InvalidOperationException>(() =>
                host.Get<IFooModule>());
        }

        [Test]
        public void TryGet_RegisteredInterface_ReturnsTrue()
        {
            var host = new ModuleHost();
            var foo = new FooModule();
            host.Register<IFooModule>(foo);

            bool found = host.TryGet<IFooModule>(out var resolved);

            Assert.IsTrue(found);
            Assert.AreEqual(foo, resolved);
        }

        [Test]
        public void TryGet_UnregisteredInterface_ReturnsFalseAndNull()
        {
            var host = new ModuleHost();

            bool found = host.TryGet<IFooModule>(out var resolved);

            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

        [Test]
        public void Register_FrameworkBaseIModule_Throws()
        {
            // ADR-0011 纪律：不能注册到框架基础接口，必须用 Module 自定义服务接口。
            var host = new ModuleHost();

            Assert.Throws<ArgumentException>(() =>
                host.Register<IModule>(new FooModule()));
        }

        [Test]
        public void Register_FrameworkIUpdateModule_Throws()
        {
            var host = new ModuleHost();

            Assert.Throws<ArgumentException>(() =>
                host.Register<IUpdateModule>(new UpdateableFoo()));
        }

        [Test]
        public void Register_SameInstanceTwoInterfaces_BothAccessible()
        {
            // 同一实例可以通过两个不同的服务接口注册（如同时提供两种能力）。
            var host = new ModuleHost();
            var dual = new DualModule();

            host.Register<IFooModule>(dual);
            host.Register<IBarModule>(dual);

            Assert.AreSame(dual, host.Get<IFooModule>());
            Assert.AreSame(dual, host.Get<IBarModule>());
        }

        [Test]
        public void EventBus_NonNullAndUsable()
        {
            // EventBus 在 ModuleHost 构造时即可用，不需要 Initialize。
            var host = new ModuleHost();
            int received = 0;
            host.EventBus.Subscribe<SampleEvent>(evt => received = evt.Value);

            host.EventBus.Publish(new SampleEvent { Value = 42 });

            Assert.AreEqual(42, received);
        }

        private struct SampleEvent
        {
            public int Value;
        }

        // 同时实现两个服务接口的 Module（用于多接口注册测试）
        private class DualModule : IFooModule, IBarModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
        }

        // 同时实现 IModule 和 IUpdateModule（用于框架接口拒绝测试）
        private class UpdateableFoo : IUpdateModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
            public void Update(float deltaTime, float unscaledDeltaTime) { }
        }
    }
}
