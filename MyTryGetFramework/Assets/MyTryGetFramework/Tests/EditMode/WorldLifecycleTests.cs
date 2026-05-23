using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// World 生命周期测试：创建/销毁 Entity、Phase 推进、关闭行为。
    /// </summary>
    [TestFixture]
    public class WorldLifecycleTests
    {
        [Test]
        public void CreateEntity_ReturnsEntityWithValidId()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();

            Assert.IsNotNull(entity);
            Assert.IsTrue(entity.Id.IsValid);
            Assert.AreEqual(world, entity.World);
            Assert.IsFalse(entity.IsDestroyed);

            world.Shutdown();
        }

        [Test]
        public void CreateMultipleEntities_HaveUniqueIds()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();

            Assert.AreNotEqual(a.Id, b.Id);

            world.Shutdown();
        }

        [Test]
        public void DestroyEntity_MarksAsDestroyed()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            world.DestroyEntity(entity);

            Assert.IsTrue(entity.IsDestroyed);

            world.Shutdown();
        }

        [Test]
        public void GetEntity_ReturnsNullForDestroyed()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            EntityId id = entity.Id;
            world.DestroyEntity(entity);

            Assert.IsNull(world.GetEntity(id));

            world.Shutdown();
        }

        [Test]
        public void WorldStart_TransitionsToRunning()
        {
            var world = new World("Test");
            Assert.AreEqual(WorldState.Created, world.State);

            world.Start();
            Assert.AreEqual(WorldState.Running, world.State);

            world.Shutdown();
        }

        [Test]
        public void WorldShutdown_TransitionsToShutdown()
        {
            var world = new World("Test");
            world.Start();
            world.Shutdown();

            Assert.AreEqual(WorldState.Shutdown, world.State);
        }

        [Test]
        public void WorldShutdown_DestroysAllEntities()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();
            world.Start();
            world.Shutdown();

            Assert.IsTrue(a.IsDestroyed);
            Assert.IsTrue(b.IsDestroyed);
            Assert.AreEqual(0, world.Entities.Count);
        }
    }
}
