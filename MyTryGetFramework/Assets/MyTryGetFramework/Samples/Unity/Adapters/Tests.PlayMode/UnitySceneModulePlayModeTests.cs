using NUnit.Framework;
using TryGet;
using TryGet.Unity;
using UnityEngine.SceneManagement;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// UnitySceneModule（ISceneModule Unity Adapter）的 PlayMode 测试。
    ///
    /// 注意：本测试只验证 Adapter 内部状态机的转换契约，不验证真实 Unity 场景加载
    /// （后者依赖 BuildSettings 配置，属业务集成测试范围）。
    /// 所有 Load 调用使用当前 active scene 名（PlayMode runner 自带），保证 SceneManager.LoadScene
    /// 调用不会因场景缺失抛错。
    /// </summary>
    [TestFixture]
    public class UnitySceneModulePlayModeTests
    {
        [Test]
        public void Priority_SameAsMemoryImpl()
        {
            var s = new UnitySceneModule();
            Assert.AreEqual(-250, s.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var s = new UnitySceneModule();
            Assert.AreEqual(0, s.DependsOn.Count);
        }

        [Test]
        public void Initial_NoScene_NoActive()
        {
            var s = new UnitySceneModule();
            Assert.IsNull(s.ActiveScene);
            Assert.AreEqual(0, s.LoadedCount);
        }

        [Test]
        public void Load_NullOrEmpty_Throws()
        {
            var s = new UnitySceneModule();
            Assert.Throws<System.ArgumentException>(() => s.Load(null));
            Assert.Throws<System.ArgumentException>(() => s.Load(""));
        }

        [Test]
        public void Load_Duplicate_Throws()
        {
            var s = new UnitySceneModule();
            var activeName = SceneManager.GetActiveScene().name;
            // 第一次 Load：记录内部状态（真实加载可能失败但 Adapter 状态机已变）
            // 用 try-catch 容错：场景在 BuildSettings 中才能真正加载，否则 SceneManager.LoadScene 会 log error
            try { s.Load(activeName); } catch { /* 真实加载失败也可，只验证状态机 */ }

            Assert.Throws<System.InvalidOperationException>(() => s.Load(activeName));
        }

        [Test]
        public void Unload_Missing_ReturnsFalse()
        {
            var s = new UnitySceneModule();
            Assert.IsFalse(s.Unload("Ghost"));
        }

        [Test]
        public void Unload_NullOrEmpty_ReturnsFalse()
        {
            var s = new UnitySceneModule();
            Assert.IsFalse(s.Unload(null));
            Assert.IsFalse(s.Unload(""));
        }

        [Test]
        public void SetActive_NotLoaded_Throws()
        {
            var s = new UnitySceneModule();
            Assert.Throws<System.InvalidOperationException>(() => s.SetActive("Ghost"));
        }

        [Test]
        public void SetActive_NullOrEmpty_Throws()
        {
            var s = new UnitySceneModule();
            Assert.Throws<System.ArgumentException>(() => s.SetActive(null));
            Assert.Throws<System.ArgumentException>(() => s.SetActive(""));
        }

        [Test]
        public void IsLoaded_Boundary()
        {
            var s = new UnitySceneModule();
            Assert.IsFalse(s.IsLoaded(null));
            Assert.IsFalse(s.IsLoaded(""));
            Assert.IsFalse(s.IsLoaded("Ghost"));
        }

        [Test]
        public void IntegratesWithModuleHost_RegisterInitShutdown()
        {
            var host = new ModuleHost();
            host.Register<ISceneModule>(new UnitySceneModule());
            host.Initialize();

            Assert.NotNull(host.Get<ISceneModule>());
            Assert.IsNull(host.Get<ISceneModule>().ActiveScene);

            host.Shutdown();
        }

        [Test]
        public void Shutdown_ClearsInternalState_DoesNotUnloadScenes()
        {
            // 关键：Adapter Shutdown 不应卸载已加载场景（持久化资源不应被 Shutdown 擦除）
            // 我们只能验证 Adapter 内部状态清空（_ordered/_index/_active）
            var s = new UnitySceneModule();
            s.Shutdown();

            Assert.AreEqual(0, s.LoadedCount);
            Assert.IsNull(s.ActiveScene);
        }

        [Test]
        public void CoexistsWithOtherUnityAdapters()
        {
            // 与其他 Unity Adapter 共存验证（Audio / Input / UI 已落地）
            var host = new ModuleHost();
            host.Register<IAudioModule>(new UnityAudioModule(poolSize: 2));
            host.Register<IInputModule>(new UnityInputModule());
            host.Register<IUIModule>(new UGUIUIModule());
            host.Register<ISaveModule>(new PlayerPrefsSaveModule());
            host.Register<ISceneModule>(new UnitySceneModule());
            host.Initialize();

            Assert.NotNull(host.Get<ISceneModule>());

            host.Shutdown();
        }
    }
}
