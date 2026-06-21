using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// SystemClock Shutdown 后 Update 的行为文档化测试。
    ///
    /// 背景：SystemClock（IClock.cs:51-82）的 Shutdown 只清零各字段，【无 _shutdown 守卫】。
    /// 对比同文件 ConsoleLogger：Write 第一行 `if (_shutdown) return`，Shutdown 后静默丢弃。
    /// SystemClock.Shutdown 后再调 Update 会继续 ElapsedTime += dt、FrameCount++。
    ///
    /// 【条件性】bug：正常接入路径下 SystemClock 由 ModuleSystem.Update 驱动，而 ModuleSystem
    /// 在 IsInitialized==false（Shutdown 后）时 Update 直接抛异常（ModuleSystemErrorPathTests 已覆盖），
    /// 所以 SystemClock 不会被再 Update。此 bug 只在「脱离 ModuleSystem 独立使用 SystemClock」时触发。
    ///
    /// 本测试组用直接调 SystemClock.Update 的方式【文档化当前行为】，不预设应否修复——
    /// 由 owner 决定 SystemClock 是否应支持独立可用语义。若决定应对齐 ConsoleLogger 加 _shutdown 守卫，
    /// 则 ShutdownThenUpdate_IsNoOp 测试改为红灯驱动修复；当前先记录现状。
    /// </summary>
    [TestFixture]
    public class SystemClockShutdownBehaviorTests
    {
        /// <summary>
        /// 当前行为（现状记录）：Shutdown 后再 Update 会继续累加。
        /// 这与 ConsoleLogger 的 _shutdown 守卫设计不一致。
        /// 若 owner 决定 SystemClock 应独立可用且 Shutdown 后应静默，此测试应改为期望 no-op（红灯驱动修复）。
        /// </summary>
        [Test]
        public void Shutdown_ThenUpdate_CurrentlyAccumulates_DocumentedInconsistency()
        {
            var clock = new SystemClock();
            clock.OnInit(null);
            clock.Update(0.1f, 0.1f);
            Assert.AreEqual(0.1, clock.ElapsedTime, 0.0001, "前提：Shutdown 前正常累加");

            clock.Shutdown();
            Assert.AreEqual(0, clock.ElapsedTime, "Shutdown 清零");

            // 现状：继续累加（无 _shutdown 守卫）
            clock.Update(0.5f, 0.5f);

            // 记录当前行为：累加了。若加 _shutdown 守卫，此处应为 0。
            Assert.AreEqual(0.5, clock.ElapsedTime, 0.0001,
                "现状记录：SystemClock 无 _shutdown 守卫，Shutdown 后再 Update 仍累加。" +
                "这与 ConsoleLogger(有守卫)设计不一致；正常接入下 ModuleSystem 会先抛，不触达此处。");
        }

        /// <summary>
        /// 正常接入路径的契约验证：SystemClock 经 ModuleSystem 驱动时，Shutdown 后不会因 ModuleSystem
        /// 抛异常而再被 Update。此为绿灯测试，固化「正常路径不受条件性 bug 影响」。
        /// </summary>
        [Test]
        public void ViaModuleSystem_ShutdownThenUpdate_ModuleSystemThrowsBeforeClockAccumulates()
        {
            var host = new ModuleSystem();
            host.Register<IClock>(new SystemClock());
            host.Initialize();

            host.Update(0.1f, 0.1f);
            Assert.AreEqual(0.1, host.Get<IClock>().ElapsedTime, 0.0001, "前提：正常累加");

            host.Shutdown();
            Assert.AreEqual(0, host.Get<IClock>().ElapsedTime, "Shutdown 清零");

            // ModuleSystem.Shutdown 后再 Update 应抛，SystemClock 不会被再驱动
            Assert.Throws<InvalidOperationException>(() => host.Update(0.5f, 0.5f));

            // 因此 SystemClock 不会因 ModuleSystem 路径而累加 —— 条件性 bug 在正常接入下不触发
            Assert.AreEqual(0, host.Get<IClock>().ElapsedTime, 0.0001,
                "正常接入：ModuleSystem 先抛，SystemClock 未被再 Update，不累加");
        }
    }
}
