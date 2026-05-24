using System.Collections;
using NUnit.Framework;
using TryGet;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// UGUIUIModule（IUIModule Unity Adapter）的 PlayMode 测试。
    /// </summary>
    [TestFixture]
    public class UGUIUIModulePlayModeTests
    {
        private GameObject MakePrefab(string name)
        {
            // 创建一个简单的 UI prefab：含 RectTransform + Image
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            return go;
        }

        [Test]
        public void Priority_SameAsMemoryImpl()
        {
            var ui = new UGUIUIModule();
            Assert.AreEqual(-300, ui.Priority);
        }

        [Test]
        public void Initialize_CreatesCanvasRoot()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var root = GameObject.Find("[UGUIRoot]");
            Assert.NotNull(root, "Initialize 应创建 Canvas root");
            Assert.NotNull(root.GetComponent<Canvas>());
            Assert.NotNull(root.GetComponent<GraphicRaycaster>());
            Assert.NotNull(ui.Canvas);

            host.Shutdown();
            Assert.IsNull(GameObject.Find("[UGUIRoot]"), "Shutdown 应销毁 root");
        }

        [UnityTest]
        public IEnumerator Open_RegisteredPrefab_InstantiatesUnderCanvas()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var prefab = MakePrefab("MainMenuPrefab");
            ui.RegisterPrefab("MainMenu", prefab);

            host.Get<IUIModule>().Open("MainMenu");
            yield return null;

            Assert.IsTrue(host.Get<IUIModule>().IsOpen("MainMenu"));
            Assert.AreEqual(1, host.Get<IUIModule>().OpenedCount);

            // 验证实例化到 Canvas
            var instance = GameObject.Find("MainMenu");
            Assert.NotNull(instance);
            Assert.AreEqual(ui.Canvas.transform, instance.transform.parent);

            host.Shutdown();
            Object.Destroy(prefab);
        }

        [Test]
        public void Open_UnregisteredUI_Throws()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            Assert.Throws<System.InvalidOperationException>(() => host.Get<IUIModule>().Open("Ghost"));

            host.Shutdown();
        }

        [Test]
        public void Open_Duplicate_Throws()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var prefab = MakePrefab("X");
            ui.RegisterPrefab("X", prefab);
            host.Get<IUIModule>().Open("X");

            Assert.Throws<System.InvalidOperationException>(() => host.Get<IUIModule>().Open("X"));

            host.Shutdown();
            Object.Destroy(prefab);
        }

        [UnityTest]
        public IEnumerator Close_OpenedUI_DestroysInstance()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var prefab = MakePrefab("X");
            ui.RegisterPrefab("X", prefab);
            host.Get<IUIModule>().Open("X");
            yield return null;

            Assert.IsNotNull(GameObject.Find("X"));

            host.Get<IUIModule>().Close("X");
            yield return null;

            Assert.IsFalse(host.Get<IUIModule>().IsOpen("X"));
            Assert.IsNull(GameObject.Find("X"), "GameObject 应被 Destroy");
            Assert.AreEqual(0, host.Get<IUIModule>().OpenedCount);

            host.Shutdown();
            Object.Destroy(prefab);
        }

        [Test]
        public void Close_UnknownUI_NoThrow()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            Assert.DoesNotThrow(() => host.Get<IUIModule>().Close("Ghost"));

            host.Shutdown();
        }

        [UnityTest]
        public IEnumerator CloseAll_DestroysAllInstances()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var p1 = MakePrefab("A");
            var p2 = MakePrefab("B");
            var p3 = MakePrefab("C");
            ui.RegisterPrefab("A", p1);
            ui.RegisterPrefab("B", p2);
            ui.RegisterPrefab("C", p3);

            host.Get<IUIModule>().Open("A");
            host.Get<IUIModule>().Open("B");
            host.Get<IUIModule>().Open("C");
            yield return null;

            host.Get<IUIModule>().CloseAll();
            yield return null;

            Assert.AreEqual(0, host.Get<IUIModule>().OpenedCount);
            Assert.IsNull(GameObject.Find("A"));
            Assert.IsNull(GameObject.Find("B"));
            Assert.IsNull(GameObject.Find("C"));

            host.Shutdown();
            Object.Destroy(p1); Object.Destroy(p2); Object.Destroy(p3);
        }

        [Test]
        public void RegisterPrefab_NullArgs_Throws()
        {
            var ui = new UGUIUIModule();
            var prefab = MakePrefab("X");
            Assert.Throws<System.ArgumentException>(() => ui.RegisterPrefab(null, prefab));
            Assert.Throws<System.ArgumentException>(() => ui.RegisterPrefab("", prefab));
            Assert.Throws<System.ArgumentNullException>(() => ui.RegisterPrefab("X", null));
            Object.Destroy(prefab);
        }

        [UnityTest]
        public IEnumerator UnregisterPrefab_OpenedUI_ClosesFirst()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var prefab = MakePrefab("X");
            ui.RegisterPrefab("X", prefab);
            host.Get<IUIModule>().Open("X");
            yield return null;

            Assert.IsTrue(ui.UnregisterPrefab("X"));
            Assert.IsFalse(host.Get<IUIModule>().IsOpen("X"));
            Assert.IsNull(GameObject.Find("X"));

            // 再 Open 应抛
            Assert.Throws<System.InvalidOperationException>(() => host.Get<IUIModule>().Open("X"));

            host.Shutdown();
            Object.Destroy(prefab);
        }

        [UnityTest]
        public IEnumerator Shutdown_DestroysAllUIInstances()
        {
            var host = new ModuleHost();
            var ui = new UGUIUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            var p1 = MakePrefab("A");
            var p2 = MakePrefab("B");
            ui.RegisterPrefab("A", p1);
            ui.RegisterPrefab("B", p2);
            host.Get<IUIModule>().Open("A");
            host.Get<IUIModule>().Open("B");

            host.Shutdown();
            yield return null;

            Assert.IsNull(GameObject.Find("A"));
            Assert.IsNull(GameObject.Find("B"));
            Assert.IsNull(GameObject.Find("[UGUIRoot]"));

            Object.Destroy(p1); Object.Destroy(p2);
        }
    }
}
