using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// Tag 测试：添加/移除、存在性检查。
    /// </summary>
    [TestFixture]
    public class TagTests
    {
        private class DeadTag : Tag { }
        private class PoisonedTag : Tag { }

        [Test]
        public void AddTag_EntityHasTag()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();

            bool added = entity.AddTag<DeadTag>();

            Assert.IsTrue(added);
            Assert.IsTrue(entity.HasTag<DeadTag>());

            world.Shutdown();
        }

        [Test]
        public void RemoveTag_EntityNoLongerHasTag()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            entity.AddTag<DeadTag>();

            bool removed = entity.RemoveTag<DeadTag>();

            Assert.IsTrue(removed);
            Assert.IsFalse(entity.HasTag<DeadTag>());

            world.Shutdown();
        }

        [Test]
        public void AddDuplicateTag_ReturnsFalse()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            entity.AddTag<DeadTag>();

            bool addedAgain = entity.AddTag<DeadTag>();

            Assert.IsFalse(addedAgain);

            world.Shutdown();
        }

        [Test]
        public void MultipleTags_Coexist()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            entity.AddTag<DeadTag>();
            entity.AddTag<PoisonedTag>();

            Assert.IsTrue(entity.HasTag<DeadTag>());
            Assert.IsTrue(entity.HasTag<PoisonedTag>());

            world.Shutdown();
        }
    }
}
