using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemoryResourceModule（IResourceModule 内存实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemoryResourceModuleTests
    {
        private class Prefab
        {
            public string Name;
        }

        private class AudioClip
        {
            public float DurationSeconds;
        }

        [Test]
        public void Priority_BetweenPoolAndEntityWorld()
        {
            var r = new MemoryResourceModule();
            Assert.AreEqual(-400, r.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var r = new MemoryResourceModule();
            Assert.AreEqual(0, r.DependsOn.Count);
        }

        [Test]
        public void Register_NewPath_StoresResource()
        {
            var r = new MemoryResourceModule();
            var prefab = new Prefab { Name = "hero" };
            r.Register("ui/hero", prefab);

            Assert.AreEqual(1, r.RegisteredCount);
        }

        [Test]
        public void Register_NullPath_Throws()
        {
            var r = new MemoryResourceModule();
            Assert.Throws<ArgumentException>(() => r.Register<Prefab>(null, new Prefab()));
            Assert.Throws<ArgumentException>(() => r.Register<Prefab>("", new Prefab()));
        }

        [Test]
        public void Register_NullResource_Throws()
        {
            var r = new MemoryResourceModule();
            Assert.Throws<ArgumentNullException>(() => r.Register<Prefab>("x", null));
        }

        [Test]
        public void Register_DuplicatePath_Throws()
        {
            var r = new MemoryResourceModule();
            r.Register("x", new Prefab());
            Assert.Throws<InvalidOperationException>(() => r.Register("x", new Prefab()));
        }

        [Test]
        public void Load_RegisteredPath_ReturnsResource()
        {
            var r = new MemoryResourceModule();
            var prefab = new Prefab { Name = "hero" };
            r.Register("ui/hero", prefab);

            var loaded = r.Load<Prefab>("ui/hero");
            Assert.AreSame(prefab, loaded);
        }

        [Test]
        public void Load_UnregisteredPath_ThrowsResourceNotFound()
        {
            var r = new MemoryResourceModule();
            var ex = Assert.Throws<ResourceNotFoundException>(() => r.Load<Prefab>("missing"));
            Assert.AreEqual("missing", ex.Path);
        }

        [Test]
        public void Load_WrongType_Throws()
        {
            var r = new MemoryResourceModule();
            r.Register("clip", new AudioClip { DurationSeconds = 1f });

            Assert.Throws<InvalidOperationException>(() => r.Load<Prefab>("clip"),
                "类型不匹配应抛");
        }

        [Test]
        public void TryLoad_RegisteredPath_ReturnsTrue()
        {
            var r = new MemoryResourceModule();
            var prefab = new Prefab();
            r.Register("x", prefab);

            Assert.IsTrue(r.TryLoad<Prefab>("x", out var loaded));
            Assert.AreSame(prefab, loaded);
        }

        [Test]
        public void TryLoad_Unregistered_ReturnsFalse()
        {
            var r = new MemoryResourceModule();
            Assert.IsFalse(r.TryLoad<Prefab>("missing", out var loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void TryLoad_WrongType_ReturnsFalse()
        {
            var r = new MemoryResourceModule();
            r.Register("clip", new AudioClip());

            Assert.IsFalse(r.TryLoad<Prefab>("clip", out var loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void Unregister_RegisteredPath_ReturnsTrueAndRemoves()
        {
            var r = new MemoryResourceModule();
            r.Register("x", new Prefab());

            Assert.IsTrue(r.Unregister("x"));
            Assert.AreEqual(0, r.RegisteredCount);
            Assert.IsFalse(r.TryLoad<Prefab>("x", out _));
        }

        [Test]
        public void Unregister_Unknown_ReturnsFalse()
        {
            var r = new MemoryResourceModule();
            Assert.IsFalse(r.Unregister("missing"));
        }

        [Test]
        public void Release_AnyObject_IsNoOp()
        {
            // Memory 实现 Release 不应改变状态
            var r = new MemoryResourceModule();
            var prefab = new Prefab();
            r.Register("x", prefab);

            r.Release(prefab);

            Assert.AreEqual(1, r.RegisteredCount, "Memory Release 不应清理已注册资源");
            Assert.IsTrue(r.TryLoad<Prefab>("x", out _));
        }

        [Test]
        public void Shutdown_ClearsAllResources()
        {
            var r = new MemoryResourceModule();
            r.Register("a", new Prefab());
            r.Register("b", new Prefab());

            r.Shutdown();

            Assert.AreEqual(0, r.RegisteredCount);
        }

        [Test]
        public void DifferentTypesByPath_CanCoexist()
        {
            var r = new MemoryResourceModule();
            r.Register("ui/hero", new Prefab { Name = "hero" });
            r.Register("sfx/punch", new AudioClip { DurationSeconds = 0.3f });

            Assert.AreEqual(2, r.RegisteredCount);
            Assert.IsNotNull(r.Load<Prefab>("ui/hero"));
            Assert.IsNotNull(r.Load<AudioClip>("sfx/punch"));
        }

        [Test]
        public void IntegratesWithModuleHost_ResourceUsableInProcedureOnEnter()
        {
            // 验证 ResourceModule 在 ModuleHost 注册后可被 Procedure 通过 host.Get 拉取
            var host = new ModuleHost();
            var resources = new MemoryResourceModule();
            host.Register<IResourceModule>(resources);
            host.Initialize();

            // 模拟"启动前预注册资源"
            resources.Register("ui/main_menu", new Prefab { Name = "MainMenu" });

            var loaded = host.Get<IResourceModule>().Load<Prefab>("ui/main_menu");
            Assert.AreEqual("MainMenu", loaded.Name);

            host.Shutdown();

            // Shutdown 后资源应被清空
            Assert.AreEqual(0, resources.RegisteredCount);
        }
    }
}
