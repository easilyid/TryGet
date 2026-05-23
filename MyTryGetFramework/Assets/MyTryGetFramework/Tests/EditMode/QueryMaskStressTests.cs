using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.3 迭代 1 Query 位运算加速的压力测试 + 行为正确性验证。
    ///
    /// 范围：
    /// - 10000 Entity × 5 Query 大数据集稳定性（不崩 + 不漏匹配）
    /// - Entity._aspectMask / _tagMask 与 _aspects / _tags 字典一致性
    /// - AspectType 重写场景的 mask 一致性
    /// </summary>
    [TestFixture]
    public class QueryMaskStressTests
    {
        private class HealthAspect : Aspect { public int HP; }
        private class ArmorAspect : Aspect { public int Defense; }
        private class WeaponAspect : Aspect { public int Damage; }
        private class PoisonAspect : Aspect { public int DamagePerTick; }

        private class PlayerTag : Tag { }
        private class EnemyTag : Tag { }
        private class BossTag : Tag { }

        [Test]
        public void Stress_10000Entity_10Query_AllMatchesCorrect()
        {
            var world = new EntityWorld("Stress");
            var entities = new List<Entity>(10000);

            // 创建 10000 entity，按 i % 4 给不同 Aspect 组合
            for (int i = 0; i < 10000; i++)
            {
                var e = world.CreateEntity();
                if ((i & 1) == 0) e.Attach(new HealthAspect { HP = 100 });
                if ((i & 2) == 0) e.Attach(new ArmorAspect { Defense = 50 });
                if ((i & 4) == 0) e.AddTag<PlayerTag>();
                if ((i & 8) == 0) e.AddTag<EnemyTag>();
                entities.Add(e);
            }

            var qHealthOnly = Query.Create().WithAll<HealthAspect>().Build();
            var qHealthArmor = Query.Create().WithAll<HealthAspect>().WithAll<ArmorAspect>().Build();
            var qHealthNoArmor = Query.Create().WithAll<HealthAspect>().WithNone<ArmorAspect>().Build();
            var qPlayer = Query.Create().WithAllTag<PlayerTag>().Build();
            var qPlayerNotEnemy = Query.Create().WithAllTag<PlayerTag>().WithNoneTag<EnemyTag>().Build();

            // 计数验证
            int healthCount = 0, healthArmorCount = 0, healthNoArmorCount = 0, playerCount = 0, playerNotEnemyCount = 0;
            var sw = Stopwatch.StartNew();
            foreach (var e in entities)
            {
                if (qHealthOnly.Matches(e)) healthCount++;
                if (qHealthArmor.Matches(e)) healthArmorCount++;
                if (qHealthNoArmor.Matches(e)) healthNoArmorCount++;
                if (qPlayer.Matches(e)) playerCount++;
                if (qPlayerNotEnemy.Matches(e)) playerNotEnemyCount++;
            }
            sw.Stop();

            // 10000 * 5 = 50000 次 Matches。位运算路径应在数毫秒内完成。
            // 不做硬性时间断言（不同机器差异大），仅记录在测试输出供观察。
            Console.WriteLine($"[QueryMaskStress] 10000 entity × 5 query = 50000 Matches in {sw.ElapsedMilliseconds} ms");

            // 数学验证（位运算结果应等于按位组合的期望）：
            Assert.AreEqual(5000, healthCount, "HealthAspect 在偶数 entity 上：i&1==0 => 5000");
            Assert.AreEqual(2500, healthArmorCount, "Health + Armor：i&3==0 => 2500");
            Assert.AreEqual(2500, healthNoArmorCount, "Health 但无 Armor：i&3==1 => 2500");
            Assert.AreEqual(5000, playerCount, "PlayerTag：i&4==0 => 5000");
            Assert.AreEqual(2500, playerNotEnemyCount, "Player 但非 Enemy：(i&4)==0 && (i&8)!=0 => 2500");

            world.Shutdown();
        }

        [Test]
        public void EntityMask_StaysInSync_AfterAttachDetach()
        {
            var world = new EntityWorld("MaskSync");
            var e = world.CreateEntity();

            Assert.IsFalse(e.HasAspect<HealthAspect>(), "初始无 Aspect");

            var h = new HealthAspect();
            e.Attach(h);
            Assert.IsTrue(e.HasAspect<HealthAspect>(), "Attach 后 mask 应包含");

            e.Detach(h);
            Assert.IsFalse(e.HasAspect<HealthAspect>(), "Detach 后 mask 应不包含");

            // 重新 Attach 应工作
            e.Attach(new HealthAspect());
            Assert.IsTrue(e.HasAspect<HealthAspect>(), "重新 Attach 同类型 mask 应再次包含");

            world.Shutdown();
        }

        [Test]
        public void EntityMask_StaysInSync_AfterAddRemoveTag()
        {
            var world = new EntityWorld("MaskSync");
            var e = world.CreateEntity();

            Assert.IsFalse(e.HasTag<PlayerTag>());

            e.AddTag<PlayerTag>();
            Assert.IsTrue(e.HasTag<PlayerTag>());

            e.RemoveTag<PlayerTag>();
            Assert.IsFalse(e.HasTag<PlayerTag>());

            world.Shutdown();
        }

        [Test]
        public void EntityMask_AfterMarkDestroyed_IsCleared()
        {
            var world = new EntityWorld("MaskClear");
            var e = world.CreateEntity();
            e.Attach(new HealthAspect());
            e.AddTag<PlayerTag>();

            world.DestroyEntity(e);

            Assert.IsTrue(e.IsDestroyed);
            Assert.IsFalse(e.HasAspect<HealthAspect>(), "Destroy 应清 _aspectMask");
            Assert.IsFalse(e.HasTag<PlayerTag>(), "Destroy 应清 _tagMask");

            world.Shutdown();
        }

        [Test]
        public void Query_OnNullOrDestroyedEntity_ReturnsFalse()
        {
            var q = Query.Create().WithAll<HealthAspect>().Build();
            Assert.IsFalse(q.Matches(null));

            var world = new EntityWorld("Test");
            var e = world.CreateEntity();
            e.Attach(new HealthAspect());
            world.DestroyEntity(e);
            Assert.IsFalse(q.Matches(e), "已 destroy 的 entity 不应被 Match");

            world.Shutdown();
        }

        [Test]
        public void Query_EmptyAllOfAndNoneOf_MatchesAnyEntity()
        {
            // 空 Query（trivially true）：应匹配任何活跃 entity
            var q = Query.Create().Build();

            var world = new EntityWorld("Test");
            var bare = world.CreateEntity();
            var withStuff = world.CreateEntity();
            withStuff.Attach(new HealthAspect());
            withStuff.AddTag<PlayerTag>();

            Assert.IsTrue(q.Matches(bare));
            Assert.IsTrue(q.Matches(withStuff));

            world.Shutdown();
        }

        [Test]
        public void Query_MultipleAllOf_RequiresAllPresent()
        {
            var q = Query.Create()
                .WithAll<HealthAspect>()
                .WithAll<ArmorAspect>()
                .WithAll<WeaponAspect>()
                .Build();

            var world = new EntityWorld("Test");
            var partial = world.CreateEntity();
            partial.Attach(new HealthAspect());
            partial.Attach(new ArmorAspect());

            var complete = world.CreateEntity();
            complete.Attach(new HealthAspect());
            complete.Attach(new ArmorAspect());
            complete.Attach(new WeaponAspect());

            Assert.IsFalse(q.Matches(partial), "缺 WeaponAspect 不应匹配");
            Assert.IsTrue(q.Matches(complete));

            world.Shutdown();
        }

        [Test]
        public void Query_NoneOfWithMultipleTypes_AnyMatchRejects()
        {
            var q = Query.Create()
                .WithAll<HealthAspect>()
                .WithNone<PoisonAspect>()
                .Build();

            var world = new EntityWorld("Test");
            var clean = world.CreateEntity();
            clean.Attach(new HealthAspect());

            var poisoned = world.CreateEntity();
            poisoned.Attach(new HealthAspect());
            poisoned.Attach(new PoisonAspect());

            Assert.IsTrue(q.Matches(clean));
            Assert.IsFalse(q.Matches(poisoned), "带 PoisonAspect 应被排除");

            world.Shutdown();
        }
    }
}
