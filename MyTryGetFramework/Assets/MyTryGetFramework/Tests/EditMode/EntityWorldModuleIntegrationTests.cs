using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// EntityWorld + ModuleHost 协同测试。来自 V0.3 迭代 0 Plan agent 审阅建议项 4：
    /// 验证 host.Register&lt;IEntityWorld&gt; → host.Initialize → host.Update → host.Shutdown 闭环。
    /// </summary>
    [TestFixture]
    public class EntityWorldModuleIntegrationTests
    {
        private class CountSystem : SystemBase
        {
            public int ExecuteCount;
            protected override void Execute(IReadOnlyList<Entity> entities) => ExecuteCount++;
        }

        [Test]
        public void ModuleHost_DrivesEntityWorld_FullLifecycle()
        {
            var host = new ModuleHost();
            var world = new EntityWorld("Game");
            host.Register<IEntityWorld>(world);

            Assert.AreEqual(EntityWorldState.Created, world.State, "Initialize 前未启动");

            host.Initialize();

            Assert.AreEqual(EntityWorldState.Running, world.State, "Initialize 触发 OnInit→Start");

            // 注册 System 后驱动 Update Phase
            var system = new CountSystem();
            world.RegisterSystem(system, Phase.Update);

            host.Update(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);

            Assert.AreEqual(2, system.ExecuteCount, "host.Update 驱动 EntityWorld.Update 推进 Update Phase");

            host.Shutdown();

            Assert.IsFalse(host.IsInitialized);
            Assert.AreEqual(EntityWorldState.Shutdown, world.State);
        }

        [Test]
        public void EntityWorldStartedBeforeRegister_OnInitDoesNotRestart()
        {
            // 用户先用 V0.1 直接 API：world.Start()
            var world = new EntityWorld("Standalone");
            world.Start();
            Assert.AreEqual(EntityWorldState.Running, world.State);

            // 再注册到 ModuleHost
            var host = new ModuleHost();
            host.Register<IEntityWorld>(world);
            host.Initialize();

            // OnInit 看到 _state != Created，不重复 Start —— world 仍在 Running
            Assert.AreEqual(EntityWorldState.Running, world.State, "OnInit 不应重复启动");
        }

        [Test]
        public void EntityWorld_ShutdownBeforeHostUpdate_UpdateIsSilent()
        {
            // 一种现实场景：业务代码（如 Procedure 切场景）提前关闭 EntityWorld，
            // 下一帧 host.Update 不应崩。
            var host = new ModuleHost();
            var world = new EntityWorld("Test");
            host.Register<IEntityWorld>(world);
            host.Initialize();

            world.Shutdown(); // 业务侧提前关闭

            Assert.DoesNotThrow(() => host.Update(0.016f, 0.016f),
                "Shutdown 后的 EntityWorld.Update 应静默返回，不再抛");
        }

        [Test]
        public void IEntityWorld_ExposesEventBus_ForCrossModuleUse()
        {
            // 业务 Module 通过 IEntityWorld 接口拉 EventBus（不应需要强转具体类）
            var host = new ModuleHost();
            host.Register<IEntityWorld>(new EntityWorld("Test"));
            host.Initialize();

            IEntityWorld w = host.Get<IEntityWorld>();
            Assert.IsNotNull(w.EventBus, "IEntityWorld.EventBus 应可通过接口直接访问");

            host.Shutdown();
        }

        [Test]
        public void EntityWorld_ShutdownByHost_PropagatesToWorldState()
        {
            var host = new ModuleHost();
            var world = new EntityWorld("Test");
            host.Register<IEntityWorld>(world);
            host.Initialize();

            host.Shutdown();

            Assert.AreEqual(EntityWorldState.Shutdown, world.State, "host.Shutdown 应通过 IModule.Shutdown 传播到 EntityWorld");
        }
    }
}
