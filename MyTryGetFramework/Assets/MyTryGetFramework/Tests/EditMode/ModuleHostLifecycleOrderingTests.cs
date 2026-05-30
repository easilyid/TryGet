using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleHost 生命周期排序测试。
    /// </summary>
    [TestFixture]
    public class ModuleHostLifecycleOrderingTests
    {
        private interface IAModule : IModule { }
        private interface IBModule : IModule { }
        private interface ICModule : IModule { }

        /// <summary>记录调用顺序的 Module 基类。</summary>
        private class TrackingModule : IModule
        {
            public List<string> Log;
            public string Name;
            public Type[] Dependencies = Array.Empty<Type>();
            public int Pri = 0;

            public int Priority => Pri;
            public IReadOnlyList<Type> DependsOn => Dependencies;

            public void OnInit(IModuleHost host)
            {
                Log?.Add("init:" + Name);
            }

            public void Shutdown()
            {
                Log?.Add("shutdown:" + Name);
            }
        }

        private class AModule : TrackingModule, IAModule { }
        private class BModule : TrackingModule, IBModule { }
        private class CModule : TrackingModule, ICModule { }

        [Test]
        public void Initialize_NoDependencies_PriorityOrderRespected()
        {
            var log = new List<string>();
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule { Log = log, Name = "A", Pri = 20 });
            host.Register<IBModule>(new BModule { Log = log, Name = "B", Pri = 10 });
            host.Register<ICModule>(new CModule { Log = log, Name = "C", Pri = 30 });

            host.Initialize();

            // 同一拓扑层（都无依赖），按 Priority 升序：B(10) → A(20) → C(30)
            Assert.AreEqual(new[] { "init:B", "init:A", "init:C" }, log.ToArray());
        }

        [Test]
        public void Initialize_LinearDependencyChain_TopologicalOrder()
        {
            var log = new List<string>();
            var host = new ModuleHost();
            // A 依赖 B，B 依赖 C → 期望 C → B → A
            host.Register<IAModule>(new AModule { Log = log, Name = "A", Dependencies = new[] { typeof(IBModule) } });
            host.Register<IBModule>(new BModule { Log = log, Name = "B", Dependencies = new[] { typeof(ICModule) } });
            host.Register<ICModule>(new CModule { Log = log, Name = "C" });

            host.Initialize();

            Assert.AreEqual(new[] { "init:C", "init:B", "init:A" }, log.ToArray());
        }

        [Test]
        public void Initialize_UnregisteredDependency_Throws()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule { Dependencies = new[] { typeof(IBModule) } });

            var ex = Assert.Throws<InvalidOperationException>(() => host.Initialize());
            StringAssert.Contains("IBModule", ex.Message);
        }

        [Test]
        public void Initialize_CircularDependency_Throws()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule { Name = "A", Dependencies = new[] { typeof(IBModule) } });
            host.Register<IBModule>(new BModule { Name = "B", Dependencies = new[] { typeof(IAModule) } });

            var ex = Assert.Throws<InvalidOperationException>(() => host.Initialize());
            StringAssert.Contains("Circular", ex.Message);
        }

        [Test]
        public void Initialize_SelfDependency_Throws()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule { Name = "A", Dependencies = new[] { typeof(IAModule) } });

            var ex = Assert.Throws<InvalidOperationException>(() => host.Initialize());
            StringAssert.Contains("itself", ex.Message);
        }

        [Test]
        public void Initialize_Twice_Throws()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule());
            host.Initialize();

            Assert.Throws<InvalidOperationException>(() => host.Initialize());
        }

        [Test]
        public void Register_AfterInitialize_Throws()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule());
            host.Initialize();

            Assert.Throws<InvalidOperationException>(() =>
                host.Register<IBModule>(new BModule()));
        }

        [Test]
        public void Shutdown_ReverseOfInitOrder()
        {
            var log = new List<string>();
            var host = new ModuleHost();
            // 期望初始化序：C → B → A，关闭序：A → B → C
            host.Register<IAModule>(new AModule { Log = log, Name = "A", Dependencies = new[] { typeof(IBModule) } });
            host.Register<IBModule>(new BModule { Log = log, Name = "B", Dependencies = new[] { typeof(ICModule) } });
            host.Register<ICModule>(new CModule { Log = log, Name = "C" });

            host.Initialize();
            log.Clear();
            host.Shutdown();

            Assert.AreEqual(new[] { "shutdown:A", "shutdown:B", "shutdown:C" }, log.ToArray());
        }

        [Test]
        public void Shutdown_BeforeInitialize_NoOp()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule());

            Assert.DoesNotThrow(() => host.Shutdown());
        }

        [Test]
        public void Shutdown_Twice_OnlyShutdownsOnce()
        {
            var log = new List<string>();
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule { Log = log, Name = "A" });
            host.Initialize();

            host.Shutdown();
            host.Shutdown();

            Assert.AreEqual(1, log.FindAll(s => s == "shutdown:A").Count);
        }

        [Test]
        public void IsInitialized_TracksLifecycle()
        {
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule());

            Assert.IsFalse(host.IsInitialized);
            host.Initialize();
            Assert.IsTrue(host.IsInitialized);
            host.Shutdown();
            Assert.IsFalse(host.IsInitialized);
        }

        [Test]
        public void Initialize_SameInstanceMultipleInterfaces_OnInitCalledOnce()
        {
            var log = new List<string>();
            var host = new ModuleHost();
            var dual = new DualModule { Log = log, Name = "Dual" };

            host.Register<IAModule>(dual);
            host.Register<IBModule>(dual);
            host.Initialize();

            // OnInit 只应被调用一次，即使注册了两个接口
            Assert.AreEqual(1, log.FindAll(s => s == "init:Dual").Count);
        }

        [Test]
        public void Shutdown_SameInstanceMultipleInterfaces_ShutdownCalledOnce()
        {
            var log = new List<string>();
            var host = new ModuleHost();
            var dual = new DualModule { Log = log, Name = "Dual" };

            host.Register<IAModule>(dual);
            host.Register<IBModule>(dual);
            host.Initialize();
            log.Clear();
            host.Shutdown();

            Assert.AreEqual(1, log.FindAll(s => s == "shutdown:Dual").Count);
        }

        [Test]
        public void Initialize_DiamondDependency_AllInitInValidOrder()
        {
            // D 依赖 A,B,C；A 依赖 C，B 依赖 C → C 必须最先，D 必须最后
            var log = new List<string>();
            var host = new ModuleHost();
            host.Register<IAModule>(new AModule { Log = log, Name = "A", Dependencies = new[] { typeof(ICModule) } });
            host.Register<IBModule>(new BModule { Log = log, Name = "B", Dependencies = new[] { typeof(ICModule) } });
            host.Register<ICModule>(new CModule { Log = log, Name = "C" });
            host.Register<IDModule>(new DModule { Log = log, Name = "D",
                Dependencies = new[] { typeof(IAModule), typeof(IBModule), typeof(ICModule) } });

            host.Initialize();

            Assert.AreEqual("init:C", log[0]);
            Assert.AreEqual("init:D", log[3]);
        }

        private interface IDModule : IModule { }
        private class DModule : TrackingModule, IDModule { }

        private class DualModule : TrackingModule, IAModule, IBModule { }
    }
}
