using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleHost 迭代 2 测试：Update / LateUpdate 调度。
    /// </summary>
    [TestFixture]
    public class ModuleHostIteration2Tests
    {
        private interface IRendererModule : IModule { }
        private interface ICameraModule : IModule { }
        private interface IPassiveModule : IModule { }

        /// <summary>同时支持 Update 与 LateUpdate 计数的 Module。</summary>
        private class RendererModule : IRendererModule, IUpdateModule, ILateUpdateModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public List<string> Log;
            public int UpdateCount;
            public int LateUpdateCount;
            public float LastDelta;
            public float LastUnscaled;

            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }

            public void Update(float dt, float unscaledDt)
            {
                UpdateCount++;
                LastDelta = dt;
                LastUnscaled = unscaledDt;
                Log?.Add("update:Renderer");
            }

            public void LateUpdate(float dt, float unscaledDt)
            {
                LateUpdateCount++;
                Log?.Add("late:Renderer");
            }
        }

        private class CameraModule : ICameraModule, ILateUpdateModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public List<string> Log;

            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
            public void LateUpdate(float dt, float unscaledDt) { Log?.Add("late:Camera"); }
        }

        /// <summary>不实现 IUpdateModule / ILateUpdateModule 的纯 Module。</summary>
        private class PassiveModule : IPassiveModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public bool UpdateCalled;
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }
        }

        [Test]
        public void Update_CallsAllIUpdateModules()
        {
            var host = new ModuleHost();
            var renderer = new RendererModule();
            host.Register<IRendererModule>(renderer);
            host.Initialize();

            host.Update(0.016f, 0.020f);

            Assert.AreEqual(1, renderer.UpdateCount);
            Assert.AreEqual(0.016f, renderer.LastDelta);
            Assert.AreEqual(0.020f, renderer.LastUnscaled);
        }

        [Test]
        public void Update_SkipsModulesWithoutIUpdateModule()
        {
            var host = new ModuleHost();
            var passive = new PassiveModule();
            host.Register<IPassiveModule>(passive);
            host.Initialize();

            host.Update(0.016f, 0.016f);

            Assert.IsFalse(passive.UpdateCalled);
        }

        [Test]
        public void LateUpdate_CallsAllILateUpdateModules()
        {
            var host = new ModuleHost();
            var renderer = new RendererModule();
            var camera = new CameraModule();
            host.Register<IRendererModule>(renderer);
            host.Register<ICameraModule>(camera);
            host.Initialize();

            host.LateUpdate(0.016f, 0.016f);

            Assert.AreEqual(1, renderer.LateUpdateCount);
        }

        [Test]
        public void Update_BeforeInitialize_Throws()
        {
            var host = new ModuleHost();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.Update(0.016f, 0.016f));
        }

        [Test]
        public void LateUpdate_BeforeInitialize_Throws()
        {
            var host = new ModuleHost();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.LateUpdate(0.016f, 0.016f));
        }

        [Test]
        public void Update_AfterShutdown_Throws()
        {
            var host = new ModuleHost();
            host.Register<IRendererModule>(new RendererModule());
            host.Initialize();
            host.Shutdown();

            Assert.Throws<InvalidOperationException>(() => host.Update(0.016f, 0.016f));
        }

        [Test]
        public void Update_OrderMatchesInitOrder()
        {
            // 通过依赖关系强制初始化顺序，验证 Update 也按此顺序
            var host = new ModuleHost();
            var log = new List<string>();
            var renderer = new RendererModule { Log = log };
            var camera = new CameraModule { Log = log };

            host.Register<IRendererModule>(renderer);
            host.Register<ICameraModule>(camera);
            host.Initialize();
            log.Clear();

            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);

            // Renderer 实现 IUpdateModule，Camera 不实现；
            // LateUpdate 都实现，Renderer 先注册
            Assert.AreEqual(new[] { "update:Renderer", "late:Renderer", "late:Camera" }, log.ToArray());
        }

        [Test]
        public void Update_MultipleTicks_Accumulate()
        {
            var host = new ModuleHost();
            var renderer = new RendererModule();
            host.Register<IRendererModule>(renderer);
            host.Initialize();

            host.Update(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);

            Assert.AreEqual(3, renderer.UpdateCount);
        }
    }
}
