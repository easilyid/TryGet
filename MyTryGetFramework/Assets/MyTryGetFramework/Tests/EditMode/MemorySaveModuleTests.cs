using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemorySaveModule（ISaveModule 内存实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemorySaveModuleTests
    {
        [Test]
        public void Priority_BetweenPoolAndResource()
        {
            var s = new MemorySaveModule();
            Assert.AreEqual(-450, s.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var s = new MemorySaveModule();
            Assert.AreEqual(0, s.DependsOn.Count);
        }

        // —— HasKey ——

        [Test]
        public void HasKey_Existing_True()
        {
            var s = new MemorySaveModule();
            s.SetString("name", "Alice");
            Assert.IsTrue(s.HasKey("name"));
        }

        [Test]
        public void HasKey_NonExisting_False()
        {
            var s = new MemorySaveModule();
            Assert.IsFalse(s.HasKey("missing"));
        }

        [Test]
        public void HasKey_NullOrEmpty_False()
        {
            var s = new MemorySaveModule();
            Assert.IsFalse(s.HasKey(null));
            Assert.IsFalse(s.HasKey(""));
        }

        // —— Get 成功路径 ——

        [Test]
        public void SetGet_String_RoundTrip()
        {
            var s = new MemorySaveModule();
            s.SetString("name", "Alice");
            Assert.AreEqual("Alice", s.GetString("name"));
        }

        [Test]
        public void SetGet_Int_RoundTrip()
        {
            var s = new MemorySaveModule();
            s.SetInt("level", 42);
            Assert.AreEqual(42, s.GetInt("level"));
        }

        [Test]
        public void SetGet_Float_RoundTrip()
        {
            var s = new MemorySaveModule();
            s.SetFloat("hp", 99.5f);
            Assert.AreEqual(99.5f, s.GetFloat("hp"));
        }

        [Test]
        public void SetGet_Bool_RoundTrip()
        {
            var s = new MemorySaveModule();
            s.SetBool("muted", true);
            Assert.IsTrue(s.GetBool("muted"));
        }

        // —— Get 默认值（不存在 / 类型不匹配）——

        [Test]
        public void Get_Missing_ReturnsDefault()
        {
            var s = new MemorySaveModule();
            Assert.AreEqual("fallback", s.GetString("x", "fallback"));
            Assert.AreEqual(7, s.GetInt("x", 7));
            Assert.AreEqual(3.14f, s.GetFloat("x", 3.14f));
            Assert.IsTrue(s.GetBool("x", true));
        }

        [Test]
        public void Get_WrongType_ReturnsDefault()
        {
            var s = new MemorySaveModule();
            s.SetInt("x", 42);
            // GetString("x") 类型不匹配 → 返回 default
            Assert.AreEqual("D", s.GetString("x", "D"));
            Assert.AreEqual(0f, s.GetFloat("x"));
            Assert.IsFalse(s.GetBool("x"));
        }

        [Test]
        public void Get_NullKey_ReturnsDefault()
        {
            var s = new MemorySaveModule();
            Assert.AreEqual("D", s.GetString(null, "D"));
            Assert.AreEqual(9, s.GetInt(null, 9));
            Assert.AreEqual(1.5f, s.GetFloat(null, 1.5f));
            Assert.IsTrue(s.GetBool(null, true));
        }

        // —— Set 边界 ——

        [Test]
        public void Set_NullOrEmptyKey_Throws()
        {
            var s = new MemorySaveModule();
            Assert.Throws<ArgumentException>(() => s.SetString(null, "v"));
            Assert.Throws<ArgumentException>(() => s.SetString("", "v"));
            Assert.Throws<ArgumentException>(() => s.SetInt("", 1));
            Assert.Throws<ArgumentException>(() => s.SetFloat("", 1f));
            Assert.Throws<ArgumentException>(() => s.SetBool("", true));
        }

        [Test]
        public void SetString_NullValue_Throws()
        {
            var s = new MemorySaveModule();
            Assert.Throws<ArgumentNullException>(() => s.SetString("x", null));
        }

        // —— 跨类型覆盖 ——

        [Test]
        public void Set_SameKey_DifferentType_Overwrites()
        {
            var s = new MemorySaveModule();
            s.SetInt("x", 42);
            s.SetString("x", "hello");

            // 原 Int 已被 String 覆盖
            Assert.AreEqual("hello", s.GetString("x"));
            Assert.AreEqual(0, s.GetInt("x")); // 类型变了，GetInt 返回 default
            Assert.AreEqual(1, s.KeyCount, "覆盖后仍是单 key");
        }

        [Test]
        public void Set_SameKey_SameType_Overwrites()
        {
            var s = new MemorySaveModule();
            s.SetInt("x", 1);
            s.SetInt("x", 2);
            Assert.AreEqual(2, s.GetInt("x"));
            Assert.AreEqual(1, s.KeyCount);
        }

        // —— DeleteKey ——

        [Test]
        public void DeleteKey_Existing_ReturnsTrueAndRemoves()
        {
            var s = new MemorySaveModule();
            s.SetString("x", "v");
            Assert.IsTrue(s.DeleteKey("x"));
            Assert.IsFalse(s.HasKey("x"));
            Assert.AreEqual(0, s.KeyCount);
        }

        [Test]
        public void DeleteKey_Missing_ReturnsFalse()
        {
            var s = new MemorySaveModule();
            Assert.IsFalse(s.DeleteKey("x"));
        }

        [Test]
        public void DeleteKey_NullOrEmpty_ReturnsFalse()
        {
            var s = new MemorySaveModule();
            Assert.IsFalse(s.DeleteKey(null));
            Assert.IsFalse(s.DeleteKey(""));
        }

        // —— DeleteAll ——

        [Test]
        public void DeleteAll_RemovesEverything()
        {
            var s = new MemorySaveModule();
            s.SetString("a", "1");
            s.SetInt("b", 2);
            s.SetFloat("c", 3f);
            s.SetBool("d", true);
            Assert.AreEqual(4, s.KeyCount);

            s.DeleteAll();

            Assert.AreEqual(0, s.KeyCount);
            Assert.IsFalse(s.HasKey("a"));
            Assert.IsFalse(s.HasKey("d"));
        }

        // —— Save no-op ——

        [Test]
        public void Save_IsNoOp_DataPreserved()
        {
            var s = new MemorySaveModule();
            s.SetString("x", "v");
            s.Save(); // Memory 实现 no-op，不应改变状态
            Assert.AreEqual("v", s.GetString("x"));
            Assert.AreEqual(1, s.KeyCount);
        }

        // —— Shutdown ——

        [Test]
        public void Shutdown_ClearsAllData()
        {
            var s = new MemorySaveModule();
            s.SetString("a", "1");
            s.SetInt("b", 2);

            s.Shutdown();

            Assert.AreEqual(0, s.KeyCount);
        }

        // —— KeyCount 准确性 ——

        [Test]
        public void KeyCount_MultiTypeKeys_AggregateCorrectly()
        {
            var s = new MemorySaveModule();
            s.SetString("a", "1");
            s.SetInt("b", 2);
            s.SetFloat("c", 3f);
            s.SetBool("d", true);
            Assert.AreEqual(4, s.KeyCount);

            s.DeleteKey("b");
            Assert.AreEqual(3, s.KeyCount);
        }

        // —— ModuleHost 集成 ——

        [Test]
        public void IntegratesWithModuleHost_BusinessCanReadSave()
        {
            var host = new ModuleHost();
            var save = new MemorySaveModule();
            host.Register<ISaveModule>(save);
            host.Initialize();

            host.Get<ISaveModule>().SetString("lastUser", "Alice");
            host.Get<ISaveModule>().Save();

            Assert.AreEqual("Alice", host.Get<ISaveModule>().GetString("lastUser"));

            host.Shutdown();

            // Shutdown 后数据应清空（Memory 行为；Adapter 可能不清而是落盘）
            Assert.AreEqual(0, save.KeyCount);
        }

        [Test]
        public void CoexistsWithOtherModules_DependencyOrderImplicit()
        {
            // Save (-450) → Resource (-400) → UI (-300) Priority 链验证
            var host = new ModuleHost();
            host.Register<ISaveModule>(new MemorySaveModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IUIModule>(new MemoryUIModule());
            host.Initialize();

            Assert.NotNull(host.Get<ISaveModule>());
            Assert.NotNull(host.Get<IResourceModule>());
            Assert.NotNull(host.Get<IUIModule>());

            host.Shutdown();
        }
    }
}
