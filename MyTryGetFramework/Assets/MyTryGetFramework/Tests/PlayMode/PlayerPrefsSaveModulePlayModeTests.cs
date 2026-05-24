using NUnit.Framework;
using TryGet;
using TryGet.Unity;
using UnityEngine;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// PlayerPrefsSaveModule（ISaveModule Unity Adapter）的 PlayMode 测试。
    /// SetUp/TearDown 用 PlayerPrefs.DeleteAll 清理 PlayMode 共享存储。
    /// </summary>
    [TestFixture]
    public class PlayerPrefsSaveModulePlayModeTests
    {
        private const string TestKeyPrefix = "TryGet_Test_";

        [SetUp]
        public void SetUp()
        {
            // 清掉所有 PlayerPrefs（隔离测试）
            PlayerPrefs.DeleteAll();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void Priority_SameAsMemoryImpl()
        {
            var s = new PlayerPrefsSaveModule();
            Assert.AreEqual(-450, s.Priority);
        }

        [Test]
        public void SetGet_String_RoundTrip()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetString(TestKeyPrefix + "name", "Alice");
            Assert.AreEqual("Alice", s.GetString(TestKeyPrefix + "name"));
        }

        [Test]
        public void SetGet_Int_RoundTrip()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetInt(TestKeyPrefix + "score", 42);
            Assert.AreEqual(42, s.GetInt(TestKeyPrefix + "score"));
        }

        [Test]
        public void SetGet_Float_RoundTrip()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetFloat(TestKeyPrefix + "ratio", 0.75f);
            Assert.AreEqual(0.75f, s.GetFloat(TestKeyPrefix + "ratio"), 0.0001f);
        }

        [Test]
        public void SetGet_Bool_RoundTrip()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetBool(TestKeyPrefix + "muted", true);
            Assert.IsTrue(s.GetBool(TestKeyPrefix + "muted"));

            s.SetBool(TestKeyPrefix + "muted", false);
            Assert.IsFalse(s.GetBool(TestKeyPrefix + "muted"));
        }

        [Test]
        public void Get_Missing_ReturnsDefault()
        {
            var s = new PlayerPrefsSaveModule();
            Assert.AreEqual("fallback", s.GetString(TestKeyPrefix + "ghost", "fallback"));
            Assert.AreEqual(7, s.GetInt(TestKeyPrefix + "ghost", 7));
            Assert.AreEqual(3.14f, s.GetFloat(TestKeyPrefix + "ghost", 3.14f), 0.0001f);
            Assert.IsTrue(s.GetBool(TestKeyPrefix + "ghost", true));
        }

        [Test]
        public void HasKey_Boundary()
        {
            var s = new PlayerPrefsSaveModule();
            Assert.IsFalse(s.HasKey(TestKeyPrefix + "missing"));
            Assert.IsFalse(s.HasKey(null));
            Assert.IsFalse(s.HasKey(""));

            s.SetInt(TestKeyPrefix + "real", 1);
            Assert.IsTrue(s.HasKey(TestKeyPrefix + "real"));
        }

        [Test]
        public void Set_NullOrEmptyKey_Throws()
        {
            var s = new PlayerPrefsSaveModule();
            Assert.Throws<System.ArgumentException>(() => s.SetString("", "v"));
            Assert.Throws<System.ArgumentException>(() => s.SetInt("", 1));
            Assert.Throws<System.ArgumentException>(() => s.SetFloat("", 1f));
            Assert.Throws<System.ArgumentException>(() => s.SetBool("", true));
        }

        [Test]
        public void SetString_NullValue_Throws()
        {
            var s = new PlayerPrefsSaveModule();
            Assert.Throws<System.ArgumentNullException>(() => s.SetString(TestKeyPrefix + "x", null));
        }

        [Test]
        public void DeleteKey_Existing_RemovesAndReturnsTrue()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetInt(TestKeyPrefix + "x", 1);

            Assert.IsTrue(s.DeleteKey(TestKeyPrefix + "x"));
            Assert.IsFalse(s.HasKey(TestKeyPrefix + "x"));
        }

        [Test]
        public void DeleteKey_Missing_ReturnsFalse()
        {
            var s = new PlayerPrefsSaveModule();
            Assert.IsFalse(s.DeleteKey(TestKeyPrefix + "ghost"));
        }

        [Test]
        public void DeleteAll_RemovesEverything()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetString(TestKeyPrefix + "a", "1");
            s.SetInt(TestKeyPrefix + "b", 2);

            s.DeleteAll();

            Assert.IsFalse(s.HasKey(TestKeyPrefix + "a"));
            Assert.IsFalse(s.HasKey(TestKeyPrefix + "b"));
            Assert.AreEqual(0, s.KeyCount);
        }

        [Test]
        public void Save_NoExceptionAndPersists()
        {
            var s = new PlayerPrefsSaveModule();
            s.SetString(TestKeyPrefix + "persist", "data");

            Assert.DoesNotThrow(() => s.Save());

            // Save 后值仍存在（PlayerPrefs 行为：Save 是同步落盘，不擦内存）
            Assert.AreEqual("data", s.GetString(TestKeyPrefix + "persist"));
        }

        [Test]
        public void Shutdown_DoesNotErasePlayerPrefs()
        {
            // Adapter Shutdown 不应擦盘（持久化语义）
            var s = new PlayerPrefsSaveModule();
            s.SetString(TestKeyPrefix + "kept", "value");

            s.Shutdown();

            // 用第二个 instance 读，验证数据仍在
            var s2 = new PlayerPrefsSaveModule();
            Assert.AreEqual("value", s2.GetString(TestKeyPrefix + "kept"));
            Assert.IsTrue(s2.HasKey(TestKeyPrefix + "kept"));
        }

        [Test]
        public void IntegratesWithModuleHost()
        {
            var host = new ModuleHost();
            host.Register<ISaveModule>(new PlayerPrefsSaveModule());
            host.Initialize();

            host.Get<ISaveModule>().SetString(TestKeyPrefix + "user", "Bob");
            host.Get<ISaveModule>().Save();

            Assert.AreEqual("Bob", host.Get<ISaveModule>().GetString(TestKeyPrefix + "user"));

            host.Shutdown();
        }

        [Test]
        public void CrossTypeSet_Overwrites()
        {
            // 与 MemorySaveModule 行为对齐：同 key 跨类型覆盖
            var s = new PlayerPrefsSaveModule();
            s.SetInt(TestKeyPrefix + "x", 42);
            s.SetString(TestKeyPrefix + "x", "hello");

            Assert.AreEqual("hello", s.GetString(TestKeyPrefix + "x"));
            // PlayerPrefs.GetInt 在 key 已是 string 类型时返回 default
            Assert.AreEqual(0, s.GetInt(TestKeyPrefix + "x"));
        }
    }
}
