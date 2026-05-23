using System;
using System.Diagnostics;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.3 迭代 1 加固（迭代 1.5）：Plan agent review 必改项 + 建议项的回归测试。
    /// </summary>
    [TestFixture]
    public class V03MustFixTests
    {
        // —— 必改 1：Entity.Attach OnAttach 异常回滚 ——

        private class ThrowingAttachAspect : Aspect
        {
            protected internal override void OnAttach() => throw new InvalidOperationException("attach boom");
        }

        [Test]
        public void Attach_OnAttachThrows_StateIsFullyRolledBack()
        {
            var world = new EntityWorld("Test");
            var e = world.CreateEntity();
            var a = new ThrowingAttachAspect();

            Assert.Throws<InvalidOperationException>(() => e.Attach(a));

            Assert.IsFalse(e.HasAspect<ThrowingAttachAspect>(), "回滚：HasAspect 应返回 false");
            Assert.AreEqual(0, e.AspectCount, "回滚：AspectCount 应为 0");

            // 关键：mask 也应回滚——通过 Query.Matches 间接验证
            var q = Query.Create().WithAll<ThrowingAttachAspect>().Build();
            Assert.IsFalse(q.Matches(e), "回滚：mask 应不含 ThrowingAttachAspect 位");

            world.Shutdown();
        }

        // —— 必改 2：Entity.Detach OnDetach 异常回滚 ——

        private class ThrowingDetachAspect : Aspect
        {
            protected internal override void OnDetach() => throw new InvalidOperationException("detach boom");
        }

        [Test]
        public void Detach_OnDetachThrows_DictAndMaskRolledBack()
        {
            var world = new EntityWorld("Test");
            var e = world.CreateEntity();
            var a = new ThrowingDetachAspect();
            e.Attach(a);

            Assert.IsTrue(e.HasAspect<ThrowingDetachAspect>(), "Attach 后存在");

            Assert.Throws<InvalidOperationException>(() => e.Detach(a));

            Assert.IsTrue(e.HasAspect<ThrowingDetachAspect>(), "Detach 失败：HasAspect 应仍 true（dict 已回滚）");
            Assert.AreEqual(1, e.AspectCount);

            var q = Query.Create().WithAll<ThrowingDetachAspect>().Build();
            Assert.IsTrue(q.Matches(e), "Detach 失败：mask 应仍含此 Aspect 位");

            world.Shutdown();
        }

        // —— 必改 3：TypeRegistry 并发安全 ——

        [Test]
        public void TypeRegistry_ConcurrentGetOrAllocate_NoIndexCollision()
        {
            // 多线程同时分配不同类型，验证 _next++ 串行化（不出现两类型拿同 index）
            TypeRegistry.ResetForTests();

            var types = new Type[]
            {
                typeof(ThrowingAttachAspect), typeof(ThrowingDetachAspect),
                typeof(int), typeof(long), typeof(float), typeof(double),
                typeof(byte), typeof(short)
            };
            var results = new int[types.Length];

            var threads = new System.Threading.Thread[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                int idx = i;
                threads[i] = new System.Threading.Thread(() =>
                {
                    results[idx] = TypeRegistry.GetOrAllocate(types[idx]);
                });
                threads[i].Start();
            }
            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();

            // 所有 index 应两两不同
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var v in results)
                Assert.IsTrue(seen.Add(v), $"index {v} 被多个类型共享（碰撞）");
        }

        // —— 建议 1：Builder 多次 Build 返回独立 Query ——

        private class FooAspect : Aspect { }
        private class BarAspect : Aspect { }

        [Test]
        public void Builder_BuildTwice_ReturnsIndependentQueries()
        {
            var builder = Query.Create().WithAll<FooAspect>();
            var q1 = builder.Build();

            // 在 q1 build 之后继续修改 builder：q2 应包含新加的位，q1 不受影响
            builder.WithAll<BarAspect>();
            var q2 = builder.Build();

            var world = new EntityWorld("Test");
            var fooOnly = world.CreateEntity();
            fooOnly.Attach(new FooAspect());

            var both = world.CreateEntity();
            both.Attach(new FooAspect());
            both.Attach(new BarAspect());

            Assert.IsTrue(q1.Matches(fooOnly), "q1 只要 Foo，应匹配 fooOnly");
            Assert.IsTrue(q1.Matches(both),    "q1 只要 Foo，both 也匹配");
            Assert.IsFalse(q2.Matches(fooOnly), "q2 要 Foo+Bar，fooOnly 不匹配");
            Assert.IsTrue(q2.Matches(both),    "q2 要 Foo+Bar，both 匹配");

            world.Shutdown();
        }

        // —— 建议 2：BitArray256 跨 bucket 边界 ——

        [Test]
        public void BitArray256_ContainsAll_AcrossNonContiguousBuckets()
        {
            // a 在 bucket 0 + bucket 3 都有位；b 在 bucket 1 有位（不交集）
            var a = new BitArray256();
            a.Add(5);    // bucket 0
            a.Add(200);  // bucket 3

            var b = new BitArray256();
            b.Add(100);  // bucket 1

            Assert.IsFalse(a.ContainsAll(b), "a 缺 bucket 1 位，不应 contain b");
            Assert.IsFalse(a.ContainsAny(b));

            // a 包含 b 子集（仅 b 子集 bit）
            var bSubset = new BitArray256();
            bSubset.Add(5);
            Assert.IsTrue(a.ContainsAll(bSubset));
            Assert.IsTrue(a.ContainsAny(bSubset));
        }

        // —— 建议 3：TypeRegistry ResetForTests 行为 ——

        [Test]
        public void TypeRegistry_ResetForTests_RestoresInitialState()
        {
            TypeRegistry.ResetForTests();
            int before = TypeRegistry.AllocatedCount;
            Assert.AreEqual(0, before);

            TypeRegistry.GetOrAllocate(typeof(FooAspect));
            TypeRegistry.GetOrAllocate(typeof(BarAspect));
            Assert.AreEqual(2, TypeRegistry.AllocatedCount);

            TypeRegistry.ResetForTests();
            Assert.AreEqual(0, TypeRegistry.AllocatedCount, "ResetForTests 后 _next 归零");

            // 重新分配，应从 0 开始
            int idx = TypeRegistry.GetOrAllocate(typeof(FooAspect));
            Assert.AreEqual(0, idx, "重置后第一个分配 index=0");
        }

        // —— 建议 4：TypeIndexOverflowException 路径 ——

        [Test]
        public void TypeRegistry_OverflowAt256_ThrowsTypedException()
        {
            TypeRegistry.ResetForTests();

            // 手动塞满 256 个 type slot（用动态 type 类似 generic 比较麻烦，
            // 改用反射构建假 type token——不可，所以用循环 + 不同 generic）
            // 简单做法：构造 256 个不同的 Generic 类型实例
            for (int i = 0; i < BitArray256.Capacity; i++)
            {
                // 用 Tuple<int> + 不同 dimension 来生成不同 Type
                // 实际上无法在测试期间生成 256 个不同 Type 而不依赖于 reflection
                // 改用直接调用 256 次，每次用 typeof(int[i 维数组])
                // 这里用最简化的：用 Generic + Reflection 创建
                Type t = typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(int));
                if (i == 0) { TypeRegistry.GetOrAllocate(t); continue; }
                // 后续我们用 jaggedArrayOf depth = i 来产生不同 type
                Type variant = typeof(int);
                for (int j = 0; j < i; j++)
                    variant = variant.MakeArrayType();
                TypeRegistry.GetOrAllocate(variant);
            }

            // 现在已经 256 个，再加一个应抛
            Type extra = typeof(string);
            Assert.Throws<TypeIndexOverflowException>(() => TypeRegistry.GetOrAllocate(extra));

            TypeRegistry.ResetForTests(); // 清理，避免影响后续测试
        }

        // —— 建议 7：Benchmark 数字落地 ——

        private class HealthAspect : Aspect { public int HP; }
        private class ArmorAspect : Aspect { public int Defense; }
        private class WeaponAspect : Aspect { public int Damage; }

        [Test]
        public void Benchmark_10000Entity_5Query_Matches_RuntimeIsBounded()
        {
            // 10000 Entity 创建 + warm-up
            var world = new EntityWorld("Bench");
            var entities = new System.Collections.Generic.List<Entity>(10000);
            for (int i = 0; i < 10000; i++)
            {
                var e = world.CreateEntity();
                if ((i & 1) == 0) e.Attach(new HealthAspect());
                if ((i & 2) == 0) e.Attach(new ArmorAspect());
                if ((i & 4) == 0) e.Attach(new WeaponAspect());
                entities.Add(e);
            }

            var queries = new Query[]
            {
                Query.Create().WithAll<HealthAspect>().Build(),
                Query.Create().WithAll<HealthAspect>().WithAll<ArmorAspect>().Build(),
                Query.Create().WithAll<HealthAspect>().WithNone<WeaponAspect>().Build(),
                Query.Create().WithAll<ArmorAspect>().WithNone<HealthAspect>().Build(),
                Query.Create().WithAll<WeaponAspect>().Build(),
            };

            // Warmup：JIT
            int warm = 0;
            for (int q = 0; q < queries.Length; q++)
                foreach (var e in entities)
                    if (queries[q].Matches(e)) warm++;

            // 计时：10000 × 5 × 10 帧 = 500,000 次 Matches
            var sw = Stopwatch.StartNew();
            int matches = 0;
            for (int frame = 0; frame < 10; frame++)
            {
                for (int q = 0; q < queries.Length; q++)
                {
                    foreach (var e in entities)
                    {
                        if (queries[q].Matches(e)) matches++;
                    }
                }
            }
            sw.Stop();

            // 输出 baseline 数字。位运算路径在普通 dev box 上预期 < 50 ms。
            Console.WriteLine($"[Benchmark] 10000 × 5 × 10 frames = 500000 Matches in {sw.ElapsedMilliseconds} ms ({matches} hits)");

            // 弱断言：不超过 1 秒（防 regression 引入 O(n) 退化）
            Assert.Less(sw.ElapsedMilliseconds, 1000, "BitArray256 路径 500k Matches 应远低于 1s");

            world.Shutdown();
        }
    }
}
