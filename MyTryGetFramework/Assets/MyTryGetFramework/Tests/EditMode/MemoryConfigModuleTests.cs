using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemoryConfigModule（IConfigModule 实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemoryConfigModuleTests
    {
        private class WeaponConfig
        {
            public int Atk;
            public string Name;
        }

        private class MonsterConfig
        {
            public int Hp;
        }

        [Test]
        public void Priority_BetweenPoolAndSave()
        {
            var c = new MemoryConfigModule();
            Assert.AreEqual(-460, c.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var c = new MemoryConfigModule();
            Assert.AreEqual(0, c.DependsOn.Count);
        }

        // —— Register ——

        [Test]
        public void Register_NewKey_Stores()
        {
            var c = new MemoryConfigModule();
            c.Register("weapon.sword", new WeaponConfig { Atk = 100, Name = "Sword" });

            Assert.AreEqual(1, c.RegisteredCount);
            Assert.IsTrue(c.Has("weapon.sword"));
        }

        [Test]
        public void Register_NullKey_Throws()
        {
            var c = new MemoryConfigModule();
            Assert.Throws<ArgumentException>(() => c.Register(null, new WeaponConfig()));
            Assert.Throws<ArgumentException>(() => c.Register("", new WeaponConfig()));
        }

        [Test]
        public void Register_NullConfig_Throws()
        {
            var c = new MemoryConfigModule();
            Assert.Throws<ArgumentNullException>(() => c.Register<WeaponConfig>("k", null));
        }

        [Test]
        public void Register_DuplicateKey_Throws()
        {
            var c = new MemoryConfigModule();
            c.Register("k", new WeaponConfig());
            Assert.Throws<InvalidOperationException>(() => c.Register("k", new WeaponConfig()));
        }

        // —— Get ——

        [Test]
        public void Get_Registered_ReturnsConfig()
        {
            var c = new MemoryConfigModule();
            var w = new WeaponConfig { Atk = 100 };
            c.Register("weapon.sword", w);

            var got = c.Get<WeaponConfig>("weapon.sword");
            Assert.AreSame(w, got);
            Assert.AreEqual(100, got.Atk);
        }

        [Test]
        public void Get_Unregistered_ThrowsConfigNotFound()
        {
            var c = new MemoryConfigModule();
            var ex = Assert.Throws<ConfigNotFoundException>(() => c.Get<WeaponConfig>("missing"));
            Assert.AreEqual("missing", ex.Key);
        }

        [Test]
        public void Get_WrongType_ThrowsInvalidOperation()
        {
            var c = new MemoryConfigModule();
            c.Register("k", new WeaponConfig());

            var ex = Assert.Throws<InvalidOperationException>(() => c.Get<MonsterConfig>("k"));
            StringAssert.Contains("WeaponConfig", ex.Message);
            StringAssert.Contains("MonsterConfig", ex.Message);
        }

        [Test]
        public void Get_NullKey_Throws()
        {
            var c = new MemoryConfigModule();
            Assert.Throws<ArgumentException>(() => c.Get<WeaponConfig>(null));
            Assert.Throws<ArgumentException>(() => c.Get<WeaponConfig>(""));
        }

        // —— TryGet ——

        [Test]
        public void TryGet_Registered_ReturnsTrue()
        {
            var c = new MemoryConfigModule();
            var w = new WeaponConfig();
            c.Register("k", w);

            Assert.IsTrue(c.TryGet<WeaponConfig>("k", out var got));
            Assert.AreSame(w, got);
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalse()
        {
            var c = new MemoryConfigModule();
            Assert.IsFalse(c.TryGet<WeaponConfig>("missing", out var got));
            Assert.IsNull(got);
        }

        [Test]
        public void TryGet_WrongType_ReturnsFalse()
        {
            var c = new MemoryConfigModule();
            c.Register("k", new WeaponConfig());

            Assert.IsFalse(c.TryGet<MonsterConfig>("k", out var got));
            Assert.IsNull(got);
        }

        [Test]
        public void TryGet_NullKey_ReturnsFalse()
        {
            var c = new MemoryConfigModule();
            Assert.IsFalse(c.TryGet<WeaponConfig>(null, out var got));
            Assert.IsNull(got);
        }

        // —— Has ——

        [Test]
        public void Has_Boundary()
        {
            var c = new MemoryConfigModule();
            Assert.IsFalse(c.Has(null));
            Assert.IsFalse(c.Has(""));
            Assert.IsFalse(c.Has("missing"));

            c.Register("k", new WeaponConfig());
            Assert.IsTrue(c.Has("k"));
        }

        // —— Unregister ——

        [Test]
        public void Unregister_Existing_ReturnsTrueAndRemoves()
        {
            var c = new MemoryConfigModule();
            c.Register("k", new WeaponConfig());

            Assert.IsTrue(c.Unregister("k"));
            Assert.IsFalse(c.Has("k"));
            Assert.AreEqual(0, c.RegisteredCount);
        }

        [Test]
        public void Unregister_Missing_ReturnsFalse()
        {
            var c = new MemoryConfigModule();
            Assert.IsFalse(c.Unregister("missing"));
        }

        [Test]
        public void Unregister_NullOrEmpty_ReturnsFalse()
        {
            var c = new MemoryConfigModule();
            Assert.IsFalse(c.Unregister(null));
            Assert.IsFalse(c.Unregister(""));
        }

        // —— 多类型共存 ——

        [Test]
        public void MultipleTypesByKey_CanCoexist()
        {
            var c = new MemoryConfigModule();
            c.Register("weapon.sword", new WeaponConfig { Atk = 100 });
            c.Register("monster.goblin", new MonsterConfig { Hp = 50 });

            Assert.AreEqual(2, c.RegisteredCount);
            Assert.AreEqual(100, c.Get<WeaponConfig>("weapon.sword").Atk);
            Assert.AreEqual(50, c.Get<MonsterConfig>("monster.goblin").Hp);
        }

        // —— Shutdown ——

        [Test]
        public void Shutdown_ClearsAllConfigs()
        {
            var c = new MemoryConfigModule();
            c.Register("a", new WeaponConfig());
            c.Register("b", new MonsterConfig());

            c.Shutdown();

            Assert.AreEqual(0, c.RegisteredCount);
        }

        // —— ModuleHost 集成 ——

        [Test]
        public void IntegratesWithModuleHost_BusinessReadsConfig()
        {
            var host = new ModuleHost();
            host.Register<IConfigModule>(new MemoryConfigModule());
            host.Initialize();

            // 模拟"Adapter 启动时批量注册"
            var c = host.Get<IConfigModule>();
            c.Register("weapon.sword", new WeaponConfig { Atk = 100, Name = "Sword" });

            // 业务运行时查询
            var w = host.Get<IConfigModule>().Get<WeaponConfig>("weapon.sword");
            Assert.AreEqual("Sword", w.Name);

            host.Shutdown();

            Assert.AreEqual(0, c.RegisteredCount, "Shutdown 后清空");
        }

        [Test]
        public void CoexistsWithV04And05ModulesFullStack()
        {
            // V0.5 完整栈共存验证
            var host = new ModuleHost();
            host.Register<IConfigModule>(new MemoryConfigModule());           // -460
            host.Register<ISaveModule>(new MemorySaveModule());                // -450
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());// -420
            host.Register<IResourceModule>(new MemoryResourceModule());        // -400
            host.Register<IAudioModule>(new MemoryAudioModule());              // -380
            host.Register<IInputModule>(new MemoryInputModule());              // -350
            host.Register<IUIModule>(new MemoryUIModule());                    // -300
            host.Initialize();

            Assert.NotNull(host.Get<IConfigModule>());
            host.Shutdown();
        }
    }
}
