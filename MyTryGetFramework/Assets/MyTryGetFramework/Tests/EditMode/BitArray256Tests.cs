using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// BitArray256（V0.3 类型索引位数组）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class BitArray256Tests
    {
        [Test]
        public void Default_IsEmpty()
        {
            var b = new BitArray256();
            Assert.IsTrue(b.IsEmpty);
            Assert.AreEqual(0, b.PopCount());
        }

        [Test]
        public void Add_SetsBit()
        {
            var b = new BitArray256();
            b.Add(5);
            Assert.IsTrue(b.Contains(5));
            Assert.IsFalse(b.Contains(6));
            Assert.AreEqual(1, b.PopCount());
        }

        [Test]
        public void Add_AcrossAll4Buckets()
        {
            var b = new BitArray256();
            b.Add(0);    // bucket 0
            b.Add(63);   // bucket 0 边界
            b.Add(64);   // bucket 1 起点
            b.Add(128);  // bucket 2 起点
            b.Add(192);  // bucket 3 起点
            b.Add(255);  // bucket 3 边界

            Assert.IsTrue(b.Contains(0));
            Assert.IsTrue(b.Contains(63));
            Assert.IsTrue(b.Contains(64));
            Assert.IsTrue(b.Contains(128));
            Assert.IsTrue(b.Contains(192));
            Assert.IsTrue(b.Contains(255));
            Assert.AreEqual(6, b.PopCount());
        }

        [Test]
        public void Remove_ClearsBit()
        {
            var b = new BitArray256();
            b.Add(10);
            b.Remove(10);
            Assert.IsFalse(b.Contains(10));
            Assert.IsTrue(b.IsEmpty);
        }

        [Test]
        public void Add_Idempotent()
        {
            var b = new BitArray256();
            b.Add(7);
            b.Add(7);
            Assert.AreEqual(1, b.PopCount());
        }

        [Test]
        public void IndexOutOfRange_Throws()
        {
            var b = new BitArray256();
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Add(256));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Contains(256));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Remove(-1));
        }

        [Test]
        public void ContainsAll_StrictSuperset_True()
        {
            var a = new BitArray256();
            a.Add(1); a.Add(2); a.Add(3);

            var b = new BitArray256();
            b.Add(1); b.Add(2);

            Assert.IsTrue(a.ContainsAll(b));
        }

        [Test]
        public void ContainsAll_MissingOneBit_False()
        {
            var a = new BitArray256();
            a.Add(1); a.Add(2);

            var b = new BitArray256();
            b.Add(1); b.Add(2); b.Add(3);

            Assert.IsFalse(a.ContainsAll(b));
        }

        [Test]
        public void ContainsAll_EmptyOther_AlwaysTrue()
        {
            var a = new BitArray256();
            a.Add(5);
            Assert.IsTrue(a.ContainsAll(new BitArray256()), "包含空集是 trivially true");
        }

        [Test]
        public void ContainsAny_OverlapsOnOneBit_True()
        {
            var a = new BitArray256();
            a.Add(10); a.Add(20);

            var b = new BitArray256();
            b.Add(20); b.Add(100);

            Assert.IsTrue(a.ContainsAny(b));
        }

        [Test]
        public void ContainsAny_DisjointSets_False()
        {
            var a = new BitArray256();
            a.Add(1); a.Add(2);

            var b = new BitArray256();
            b.Add(100); b.Add(200);

            Assert.IsFalse(a.ContainsAny(b));
        }

        [Test]
        public void ContainsAny_EmptyOther_False()
        {
            var a = new BitArray256();
            a.Add(5);
            Assert.IsFalse(a.ContainsAny(new BitArray256()));
        }

        [Test]
        public void Clear_RemovesAllBits()
        {
            var b = new BitArray256();
            b.Add(0); b.Add(64); b.Add(128); b.Add(255);
            b.Clear();

            Assert.IsTrue(b.IsEmpty);
            Assert.AreEqual(0, b.PopCount());
        }

        [Test]
        public void Equals_SameBits_True()
        {
            var a = new BitArray256();
            a.Add(7); a.Add(77); a.Add(177);

            var b = new BitArray256();
            b.Add(7); b.Add(77); b.Add(177);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentBits_False()
        {
            var a = new BitArray256();
            a.Add(1);

            var b = new BitArray256();
            b.Add(2);

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void StructSemantics_CopyIsIndependent()
        {
            var a = new BitArray256();
            a.Add(5);
            var b = a; // struct copy
            b.Add(10);

            Assert.IsTrue(a.Contains(5));
            Assert.IsFalse(a.Contains(10), "struct 拷贝后修改不影响源");
            Assert.IsTrue(b.Contains(5));
            Assert.IsTrue(b.Contains(10));
        }
    }

    [TestFixture]
    public class TypeIndexTests
    {
        private class SomeAspectA : Aspect { }
        private class SomeAspectB : Aspect { }
        private class SomeTagC : Tag { }

        [Test]
        public void TypeIndex_SameType_ReturnsSameIndex()
        {
            int idx1 = TypeIndex<SomeAspectA>.Index;
            int idx2 = TypeIndex<SomeAspectA>.Index;
            Assert.AreEqual(idx1, idx2);
        }

        [Test]
        public void TypeIndex_DifferentTypes_ReturnDifferentIndices()
        {
            int idxA = TypeIndex<SomeAspectA>.Index;
            int idxB = TypeIndex<SomeAspectB>.Index;
            int idxC = TypeIndex<SomeTagC>.Index;
            Assert.AreNotEqual(idxA, idxB);
            Assert.AreNotEqual(idxA, idxC);
            Assert.AreNotEqual(idxB, idxC);
        }

        [Test]
        public void TypeIndex_AllInRange0To255()
        {
            int idxA = TypeIndex<SomeAspectA>.Index;
            int idxB = TypeIndex<SomeAspectB>.Index;
            int idxC = TypeIndex<SomeTagC>.Index;
            Assert.That(idxA, Is.InRange(0, BitArray256.Capacity - 1));
            Assert.That(idxB, Is.InRange(0, BitArray256.Capacity - 1));
            Assert.That(idxC, Is.InRange(0, BitArray256.Capacity - 1));
        }

        [Test]
        public void TypeRegistry_GetOrAllocate_NullType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TypeRegistry.GetOrAllocate(null));
        }

        [Test]
        public void TypeRegistry_TryGet_KnownType_ReturnsTrue()
        {
            int allocated = TypeIndex<SomeAspectA>.Index;
            bool found = TypeRegistry.TryGet(typeof(SomeAspectA), out int idx);
            Assert.IsTrue(found);
            Assert.AreEqual(allocated, idx);
        }

        [Test]
        public void TypeRegistry_TryGet_UnknownType_ReturnsFalse()
        {
            // 用一个未使用过 TypeIndex<T> 的类型
            bool found = TypeRegistry.TryGet(typeof(string), out _);
            Assert.IsFalse(found);
        }
    }
}
