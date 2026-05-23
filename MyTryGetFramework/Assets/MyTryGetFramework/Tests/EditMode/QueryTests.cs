using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// Query 测试：All-of、None-of、组合匹配、生命周期尊重。
    /// </summary>
    [TestFixture]
    public class QueryTests
    {
        private class HealthAspect : Aspect { }
        private class MoveAspect : Aspect { }
        private class ShieldAspect : Aspect { }
        private class DeadTag : Tag { }

        [Test]
        public void AllOf_SingleAspect_MatchesCorrectly()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();
            a.Attach(new HealthAspect());

            Query query = Query.Create().WithAll<HealthAspect>().Build();

            Assert.IsTrue(query.Matches(a));
            Assert.IsFalse(query.Matches(b));

            world.Shutdown();
        }

        [Test]
        public void AllOf_MultipleAspects_RequiresAll()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();
            a.Attach(new HealthAspect());
            a.Attach(new MoveAspect());
            b.Attach(new HealthAspect());

            Query query = Query.Create()
                .WithAll<HealthAspect>()
                .WithAll<MoveAspect>()
                .Build();

            Assert.IsTrue(query.Matches(a));
            Assert.IsFalse(query.Matches(b));

            world.Shutdown();
        }

        [Test]
        public void NoneOf_ExcludesMatching()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();
            a.Attach(new HealthAspect());
            b.Attach(new HealthAspect());
            b.Attach(new ShieldAspect());

            Query query = Query.Create()
                .WithAll<HealthAspect>()
                .WithNone<ShieldAspect>()
                .Build();

            Assert.IsTrue(query.Matches(a));
            Assert.IsFalse(query.Matches(b));

            world.Shutdown();
        }

        [Test]
        public void NoneOfTag_ExcludesMatching()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();
            a.Attach(new HealthAspect());
            b.Attach(new HealthAspect());
            b.AddTag<DeadTag>();

            Query query = Query.Create()
                .WithAll<HealthAspect>()
                .WithNoneTag<DeadTag>()
                .Build();

            Assert.IsTrue(query.Matches(a));
            Assert.IsFalse(query.Matches(b));

            world.Shutdown();
        }

        [Test]
        public void AllOfTag_RequiresTag()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            Entity b = world.CreateEntity();
            a.AddTag<DeadTag>();

            Query query = Query.Create().WithAllTag<DeadTag>().Build();

            Assert.IsTrue(query.Matches(a));
            Assert.IsFalse(query.Matches(b));

            world.Shutdown();
        }

        [Test]
        public void DestroyedEntity_DoesNotMatch()
        {
            var world = new World("Test");
            Entity a = world.CreateEntity();
            a.Attach(new HealthAspect());

            Query query = Query.Create().WithAll<HealthAspect>().Build();
            Assert.IsTrue(query.Matches(a));

            world.DestroyEntity(a);
            Assert.IsFalse(query.Matches(a));

            world.Shutdown();
        }
    }
}
