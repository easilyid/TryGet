using System;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    [TestFixture]
    public class GameLauncherTests
    {
        private struct GeneratedEvent
        {
            public int Value;
        }

        private interface IGeneratedModule : IModule { }

        private sealed class GeneratedModule : IGeneratedModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public bool Initialized { get; private set; }
            public void OnInit(IModuleSystem host) { Initialized = true; }
            public void Shutdown() { Initialized = false; }
        }

        private static int _generatedEventTotal;

        [SetUp]
        public void SetUp()
        {
            ModuleRegistry.ClearForTests();
            EventHandlerRegistry.ClearForTests();
            _generatedEventTotal = 0;
        }

        [TearDown]
        public void TearDown()
        {
            ModuleRegistry.ClearForTests();
            EventHandlerRegistry.ClearForTests();
            _generatedEventTotal = 0;
        }

        [Test]
        public void CreateHost_RegistersCoreBaselineServices()
        {
            var host = GameLauncher.CreateHost();

            Assert.IsNotNull(host.Get<ILogger>());
            Assert.IsNotNull(host.Get<IClock>());
            Assert.IsNotNull(host.Get<ITGTaskScheduler>());
        }

        [Test]
        public void CreateHost_AppliesGeneratedModuleAndEventHandlerRegistries()
        {
            var generatedModule = new GeneratedModule();
            ModuleRegistry.RegisterWithMetadata(typeof(GeneratedModule), typeof(IGeneratedModule), "GeneratedTestAssembly");
            ModuleRegistry.Register(host => host.Register<IGeneratedModule>(generatedModule));

            EventHandlerRegistry.RegisterWithMetadata(
                "TryGet.Tests.GameLauncherTests.OnGeneratedEvent",
                typeof(GeneratedEvent),
                "GeneratedTestAssembly");
            EventHandlerRegistry.Register(bus => bus.Subscribe<GeneratedEvent>(OnGeneratedEvent));

            var host = GameLauncher.CreateHost();

            Assert.AreSame(generatedModule, host.Get<IGeneratedModule>());

            host.EventModule.Publish(new GeneratedEvent { Value = 7 });

            Assert.AreEqual(7, _generatedEventTotal);
        }

        private static void OnGeneratedEvent(GeneratedEvent evt)
        {
            _generatedEventTotal += evt.Value;
        }
    }
}
