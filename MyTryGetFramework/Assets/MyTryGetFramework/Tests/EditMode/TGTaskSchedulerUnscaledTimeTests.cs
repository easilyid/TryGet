using NUnit.Framework;
using System;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// C3 — TGTaskScheduler Unscaled Time 测试。
    ///
    /// 验证 Delay(seconds, TimeMode.Unscaled) 不受 scaled deltaTime 影响，
    /// 使用真实 unscaledDeltaTime 计时。
    /// </summary>
    [TestFixture]
    public class TGTaskSchedulerUnscaledTimeTests
    {
        private TGTaskScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new TGTaskScheduler();
            var host = new ModuleSystem();
            _scheduler.OnInit(host);
        }

        [TearDown]
        public void TearDown()
        {
            _scheduler.Shutdown();
        }

        [Test]
        public void Delay_ScaledMode_UsesScaledDeltaTime()
        {
            // Delay 1秒，Scaled mode（默认）
            var task = _scheduler.Delay(1.0f, TimeMode.Scaled);

            // 推进 scaled time 1秒（unscaled time 不推进）
            _scheduler.Update(1.0f, 0f);

            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void Delay_UnscaledMode_UsesUnscaledDeltaTime()
        {
            // Delay 1秒，Unscaled mode
            var task = _scheduler.Delay(1.0f, TimeMode.Unscaled);

            // 推进 unscaled time 1秒（scaled time 不推进）
            _scheduler.Update(0f, 1.0f);

            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void Delay_ScaledMode_IgnoresUnscaledDeltaTime()
        {
            // Delay 1秒，Scaled mode
            var task = _scheduler.Delay(1.0f, TimeMode.Scaled);

            // 只推进 unscaled time 1秒，scaled time 不动
            _scheduler.Update(0f, 1.0f);

            Assert.IsFalse(task.IsCompleted);
        }

        [Test]
        public void Delay_UnscaledMode_IgnoresScaledDeltaTime()
        {
            // Delay 1秒，Unscaled mode
            var task = _scheduler.Delay(1.0f, TimeMode.Unscaled);

            // 只推进 scaled time 1秒，unscaled time 不动
            _scheduler.Update(1.0f, 0f);

            Assert.IsFalse(task.IsCompleted);
        }

        [Test]
        public void Delay_UnscaledModeWithPhase_WorksInSpecifiedPhase()
        {
            // Delay 1秒，Unscaled mode, LateUpdate phase
            var task = _scheduler.Delay(1.0f, FramePhase.LateUpdate, TimeMode.Unscaled);

            // 在 Update 推进 unscaled time 1秒
            _scheduler.Update(0f, 1.0f);
            Assert.IsFalse(task.IsCompleted); // LateUpdate 未执行，task 未完成

            // 在 LateUpdate 执行
            _scheduler.LateUpdate(0f, 0f);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void Delay_MixedScaledAndUnscaled_IndependentQueues()
        {
            // 两个 Delay：一个 Scaled 1秒、一个 Unscaled 1秒
            var taskScaled = _scheduler.Delay(1.0f, TimeMode.Scaled);
            var taskUnscaled = _scheduler.Delay(1.0f, TimeMode.Unscaled);

            // 推进 scaled time 1秒（unscaled time 0）
            _scheduler.Update(1.0f, 0f);
            Assert.IsTrue(taskScaled.IsCompleted);
            Assert.IsFalse(taskUnscaled.IsCompleted);

            // 推进 unscaled time 1秒（scaled time 0）
            _scheduler.Update(0f, 1.0f);
            Assert.IsTrue(taskUnscaled.IsCompleted);
        }

        [Test]
        public void Delay_DefaultOverload_UsesScaledMode()
        {
            // Delay(seconds) 默认 Scaled mode
            var task = _scheduler.Delay(1.0f);

            // 推进 scaled time 1秒
            _scheduler.Update(1.0f, 0f);

            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void Delay_DefaultOverloadWithPhase_UsesScaledMode()
        {
            // Delay(seconds, phase) 默认 Scaled mode
            var task = _scheduler.Delay(1.0f, FramePhase.Update);

            // 推进 scaled time 1秒
            _scheduler.Update(1.0f, 0f);

            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void Delay_FixedUpdate_UnscaledTimeMode_UsesFixedUnscaledTime()
        {
            var task = _scheduler.Delay(0.03f, FramePhase.FixedUpdate, TimeMode.Unscaled);

            _scheduler.Update(1.0f, 10.0f);
            Assert.IsFalse(task.IsCompleted,
                "FixedUpdate unscaled delay must not use Update-phase unscaled elapsed time.");

            _scheduler.FixedUpdate(10.0f, 0.01f);
            Assert.IsFalse(task.IsCompleted,
                "FixedUpdate unscaled delay must not use scaled fixed elapsed time.");

            _scheduler.FixedUpdate(10.0f, 0.01f);
            Assert.IsFalse(task.IsCompleted);

            _scheduler.FixedUpdate(10.0f, 0.01f);
            Assert.IsTrue(task.IsCompleted);
        }
    }
}
