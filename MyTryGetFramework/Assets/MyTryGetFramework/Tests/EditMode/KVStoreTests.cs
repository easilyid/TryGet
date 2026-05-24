using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.8 Iter 2 — IKVStore / MemoryKVStore / SaveModuleAdapter 测试。
    /// </summary>
    [TestFixture]
    public class KVStoreTests
    {
        // ===== MemoryKVStore =====

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
        public void Memory_ModuleHost_Integration()
        {
            var host = new ModuleHost();
            host.Register<IKVStore>(new MemoryKVStore());
            host.Initialize();

            var kv = host.Get<IKVStore>();
            kv.Set("score", 100);
            Assert.AreEqual(100, kv.Get<int>("score"));

            host.Shutdown();
        }

        // ===== SaveModuleAdapter =====

        [Test]
        public void Adapter_BridgesStringPath()
        {
            #pragma warning disable CS0618
            var legacy = new MemorySaveModule();
            var adapter = new SaveModuleAdapter(legacy);
            #pragma warning restore CS0618

            adapter.Set("k", "v");
            Assert.AreEqual("v", adapter.Get<string>("k"));
        }

        [Test]
        public void Adapter_BridgesIntFloatBool()
        {
            #pragma warning disable CS0618
            var legacy = new MemorySaveModule();
            var adapter = new SaveModuleAdapter(legacy);
            #pragma warning restore CS0618

            adapter.Set("i", 42);
            adapter.Set("f", 3.14f);
            adapter.Set("b", true);

            Assert.AreEqual(42, adapter.Get<int>("i"));
            Assert.AreEqual(3.14f, adapter.Get<float>("f"), 0.0001f);
            Assert.IsTrue(adapter.Get<bool>("b"));
        }

        [Test]
        public void Adapter_UnsupportedType_Throws()
        {
            #pragma warning disable CS0618
            var legacy = new MemorySaveModule();
            var adapter = new SaveModuleAdapter(legacy);
            #pragma warning restore CS0618

            Assert.Throws<NotSupportedException>(() => adapter.Set("k", new List<int>()));
        }

        [Test]
        public void Adapter_KeysAndCount_ReflectSetCalls()
        {
            #pragma warning disable CS0618
            var legacy = new MemorySaveModule();
            var adapter = new SaveModuleAdapter(legacy);
            #pragma warning restore CS0618

            adapter.Set("a", 1);
            adapter.Set("b", "s");
            adapter.Set("c", true);

            Assert.AreEqual(3, adapter.Count);
            Assert.IsTrue(((List<string>)new List<string>(adapter.Keys)).Contains("a"));
        }

        [Test]
        public void Adapter_Remove_UpdatesBothStores()
        {
            #pragma warning disable CS0618
            var legacy = new MemorySaveModule();
            var adapter = new SaveModuleAdapter(legacy);
            #pragma warning restore CS0618

            adapter.Set("k", 1);
            Assert.IsTrue(adapter.Remove("k"));
            Assert.IsFalse(adapter.ContainsKey("k"));
            #pragma warning disable CS0618
            Assert.IsFalse(legacy.HasKey("k"));
            #pragma warning restore CS0618
        }

        [Test]
        public void Adapter_NullInner_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveModuleAdapter(null));
        }

        // ===== helpers =====

        private struct Vec3 { public float X, Y, Z; }
    }
}
