using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemorySceneModule（ISceneModule 实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemorySceneModuleTests
    {
        [Test]
        public void Priority_BetweenUIAndProcedure()
        {
            var s = new MemorySceneModule();
            Assert.AreEqual(-250, s.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var s = new MemorySceneModule();
            Assert.AreEqual(0, s.DependsOn.Count);
        }

        // —— 初始状态 ——

        [Test]
        public void Initial_NoScene_NoActive()
        {
            var s = new MemorySceneModule();
            Assert.IsNull(s.ActiveScene);
            Assert.AreEqual(0, s.LoadedCount);
            Assert.AreEqual(0, s.LoadedScenes.Count);
        }

        // —— Load ——

        [Test]
        public void Load_FirstScene_BecomesActive()
        {
            var s = new MemorySceneModule();
            s.Load("Main");

            Assert.IsTrue(s.IsLoaded("Main"));
            Assert.AreEqual("Main", s.ActiveScene, "第一个加载自动成 active");
            Assert.AreEqual(1, s.LoadedCount);
        }

        [Test]
        public void Load_SecondScene_KeepsFirstActive()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            s.Load("UI");

            Assert.AreEqual("Main", s.ActiveScene, "第二个不抢 active");
            Assert.AreEqual(2, s.LoadedCount);
            Assert.AreEqual("Main", s.LoadedScenes[0]);
            Assert.AreEqual("UI", s.LoadedScenes[1]);
        }

        [Test]
        public void Load_NullOrEmpty_Throws()
        {
            var s = new MemorySceneModule();
            Assert.Throws<ArgumentException>(() => s.Load(null));
            Assert.Throws<ArgumentException>(() => s.Load(""));
        }

        [Test]
        public void Load_Duplicate_Throws()
        {
            var s = new MemorySceneModule();
            s.Load("Main");

            var ex = Assert.Throws<InvalidOperationException>(() => s.Load("Main"));
            StringAssert.Contains("already loaded", ex.Message);
            Assert.AreEqual(1, s.LoadedCount, "失败后状态不变");
        }

        // —— Unload ——

        [Test]
        public void Unload_Loaded_RemovesAndReturnsTrue()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            s.Load("UI");

            Assert.IsTrue(s.Unload("UI"));
            Assert.IsFalse(s.IsLoaded("UI"));
            Assert.AreEqual(1, s.LoadedCount);
            Assert.AreEqual("Main", s.ActiveScene, "非 active 卸载不影响 active");
        }

        [Test]
        public void Unload_Active_NullsActive()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            s.Load("UI");

            Assert.IsTrue(s.Unload("Main")); // Main 是 active
            Assert.IsNull(s.ActiveScene, "卸载 active 后 active 置 null");
            Assert.AreEqual(1, s.LoadedCount);
            Assert.IsTrue(s.IsLoaded("UI"));
        }

        [Test]
        public void Unload_Missing_ReturnsFalse()
        {
            var s = new MemorySceneModule();
            Assert.IsFalse(s.Unload("Ghost"));
        }

        [Test]
        public void Unload_NullOrEmpty_ReturnsFalse()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            Assert.IsFalse(s.Unload(null));
            Assert.IsFalse(s.Unload(""));
            Assert.IsTrue(s.IsLoaded("Main"), "未误卸载");
        }

        // —— SetActive ——

        [Test]
        public void SetActive_Loaded_Changes()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            s.Load("UI");

            s.SetActive("UI");
            Assert.AreEqual("UI", s.ActiveScene);
        }

        [Test]
        public void SetActive_NotLoaded_Throws()
        {
            var s = new MemorySceneModule();
            s.Load("Main");

            var ex = Assert.Throws<InvalidOperationException>(() => s.SetActive("Ghost"));
            StringAssert.Contains("not loaded", ex.Message);
            Assert.AreEqual("Main", s.ActiveScene, "失败时 active 不破坏");
        }

        [Test]
        public void SetActive_NullOrEmpty_Throws()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            Assert.Throws<ArgumentException>(() => s.SetActive(null));
            Assert.Throws<ArgumentException>(() => s.SetActive(""));
        }

        // —— IsLoaded ——

        [Test]
        public void IsLoaded_Boundary()
        {
            var s = new MemorySceneModule();
            Assert.IsFalse(s.IsLoaded(null));
            Assert.IsFalse(s.IsLoaded(""));
            Assert.IsFalse(s.IsLoaded("Ghost"));

            s.Load("Main");
            Assert.IsTrue(s.IsLoaded("Main"));
        }

        // —— UnloadAll ——

        [Test]
        public void UnloadAll_ClearsEverything()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            s.Load("UI");
            s.Load("Battle");

            s.UnloadAll();

            Assert.AreEqual(0, s.LoadedCount);
            Assert.IsNull(s.ActiveScene);
            Assert.IsFalse(s.IsLoaded("Main"));
        }

        // —— 顺序保持 ——

        [Test]
        public void LoadedScenes_KeepsInsertionOrder()
        {
            var s = new MemorySceneModule();
            s.Load("A");
            s.Load("B");
            s.Load("C");

            Assert.AreEqual("A", s.LoadedScenes[0]);
            Assert.AreEqual("B", s.LoadedScenes[1]);
            Assert.AreEqual("C", s.LoadedScenes[2]);
        }

        [Test]
        public void Unload_Middle_PreservesRemainingOrder()
        {
            var s = new MemorySceneModule();
            s.Load("A");
            s.Load("B");
            s.Load("C");

            s.Unload("B");

            Assert.AreEqual(2, s.LoadedCount);
            Assert.AreEqual("A", s.LoadedScenes[0]);
            Assert.AreEqual("C", s.LoadedScenes[1]);
        }

        // —— Shutdown ——

        [Test]
        public void Shutdown_ClearsEverything()
        {
            var s = new MemorySceneModule();
            s.Load("Main");
            s.Load("UI");

            s.Shutdown();

            Assert.AreEqual(0, s.LoadedCount);
            Assert.IsNull(s.ActiveScene);
        }

        // —— ModuleHost 集成 + Procedure 协同模拟 ——

        [Test]
        public void IntegratesWithModuleHost_LoadUnloadSequence()
        {
            var host = new ModuleHost();
            host.Register<ISceneModule>(new MemorySceneModule());
            host.Initialize();

            var scenes = host.Get<ISceneModule>();
            scenes.Load("MainMenu");
            scenes.Load("Loading");
            scenes.SetActive("Loading");

            Assert.AreEqual("Loading", scenes.ActiveScene);

            scenes.Unload("MainMenu");
            Assert.AreEqual(1, scenes.LoadedCount);

            scenes.Load("Battle");
            scenes.SetActive("Battle");
            scenes.Unload("Loading");

            Assert.AreEqual("Battle", scenes.ActiveScene);
            Assert.AreEqual(1, scenes.LoadedCount);

            host.Shutdown();
        }

        [Test]
        public void CoexistsWithV04And05ModulesFullStack()
        {
            var host = new ModuleHost();
            host.Register<IConfigModule>(new MemoryConfigModule());
            host.Register<ISaveModule>(new MemorySaveModule());
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IInputModule>(new MemoryInputModule());
            host.Register<IUIModule>(new MemoryUIModule());
            host.Register<ISceneModule>(new MemorySceneModule());
            host.Initialize();

            Assert.NotNull(host.Get<ISceneModule>());
            host.Shutdown();
        }
    }
}
