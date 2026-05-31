using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ModuleSystem 帧调度测试。
    /// </summary>
    [TestFixture]
    public class ModuleSystemFrameDispatchTests
    {
        private interface IRendererModule : IModule { }
        private interface ICameraModule : IModule { }
        private interface IPassiveModule : IModule { }

        private class RendererModule : IRendererModule, IEarlyUpdateModule, IFixedUpdateModule, IUpdateModule, ILateUpdateModule, IEndOfFrameModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public List<string> Log;
            public int EarlyUpdateCount;
            public int FixedUpdateCount;
            public int UpdateCount;
            public int LateUpdateCount;
            public int EndOfFrameCount;
            public float LastDelta;
            public float LastUnscaled;

            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }

            public void EarlyUpdate(float dt, float unscaledDt)
            {
                EarlyUpdateCount++;
                Log?.Add("early:Renderer");
            }

            public void FixedUpdate(float dt, float unscaledDt)
            {
                FixedUpdateCount++;
                Log?.Add("fixed:Renderer");
            }

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

            public void EndOfFrame(float dt, float unscaledDt)
            {
                EndOfFrameCount++;
                Log?.Add("end:Renderer");
            }
        }

        private class CameraModule : ICameraModule, ILateUpdateModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn { get; set; } = Array.Empty<Type>();
            public List<string> Log;

            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
            public void LateUpdate(float dt, float unscaledDt) { Log?.Add("late:Camera"); }
        }

                private class PassiveModule : IPassiveModule
        {
            public int Priority => 0;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public bool UpdateCalled;
            public void OnInit(IModuleSystem host) { }
            public void Shutdown() { }
        }

        [Test]
        public void EarlyUpdate_CallsAllIEarlyUpdateModules()
        {
            var host = new ModuleSystem();
            var renderer = new RendererModule();
            host.Register<IRendererModule>(renderer);
            host.Initialize();

            host.EarlyUpdate(0.016f, 0.020f);

            Assert.AreEqual(1, renderer.EarlyUpdateCount);
        }

        [Test]
        public void FixedUpdate_CallsAllIFixedUpdateModules()
        {
            var host = new ModuleSystem();
            var renderer = new RendererModule();
            host.Register<IRendererModule>(renderer);
            host.Initialize();

            host.FixedUpdate(0.016f, 0.020f);

            Assert.AreEqual(1, renderer.FixedUpdateCount);
        }

        [Test]
        public void Update_CallsAllIUpdateModules()
        {
            var host = new ModuleSystem();
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
            var host = new ModuleSystem();
            var passive = new PassiveModule();
            host.Register<IPassiveModule>(passive);
            host.Initialize();

            host.Update(0.016f, 0.016f);

            Assert.IsFalse(passive.UpdateCalled);
        }

        [Test]
        public void LateUpdate_CallsAllILateUpdateModules()
        {
            var host = new ModuleSystem();
            var renderer = new RendererModule();
            var camera = new CameraModule();
            host.Register<IRendererModule>(renderer);
            host.Register<ICameraModule>(camera);
            host.Initialize();

            host.LateUpdate(0.016f, 0.016f);

            Assert.AreEqual(1, renderer.LateUpdateCount);
        }

        [Test]
        public void EarlyUpdate_BeforeInitialize_Throws()
        {
            var host = new ModuleSystem();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.EarlyUpdate(0.016f, 0.016f));
        }

        [Test]
        public void FixedUpdate_BeforeInitialize_Throws()
        {
            var host = new ModuleSystem();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.FixedUpdate(0.016f, 0.016f));
        }

        [Test]
        public void Update_BeforeInitialize_Throws()
        {
            var host = new ModuleSystem();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.Update(0.016f, 0.016f));
        }

        [Test]
        public void LateUpdate_BeforeInitialize_Throws()
        {
            var host = new ModuleSystem();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.LateUpdate(0.016f, 0.016f));
        }

        [Test]
        public void EndOfFrame_BeforeInitialize_Throws()
        {
            var host = new ModuleSystem();
            host.Register<IRendererModule>(new RendererModule());

            Assert.Throws<InvalidOperationException>(() => host.EndOfFrame(0.016f, 0.016f));
        }

        [Test]
        public void Update_AfterShutdown_Throws()
        {
            var host = new ModuleSystem();
            host.Register<IRendererModule>(new RendererModule());
            host.Initialize();
            host.Shutdown();

            Assert.Throws<InvalidOperationException>(() => host.Update(0.016f, 0.016f));
        }

        [Test]
        public void Update_OrderMatchesInitOrder()
        {
            // 用 DependsOn 强制拓扑顺序：Camera 依赖 Renderer，验证 LateUpdate 序与拓扑序一致。
            var host = new ModuleSystem();
            var log = new List<string>();
            var renderer = new RendererModule { Log = log };
            var camera = new CameraModule { Log = log, DependsOn = new[] { typeof(IRendererModule) } };

            // 故意打乱注册顺序（Camera 先注册）
            host.Register<ICameraModule>(camera);
            host.Register<IRendererModule>(renderer);
            host.Initialize();
            log.Clear();

            host.EarlyUpdate(0.016f, 0.016f);
            host.FixedUpdate(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);
            host.EndOfFrame(0.016f, 0.016f);

            // 拓扑序：Renderer → Camera。Renderer 实现 IUpdate + ILate，Camera 仅 ILate
            Assert.AreEqual(new[] { "early:Renderer", "fixed:Renderer", "update:Renderer", "late:Renderer", "late:Camera", "end:Renderer" }, log.ToArray());
        }

        [Test]
        public void EndOfFrame_CallsAllIEndOfFrameModules()
        {
            var host = new ModuleSystem();
            var renderer = new RendererModule();
            host.Register<IRendererModule>(renderer);
            host.Initialize();

            host.EndOfFrame(0.016f, 0.020f);

            Assert.AreEqual(1, renderer.EndOfFrameCount);
        }

        [Test]
        public void Update_MultipleTicks_Accumulate()
        {
            var host = new ModuleSystem();
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
