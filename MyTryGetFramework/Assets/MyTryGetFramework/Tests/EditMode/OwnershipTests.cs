using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// Entity Ownership 测试：父子关系、级联销毁、Detach、关闭策略 (Issue-07/08)。
    /// </summary>
    [TestFixture]
    public class OwnershipTests
    {
        private class TraceAspect : Aspect
        {
            public List<string> Log;
            public string Tag;

            protected override void OnDetach()
            {
                Log?.Add(Tag);
            }
        }

        [Test]
        public void AttachChild_EstablishesParentChildRelation()
        {
            var world = new World("Test");
            Entity parent = world.CreateEntity();
            Entity child = world.CreateEntity();

            parent.AttachChild(child);

            Assert.AreEqual(parent, child.Parent);
            Assert.AreEqual(1, parent.Children.Count);
            Assert.AreEqual(child, parent.Children[0]);

            world.Shutdown();
        }

        [Test]
        public void DetachChild_RemovesRelation()
        {
            var world = new World("Test");
            Entity parent = world.CreateEntity();
            Entity child = world.CreateEntity();
            parent.AttachChild(child);

            bool detached = parent.DetachChild(child);

            Assert.IsTrue(detached);
            Assert.IsNull(child.Parent);
            Assert.AreEqual(0, parent.Children.Count);
            Assert.IsFalse(child.IsDestroyed);

            world.Shutdown();
        }

        [Test]
        public void DestroyParent_CascadeDestroysChildren()
        {
            var world = new World("Test");
            Entity parent = world.CreateEntity();
            Entity child = world.CreateEntity();
            Entity grandchild = world.CreateEntity();
            parent.AttachChild(child);
            child.AttachChild(grandchild);

            world.DestroyEntity(parent);

            Assert.IsTrue(parent.IsDestroyed);
            Assert.IsTrue(child.IsDestroyed);
            Assert.IsTrue(grandchild.IsDestroyed);

            world.Shutdown();
        }

        [Test]
        public void DestroyParent_LeafFirstOnDetachOrder()
        {
            // Issue-08 AC：多层 Ownership 树销毁按叶子优先（深度优先 post-order）执行 Aspect.OnDetach。
            var world = new World("Test");
            var log = new List<string>();

            Entity root = world.CreateEntity();
            Entity mid = world.CreateEntity();
            Entity leaf = world.CreateEntity();
            root.AttachChild(mid);
            mid.AttachChild(leaf);

            root.Attach(new TraceAspect { Log = log, Tag = "root" });
            mid.Attach(new TraceAspect { Log = log, Tag = "mid" });
            leaf.Attach(new TraceAspect { Log = log, Tag = "leaf" });

            world.DestroyEntity(root);

            Assert.AreEqual(new[] { "leaf", "mid", "root" }, log.ToArray());

            world.Shutdown();
        }

        [Test]
        public void DestroyedEntity_RemovedFromWorldEntityList()
        {
            // Issue-08 AC：级联销毁后 _entities / _entityList 不再包含已销毁的子 Entity。
            var world = new World("Test");
            Entity parent = world.CreateEntity();
            Entity child = world.CreateEntity();
            parent.AttachChild(child);

            world.DestroyEntity(parent);

            Assert.AreEqual(0, world.Entities.Count);
            Assert.IsNull(world.GetEntity(parent.Id));
            Assert.IsNull(world.GetEntity(child.Id));

            world.Shutdown();
        }

        [Test]
        public void DetachBeforeDestroy_ChildSurvives()
        {
            var world = new World("Test");
            Entity parent = world.CreateEntity();
            Entity child = world.CreateEntity();
            parent.AttachChild(child);

            parent.DetachChild(child);
            world.DestroyEntity(parent);

            Assert.IsTrue(parent.IsDestroyed);
            Assert.IsFalse(child.IsDestroyed);

            world.Shutdown();
        }

        [Test]
        public void DetachChildOnDestroyedParent_Throws()
        {
            // Issue-08 AC：已销毁 Entity 拒绝 Detach 操作。
            var world = new World("Test");
            Entity parent = world.CreateEntity();
            Entity child = world.CreateEntity();
            parent.AttachChild(child);

            // 让 child 在销毁前先 Detach 出来，避免被级联销毁。
            parent.DetachChild(child);
            world.DestroyEntity(parent);

            Assert.Throws<InvalidOperationException>(() => parent.DetachChild(child));

            world.Shutdown();
        }

        [Test]
        public void Handle_ResolvesLiveEntity()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            Handle handle = entity.GetHandle();

            Entity resolved = handle.Resolve(world);
            Assert.AreEqual(entity, resolved);

            world.Shutdown();
        }

        [Test]
        public void Handle_ReturnsNullForDestroyedEntity()
        {
            var world = new World("Test");
            Entity entity = world.CreateEntity();
            Handle handle = entity.GetHandle();

            world.DestroyEntity(entity);
            Entity resolved = handle.Resolve(world);

            Assert.IsNull(resolved);

            world.Shutdown();
        }
    }
}
