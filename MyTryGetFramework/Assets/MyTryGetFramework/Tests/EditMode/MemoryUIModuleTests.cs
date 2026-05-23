using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemoryUIModule（IUIModule 内存实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemoryUIModuleTests
    {
        [Test]
        public void Priority_BetweenResourceAndBusiness()
        {
            var ui = new MemoryUIModule();
            Assert.AreEqual(-300, ui.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var ui = new MemoryUIModule();
            Assert.AreEqual(0, ui.DependsOn.Count);
        }

        [Test]
        public void Open_NewUI_AddsToList()
        {
            var ui = new MemoryUIModule();
            ui.Open("MainMenu");

            Assert.IsTrue(ui.IsOpen("MainMenu"));
            Assert.AreEqual(1, ui.OpenedCount);
            Assert.AreEqual("MainMenu", ui.OpenedUIs[0]);
        }

        [Test]
        public void Open_NullOrEmpty_Throws()
        {
            var ui = new MemoryUIModule();
            Assert.Throws<ArgumentException>(() => ui.Open(null));
            Assert.Throws<ArgumentException>(() => ui.Open(""));
        }

        [Test]
        public void Open_Duplicate_Throws()
        {
            var ui = new MemoryUIModule();
            ui.Open("MainMenu");

            var ex = Assert.Throws<InvalidOperationException>(() => ui.Open("MainMenu"));
            StringAssert.Contains("already open", ex.Message);
            // 状态完整性：失败后仍只有一份
            Assert.AreEqual(1, ui.OpenedCount);
        }

        [Test]
        public void Close_OpenedUI_Removes()
        {
            var ui = new MemoryUIModule();
            ui.Open("MainMenu");
            ui.Close("MainMenu");

            Assert.IsFalse(ui.IsOpen("MainMenu"));
            Assert.AreEqual(0, ui.OpenedCount);
        }

        [Test]
        public void Close_Unknown_NoOp()
        {
            var ui = new MemoryUIModule();
            // 不抛
            Assert.DoesNotThrow(() => ui.Close("Ghost"));
            Assert.AreEqual(0, ui.OpenedCount);
        }

        [Test]
        public void Close_NullOrEmpty_NoOp()
        {
            var ui = new MemoryUIModule();
            ui.Open("Real");
            Assert.DoesNotThrow(() => ui.Close(null));
            Assert.DoesNotThrow(() => ui.Close(""));
            // 没有误关掉 Real
            Assert.IsTrue(ui.IsOpen("Real"));
            Assert.AreEqual(1, ui.OpenedCount);
        }

        [Test]
        public void IsOpen_NullOrEmpty_ReturnsFalse()
        {
            var ui = new MemoryUIModule();
            Assert.IsFalse(ui.IsOpen(null));
            Assert.IsFalse(ui.IsOpen(""));
        }

        [Test]
        public void OpenedUIs_KeepsInsertionOrder()
        {
            var ui = new MemoryUIModule();
            ui.Open("A");
            ui.Open("B");
            ui.Open("C");

            Assert.AreEqual(3, ui.OpenedCount);
            Assert.AreEqual("A", ui.OpenedUIs[0]);
            Assert.AreEqual("B", ui.OpenedUIs[1]);
            Assert.AreEqual("C", ui.OpenedUIs[2]);
        }

        [Test]
        public void Close_Middle_PreservesRemainingOrder()
        {
            var ui = new MemoryUIModule();
            ui.Open("A");
            ui.Open("B");
            ui.Open("C");

            ui.Close("B");

            Assert.AreEqual(2, ui.OpenedCount);
            Assert.AreEqual("A", ui.OpenedUIs[0]);
            Assert.AreEqual("C", ui.OpenedUIs[1]);
            Assert.IsFalse(ui.IsOpen("B"));
        }

        [Test]
        public void ReopenAfterClose_Works()
        {
            var ui = new MemoryUIModule();
            ui.Open("MainMenu");
            ui.Close("MainMenu");

            Assert.DoesNotThrow(() => ui.Open("MainMenu"));
            Assert.IsTrue(ui.IsOpen("MainMenu"));
        }

        [Test]
        public void CloseAll_ClearsEverything()
        {
            var ui = new MemoryUIModule();
            ui.Open("A");
            ui.Open("B");
            ui.Open("C");

            ui.CloseAll();

            Assert.AreEqual(0, ui.OpenedCount);
            Assert.IsFalse(ui.IsOpen("A"));
            Assert.IsFalse(ui.IsOpen("B"));
            Assert.IsFalse(ui.IsOpen("C"));
        }

        [Test]
        public void CloseAll_OnEmpty_NoThrow()
        {
            var ui = new MemoryUIModule();
            Assert.DoesNotThrow(() => ui.CloseAll());
            Assert.AreEqual(0, ui.OpenedCount);
        }

        [Test]
        public void Shutdown_ClearsAllUIs()
        {
            var ui = new MemoryUIModule();
            ui.Open("A");
            ui.Open("B");

            ui.Shutdown();

            Assert.AreEqual(0, ui.OpenedCount);
        }

        [Test]
        public void IntegratesWithModuleHost_BusinessCanOpenUI()
        {
            // 验证业务 Module 在 OnInit 时可通过 host.Get<IUIModule>() 拿到 UI 服务
            var host = new ModuleHost();
            var ui = new MemoryUIModule();
            host.Register<IUIModule>(ui);
            host.Initialize();

            // 模拟"启动后打开 MainMenu"
            host.Get<IUIModule>().Open("MainMenu");
            Assert.IsTrue(ui.IsOpen("MainMenu"));

            host.Shutdown();

            // Shutdown 后 UI 列表应清空
            Assert.AreEqual(0, ui.OpenedCount);
        }

        [Test]
        public void CoexistsWithResourceModule_DependencyOrderImplicit()
        {
            // 验证 Resource (-400) 在 UI (-300) 之前初始化（ModuleHost 按 Priority 排序）
            var host = new ModuleHost();
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IUIModule>(new MemoryUIModule());
            host.Initialize();

            // 业务可同时拿到两个
            var res = host.Get<IResourceModule>();
            var ui = host.Get<IUIModule>();
            Assert.NotNull(res);
            Assert.NotNull(ui);

            host.Shutdown();
        }
    }
}
