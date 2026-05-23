using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemoryInputModule（IInputModule 实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemoryInputModuleTests
    {
        [Test]
        public void Priority_BetweenAudioAndUI()
        {
            var i = new MemoryInputModule();
            Assert.AreEqual(-350, i.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var i = new MemoryInputModule();
            Assert.AreEqual(0, i.DependsOn.Count);
        }

        // —— 初始状态 ——

        [Test]
        public void Initial_NothingPressed_AxesZero()
        {
            var i = new MemoryInputModule();
            Assert.AreEqual(0, i.ActiveCount);
            Assert.IsFalse(i.IsPressed("Jump"));
            Assert.IsFalse(i.WasPressedThisFrame("Jump"));
            Assert.AreEqual(0f, i.GetAxis("Move"));
            i.GetAxis2D("Look", out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        // —— Press / Release ——

        [Test]
        public void SimulatePress_NewAction_StartsPressedAndFiresEdge()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Jump");

            Assert.IsTrue(i.IsPressed("Jump"));
            Assert.IsTrue(i.WasPressedThisFrame("Jump"));
            Assert.IsFalse(i.WasReleasedThisFrame("Jump"));
            Assert.AreEqual(1, i.ActiveCount);
        }

        [Test]
        public void SimulatePress_NullOrEmpty_Throws()
        {
            var i = new MemoryInputModule();
            Assert.Throws<ArgumentException>(() => i.SimulatePress(null));
            Assert.Throws<ArgumentException>(() => i.SimulatePress(""));
        }

        [Test]
        public void SimulatePress_Twice_EdgeOnlyOnce()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Jump");
            // 重复按下不应再触发 edge（但 IsPressed 保持 true）
            i.SimulatePress("Jump");

            Assert.IsTrue(i.IsPressed("Jump"));
            Assert.IsTrue(i.WasPressedThisFrame("Jump"));
            Assert.AreEqual(1, i.ActiveCount);
        }

        [Test]
        public void SimulateRelease_PressedAction_FiresReleaseEdge()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Jump");
            i.SimulateRelease("Jump");

            Assert.IsFalse(i.IsPressed("Jump"));
            Assert.IsTrue(i.WasReleasedThisFrame("Jump"));
            // Unity 行为：同帧 Press+Release，两个 edge 都 true
            Assert.IsTrue(i.WasPressedThisFrame("Jump"), "同帧 Press→Release，Press edge 保留（Unity GetKeyDown/Up 同帧均 true）");
            Assert.AreEqual(0, i.ActiveCount);
        }

        [Test]
        public void SimulateRelease_NeverPressed_NoEdge()
        {
            var i = new MemoryInputModule();
            i.SimulateRelease("Jump");

            Assert.IsFalse(i.IsPressed("Jump"));
            Assert.IsFalse(i.WasReleasedThisFrame("Jump"), "未按下的 action Release 不触发 edge");
        }

        [Test]
        public void SimulateRelease_NullOrEmpty_Throws()
        {
            var i = new MemoryInputModule();
            Assert.Throws<ArgumentException>(() => i.SimulateRelease(null));
            Assert.Throws<ArgumentException>(() => i.SimulateRelease(""));
        }

        // —— Per-frame edge 语义 ——

        [Test]
        public void Update_ClearsThisFrameEdges_KeepsPressed()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Jump");

            Assert.IsTrue(i.WasPressedThisFrame("Jump"));

            // Update 推进到下一帧
            i.Update(0.016f, 0.016f);

            Assert.IsTrue(i.IsPressed("Jump"), "按住状态保持");
            Assert.IsFalse(i.WasPressedThisFrame("Jump"), "edge 在下一帧 Update 后归 false");
        }

        [Test]
        public void Update_ClearsReleaseEdge()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Jump");
            i.SimulateRelease("Jump");

            Assert.IsTrue(i.WasReleasedThisFrame("Jump"));

            i.Update(0.016f, 0.016f);

            Assert.IsFalse(i.WasReleasedThisFrame("Jump"));
        }

        [Test]
        public void PressReleaseSequence_AcrossFrames_EdgesAlignToCallFrame()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Fire");
            Assert.IsTrue(i.WasPressedThisFrame("Fire"));

            i.Update(0.016f, 0.016f);
            Assert.IsFalse(i.WasPressedThisFrame("Fire"));
            Assert.IsTrue(i.IsPressed("Fire"));

            i.SimulateRelease("Fire");
            Assert.IsTrue(i.WasReleasedThisFrame("Fire"));

            i.Update(0.016f, 0.016f);
            Assert.IsFalse(i.WasReleasedThisFrame("Fire"));
            Assert.IsFalse(i.IsPressed("Fire"));
        }

        // —— Query 边界 ——

        [Test]
        public void IsPressed_NullOrEmpty_False()
        {
            var i = new MemoryInputModule();
            Assert.IsFalse(i.IsPressed(null));
            Assert.IsFalse(i.IsPressed(""));
            Assert.IsFalse(i.WasPressedThisFrame(null));
            Assert.IsFalse(i.WasReleasedThisFrame(""));
        }

        // —— Axis ——

        [Test]
        public void SetGetAxis_RoundTrip()
        {
            var i = new MemoryInputModule();
            i.SetAxis("Horizontal", 0.5f);
            Assert.AreEqual(0.5f, i.GetAxis("Horizontal"));
        }

        [Test]
        public void SetAxis_Clamped()
        {
            var i = new MemoryInputModule();
            i.SetAxis("H", -2f);
            Assert.AreEqual(-1f, i.GetAxis("H"));
            i.SetAxis("H", 5f);
            Assert.AreEqual(1f, i.GetAxis("H"));
        }

        [Test]
        public void SetAxis_NullOrEmpty_Throws()
        {
            var i = new MemoryInputModule();
            Assert.Throws<ArgumentException>(() => i.SetAxis(null, 0.5f));
            Assert.Throws<ArgumentException>(() => i.SetAxis("", 0.5f));
        }

        [Test]
        public void GetAxis_UnsetAction_Zero()
        {
            var i = new MemoryInputModule();
            Assert.AreEqual(0f, i.GetAxis("Unset"));
        }

        [Test]
        public void GetAxis_NullOrEmpty_Zero()
        {
            var i = new MemoryInputModule();
            Assert.AreEqual(0f, i.GetAxis(null));
            Assert.AreEqual(0f, i.GetAxis(""));
        }

        // —— Axis2D ——

        [Test]
        public void SetGetAxis2D_RoundTrip()
        {
            var i = new MemoryInputModule();
            i.SetAxis2D("Move", 0.3f, -0.7f);

            i.GetAxis2D("Move", out var x, out var y);
            Assert.AreEqual(0.3f, x);
            Assert.AreEqual(-0.7f, y);
        }

        [Test]
        public void SetAxis2D_Clamped()
        {
            var i = new MemoryInputModule();
            i.SetAxis2D("Move", -2f, 3f);

            i.GetAxis2D("Move", out var x, out var y);
            Assert.AreEqual(-1f, x);
            Assert.AreEqual(1f, y);
        }

        [Test]
        public void GetAxis2D_UnsetAction_Zero()
        {
            var i = new MemoryInputModule();
            i.GetAxis2D("Unset", out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        // —— Shutdown ——

        [Test]
        public void Shutdown_ClearsEverything()
        {
            var i = new MemoryInputModule();
            i.SimulatePress("Jump");
            i.SetAxis("H", 0.5f);
            i.SetAxis2D("Move", 0.3f, 0.4f);

            i.Shutdown();

            Assert.AreEqual(0, i.ActiveCount);
            Assert.IsFalse(i.IsPressed("Jump"));
            Assert.IsFalse(i.WasPressedThisFrame("Jump"));
            Assert.AreEqual(0f, i.GetAxis("H"));
            i.GetAxis2D("Move", out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        // —— ModuleHost 集成 + IUpdateModule 驱动 ——

        [Test]
        public void IntegratesWithModuleHost_UpdateClearsEdges()
        {
            var host = new ModuleHost();
            host.Register<IInputModule>(new MemoryInputModule());
            host.Initialize();

            var input = (MemoryInputModule)host.Get<IInputModule>();
            input.SimulatePress("Jump");

            Assert.IsTrue(input.WasPressedThisFrame("Jump"));

            // host.Update 应通过 IUpdateModule 调度调到 MemoryInputModule.Update
            host.Update(0.016f, 0.016f);

            Assert.IsFalse(input.WasPressedThisFrame("Jump"), "host.Update 经 IUpdateModule 调度清 edge");
            Assert.IsTrue(input.IsPressed("Jump"));

            host.Shutdown();
        }

        [Test]
        public void CoexistsWithV04ModulesFullStack()
        {
            // 与 V0.4 五件套共存，验证 V0.5 Input 加入后拓扑链仍干净
            var host = new ModuleHost();
            host.Register<ISaveModule>(new MemorySaveModule());
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IInputModule>(new MemoryInputModule());
            host.Register<IUIModule>(new MemoryUIModule());
            host.Initialize();

            Assert.NotNull(host.Get<IInputModule>());
            host.Shutdown();
        }
    }
}
