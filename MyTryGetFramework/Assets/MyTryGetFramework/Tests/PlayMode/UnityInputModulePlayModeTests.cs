using System.Collections;
using NUnit.Framework;
using TryGet;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// UnityInputModule（IInputModule Unity Adapter）的 PlayMode 测试。
    /// 用 InputSystem.AddDevice + Press/Release 模拟硬件输入。
    /// </summary>
    [TestFixture]
    public class UnityInputModulePlayModeTests
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        [SetUp]
        public void SetUp()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_gamepad != null) InputSystem.RemoveDevice(_gamepad);
        }

        [Test]
        public void Priority_SameAsMemoryImpl()
        {
            var ui = new UnityInputModule();
            Assert.AreEqual(-350, ui.Priority);
        }

        [UnityTest]
        public IEnumerator RegisterButton_KeyboardPress_TriggersEdge()
        {
            var host = new ModuleHost();
            var ui = new UnityInputModule();
            host.Register<IInputModule>(ui);
            host.Initialize();

            ui.RegisterButton("Jump", "<Keyboard>/space");
            yield return null; // 让 Enable 生效

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();

            Assert.IsTrue(host.Get<IInputModule>().IsPressed("Jump"));
            Assert.IsTrue(host.Get<IInputModule>().WasPressedThisFrame("Jump"));
            Assert.IsFalse(host.Get<IInputModule>().WasReleasedThisFrame("Jump"));

            host.Shutdown();
        }

        [UnityTest]
        public IEnumerator RegisterButton_PressThenRelease_EdgesCorrect()
        {
            var host = new ModuleHost();
            var ui = new UnityInputModule();
            host.Register<IInputModule>(ui);
            host.Initialize();

            ui.RegisterButton("Jump", "<Keyboard>/space");
            yield return null;

            // Press
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            Assert.IsTrue(host.Get<IInputModule>().WasPressedThisFrame("Jump"));

            // Release
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); // 空 state = 全释放
            InputSystem.Update();
            Assert.IsFalse(host.Get<IInputModule>().IsPressed("Jump"));
            Assert.IsTrue(host.Get<IInputModule>().WasReleasedThisFrame("Jump"));

            host.Shutdown();
        }

        [UnityTest]
        public IEnumerator LateUpdate_ClearsEdges_KeepsPressed()
        {
            var host = new ModuleHost();
            var ui = new UnityInputModule();
            host.Register<IInputModule>(ui);
            host.Initialize();

            ui.RegisterButton("Jump", "<Keyboard>/space");
            yield return null;

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();

            Assert.IsTrue(host.Get<IInputModule>().WasPressedThisFrame("Jump"));

            // host.Update 不清 edge（业务消费阶段）
            host.Update(0.016f, 0.016f);
            Assert.IsTrue(host.Get<IInputModule>().WasPressedThisFrame("Jump"), "Update 期间 edge 仍可消费");

            // host.LateUpdate 清 edge
            host.LateUpdate(0.016f, 0.016f);
            Assert.IsFalse(host.Get<IInputModule>().WasPressedThisFrame("Jump"));
            Assert.IsTrue(host.Get<IInputModule>().IsPressed("Jump"), "按住状态保持");

            host.Shutdown();
        }

        [UnityTest]
        public IEnumerator RegisterAxis_Gamepad_ReadsValue()
        {
            var host = new ModuleHost();
            var ui = new UnityInputModule();
            host.Register<IInputModule>(ui);
            host.Initialize();

            ui.RegisterAxis("Throttle", "<Gamepad>/leftTrigger");
            yield return null;

            using (StateEvent.From(_gamepad, out var eventPtr))
            {
                _gamepad.leftTrigger.WriteValueIntoEvent(0.5f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();

            Assert.AreEqual(0.5f, host.Get<IInputModule>().GetAxis("Throttle"), 0.01f);

            host.Shutdown();
        }

        [UnityTest]
        public IEnumerator RegisterAxis2D_Gamepad_ReadsVector2()
        {
            var host = new ModuleHost();
            var ui = new UnityInputModule();
            host.Register<IInputModule>(ui);
            host.Initialize();

            ui.RegisterAxis2D("Move", "<Gamepad>/leftStick");
            yield return null;

            using (StateEvent.From(_gamepad, out var eventPtr))
            {
                _gamepad.leftStick.WriteValueIntoEvent(new Vector2(0.7f, -0.3f), eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();

            host.Get<IInputModule>().GetAxis2D("Move", out var x, out var y);
            Assert.AreEqual(0.7f, x, 0.05f);
            Assert.AreEqual(-0.3f, y, 0.05f);

            host.Shutdown();
        }

        [Test]
        public void RegisterButton_NullOrEmpty_Throws()
        {
            var ui = new UnityInputModule();
            Assert.Throws<System.ArgumentException>(() => ui.RegisterButton(null, "<Keyboard>/space"));
            Assert.Throws<System.ArgumentException>(() => ui.RegisterButton("Jump", null));
            Assert.Throws<System.ArgumentException>(() => ui.RegisterButton("", "<Keyboard>/space"));
        }

        [Test]
        public void RegisterButton_Duplicate_Throws()
        {
            var ui = new UnityInputModule();
            ui.RegisterButton("Jump", "<Keyboard>/space");

            Assert.Throws<System.InvalidOperationException>(
                () => ui.RegisterButton("Jump", "<Keyboard>/enter"));

            ui.Shutdown();
        }

        [UnityTest]
        public IEnumerator UnregisterAction_RemovesAndDisposes()
        {
            var ui = new UnityInputModule();
            ui.RegisterButton("Jump", "<Keyboard>/space");

            yield return null;

            Assert.IsTrue(ui.UnregisterAction("Jump"));

            // 再 Press 不影响 Jump（已 Disable + Dispose）
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();

            Assert.IsFalse(ui.IsPressed("Jump"));

            ui.Shutdown();
        }

        [UnityTest]
        public IEnumerator Shutdown_DisablesAllActions()
        {
            var host = new ModuleHost();
            var ui = new UnityInputModule();
            host.Register<IInputModule>(ui);
            host.Initialize();

            ui.RegisterButton("Jump", "<Keyboard>/space");
            ui.RegisterAxis("Throttle", "<Gamepad>/leftTrigger");

            host.Shutdown();

            yield return null;

            // Shutdown 后再 Press，状态不应改变（Adapter 已 Disable + Dispose 所有 InputAction）
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();

            Assert.AreEqual(0, ui.ActiveCount);
            Assert.IsFalse(ui.IsPressed("Jump"));
        }

        [Test]
        public void GetAxis_UnregisteredAction_ReturnsZero()
        {
            var ui = new UnityInputModule();
            Assert.AreEqual(0f, ui.GetAxis("Unset"));
            ui.GetAxis2D("Unset", out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void IsPressed_NullOrEmpty_False()
        {
            var ui = new UnityInputModule();
            Assert.IsFalse(ui.IsPressed(null));
            Assert.IsFalse(ui.IsPressed(""));
            Assert.IsFalse(ui.WasPressedThisFrame(null));
        }
    }
}
