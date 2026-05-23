using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// System 调度测试：Phase 执行、Query 匹配、SystemGroup 顺序。
    /// </summary>
    [TestFixture]
    public class SystemSchedulingTests
    {
        private class HealthAspect : Aspect
        {
            public int Current { get; set; } = 100;
        }

        private class DamageSystem : SystemBase
        {
            public int ExecuteCount;
            public List<Entity> LastEntities;

            protected override void OnCreate()
            {
                Query = Query.Create().WithAll<HealthAspect>().Build();
            }

            protected override void Execute(IReadOnlyList<Entity> entities)
            {
                ExecuteCount++;
                LastEntities = new List<Entity>(entities);
                foreach (var e in entities)
                {
                    var health = e.GetAspect<HealthAspect>();
                    if (health != null)
                        health.Current -= 10;
                }
            }
        }

        private class TrackingSystem : SystemBase
        {
            public List<string> ExecutionLog;

            public TrackingSystem(List<string> log, string name)
            {
                ExecutionLog = log;
                _name = name;
            }

            private string _name;

            protected override void OnCreate()
            {
                // No query — receives empty list
            }

            protected override void Execute(IReadOnlyList<Entity> entities)
            {
                ExecutionLog.Add(_name);
            }
        }

        [Test]
        public void System_RunsInUpdatePhase()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            entity.Attach(new HealthAspect());

            var system = new DamageSystem();
            world.RegisterSystem(system, Phase.Update);
            world.Start();
            world.Update();

            Assert.AreEqual(1, system.ExecuteCount);
            Assert.AreEqual(1, system.LastEntities.Count);
            Assert.AreEqual(90, entity.GetAspect<HealthAspect>().Current);

            world.Shutdown();
        }

        [Test]
        public void System_OnlyReceivesQueryMatchedEntities()
        {
            var world = new World("Test");
            Entity withHealth = world.CreateEntity();
            Entity without = world.CreateEntity();
            withHealth.Attach(new HealthAspect());

            var system = new DamageSystem();
            world.RegisterSystem(system, Phase.Update);
            world.Start();
            world.Update();

            Assert.AreEqual(1, system.LastEntities.Count);
            Assert.IsTrue(system.LastEntities.Contains(withHealth));
            Assert.IsFalse(system.LastEntities.Contains(without));

            world.Shutdown();
        }

        [Test]
        public void SystemGroup_DeterminesExecutionOrder()
        {
            var world = new World("Test");
            var log = new List<string>();

            var groupA = new SystemGroup("GroupA");
            var groupB = new SystemGroup("GroupB");

            world.AddSystemGroup(groupA, Phase.Update);
            world.AddSystemGroup(groupB, Phase.Update);

            world.RegisterSystem(new TrackingSystem(log, "B1"), Phase.Update, groupB);
            world.RegisterSystem(new TrackingSystem(log, "A1"), Phase.Update, groupA);
            world.RegisterSystem(new TrackingSystem(log, "A2"), Phase.Update, groupA);

            world.Start();
            world.Update();

            // Group 执行顺序 = 添加顺序：GroupA → GroupB
            // System 执行顺序 = 注册顺序
            Assert.AreEqual(3, log.Count);
            Assert.AreEqual("A1", log[0]);
            Assert.AreEqual("A2", log[1]);
            Assert.AreEqual("B1", log[2]);

            world.Shutdown();
        }

        [Test]
        public void EnterPhaseSystem_RunsOnceAtStart()
        {
            var world = new World("Test");
            var log = new List<string>();

            world.RegisterSystem(new TrackingSystem(log, "EnterSys"), Phase.Enter);
            world.Start();
            world.Update();
            world.Update();

            Assert.AreEqual(1, log.Count);
            Assert.AreEqual("EnterSys", log[0]);

            world.Shutdown();
        }

        [Test]
        public void ExitPhaseSystem_RunsOnceAtShutdown()
        {
            var world = new World("Test");
            var log = new List<string>();

            world.RegisterSystem(new TrackingSystem(log, "ExitSys"), Phase.Exit);
            world.Start();
            world.Update();
            world.Shutdown();

            Assert.AreEqual(1, log.Count);
            Assert.AreEqual("ExitSys", log[0]);
        }

        [Test]
        public void DestroyedEntity_NotReceivedBySystem()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            entity.Attach(new HealthAspect());

            var system = new DamageSystem();
            world.RegisterSystem(system, Phase.Update);
            world.Start();

            world.DestroyEntity(entity);
            world.Update();

            Assert.AreEqual(1, system.ExecuteCount);
            Assert.AreEqual(0, system.LastEntities.Count);

            world.Shutdown();
        }
    }
}
