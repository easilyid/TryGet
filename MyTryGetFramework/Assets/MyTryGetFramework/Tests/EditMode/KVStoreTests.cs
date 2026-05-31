using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// IKVStore / MemoryKVStore 测试。
    /// </summary>
    [TestFixture]
    public class KVStoreTests
    {
        [Test]
        public void Memory_NewStore_EmptyKeys()
        {
            var kv = new MemoryKVStore();
            Assert.AreEqual(0, kv.Count);
            Assert.IsFalse(kv.ContainsKey("any"));
        }

        [Test]
        public void Memory_SetGet_String()
        {
            var kv = new MemoryKVStore();
            kv.Set("name", "tryget");
            Assert.AreEqual("tryget", kv.Get<string>("name"));
        }

        [Test]
        public void Memory_SetGet_CustomStruct()
        {
            var kv = new MemoryKVStore();
            kv.Set("vec", new Vec3 { X = 1, Y = 2, Z = 3 });
            var v = kv.Get<Vec3>("vec");
            Assert.AreEqual(1, v.X);
            Assert.AreEqual(2, v.Y);
            Assert.AreEqual(3, v.Z);
        }

        [Test]
        public void Memory_SetGet_ReferenceType()
        {
            var kv = new MemoryKVStore();
            var list = new List<int> { 1, 2, 3 };
            kv.Set("list", list);
            Assert.AreSame(list, kv.Get<List<int>>("list"));
        }

        [Test]
        public void Memory_TryGet_MissingKey_ReturnsFalse()
        {
            var kv = new MemoryKVStore();
            Assert.IsFalse(kv.TryGet<string>("missing", out var v));
            Assert.IsNull(v);
        }

        [Test]
        public void Memory_TryGet_WrongType_ReturnsFalse()
        {
            var kv = new MemoryKVStore();
            kv.Set("k", "string-val");
            Assert.IsFalse(kv.TryGet<int>("k", out _));
        }

        [Test]
        public void Memory_Get_MissingKey_Throws()
        {
            var kv = new MemoryKVStore();
            Assert.Throws<KeyNotFoundException>(() => kv.Get<string>("missing"));
        }

        [Test]
        public void Memory_Get_WrongType_Throws()
        {
            var kv = new MemoryKVStore();
            kv.Set("k", "string-val");
            Assert.Throws<InvalidCastException>(() => kv.Get<int>("k"));
        }

        [Test]
        public void Memory_Set_OverwriteType_Allowed()
        {
            var kv = new MemoryKVStore();
            kv.Set("k", 42);
            kv.Set("k", "now-string");
            Assert.AreEqual("now-string", kv.Get<string>("k"));
            Assert.AreEqual(1, kv.Count);
        }

        [Test]
        public void Memory_Set_NullReference_Allowed()
        {
            var kv = new MemoryKVStore();
            kv.Set<string>("k", null);
            Assert.IsTrue(kv.TryGet<string>("k", out var v));
            Assert.IsNull(v);
        }

        [Test]
        public void Memory_Set_EmptyKey_Throws()
        {
            var kv = new MemoryKVStore();
            Assert.Throws<ArgumentException>(() => kv.Set("", "val"));
            Assert.Throws<ArgumentException>(() => kv.Set<string>(null, "val"));
        }

        [Test]
        public void Memory_Remove_ExistingKey_ReturnsTrue()
        {
            var kv = new MemoryKVStore();
            kv.Set("k", 1);
            Assert.IsTrue(kv.Remove("k"));
            Assert.IsFalse(kv.ContainsKey("k"));
        }

        [Test]
        public void Memory_Clear_RemovesAll()
        {
            var kv = new MemoryKVStore();
            kv.Set("a", 1);
            kv.Set("b", 2);
            kv.Set("c", 3);
            Assert.AreEqual(3, kv.Count);

            kv.Clear();
            Assert.AreEqual(0, kv.Count);
        }

        [Test]
        public void Memory_Keys_ReturnsAllSetKeys()
        {
            var kv = new MemoryKVStore();
            kv.Set("a", 1);
            kv.Set("b", 2);

            var keys = new List<string>(kv.Keys);
            keys.Sort();
            Assert.AreEqual(new[] { "a", "b" }, keys.ToArray());
        }

        [Test]
        public void Memory_ModuleSystem_Integration()
        {
            var host = new ModuleSystem();
            host.Register<IKVStore>(new MemoryKVStore());
            host.Initialize();

            var kv = host.Get<IKVStore>();
            kv.Set("score", 100);
            Assert.AreEqual(100, kv.Get<int>("score"));

            host.Shutdown();
        }

        private struct Vec3 { public float X, Y, Z; }
    }
}
