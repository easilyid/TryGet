using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ConsoleLogModule（ILogModule 默认实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class LogModuleTests
    {
        [Test]
        public void Priority_IsVeryLow_SoOtherModulesCanLogInOnInit()
        {
            var log = new ConsoleLogModule();
            Assert.AreEqual(-1000, log.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var log = new ConsoleLogModule();
            Assert.AreEqual(0, log.DependsOn.Count);
        }

        [Test]
        public void DefaultMinimumLevel_IsDebug()
        {
            var log = new ConsoleLogModule();
            Assert.AreEqual(LogLevel.Debug, log.MinimumLevel);
        }

        [Test]
        public void Debug_BelowMinimumLevel_IsDropped()
        {
            var log = new ConsoleLogModule { MinimumLevel = LogLevel.Warn, CaptureToMemory = true };
            log.Debug("d");
            log.Info("i");
            Assert.AreEqual(0, log.GetCapturedEntries().Count);
        }

        [Test]
        public void Warn_AtOrAboveMinimumLevel_IsKept()
        {
            var log = new ConsoleLogModule { MinimumLevel = LogLevel.Warn, CaptureToMemory = true };
            log.Warn("w");
            log.Error("e");
            Assert.AreEqual(2, log.GetCapturedEntries().Count);
            Assert.AreEqual(LogLevel.Warn, log.GetCapturedEntries()[0].level);
            Assert.AreEqual(LogLevel.Error, log.GetCapturedEntries()[1].level);
        }

        [Test]
        public void OnLog_Hook_IsInvokedForEveryWrittenEntry()
        {
            var log = new ConsoleLogModule { MinimumLevel = LogLevel.Info };
            int count = 0;
            LogLevel? lastLevel = null;
            string lastMsg = null;
            log.OnLog = (lvl, msg) => { count++; lastLevel = lvl; lastMsg = msg; };

            log.Debug("dropped");  // 被 MinimumLevel 过滤掉
            log.Info("hello");

            Assert.AreEqual(1, count);
            Assert.AreEqual(LogLevel.Info, lastLevel);
            Assert.AreEqual("hello", lastMsg);
        }

        [Test]
        public void CaptureToMemory_False_DoesNotAccumulate()
        {
            var log = new ConsoleLogModule { CaptureToMemory = false };
            log.Info("a");
            log.Info("b");
            Assert.AreEqual(0, log.GetCapturedEntries().Count);
        }

        [Test]
        public void ErrorWithException_AppendsExceptionString()
        {
            var log = new ConsoleLogModule { CaptureToMemory = true };
            var ex = new InvalidOperationException("boom");

            log.Error("oops", ex);

            Assert.AreEqual(1, log.GetCapturedEntries().Count);
            Assert.AreEqual(LogLevel.Error, log.GetCapturedEntries()[0].level);
            StringAssert.Contains("oops", log.GetCapturedEntries()[0].message);
            StringAssert.Contains("InvalidOperationException", log.GetCapturedEntries()[0].message);
        }

        [Test]
        public void ErrorWithNullException_FallsBackToMessageOnly()
        {
            var log = new ConsoleLogModule { CaptureToMemory = true };
            log.Error("only-msg", null);

            Assert.AreEqual(1, log.GetCapturedEntries().Count);
            Assert.AreEqual("only-msg", log.GetCapturedEntries()[0].message);
        }

        [Test]
        public void Shutdown_ClearsCapturedAndOnLog()
        {
            var log = new ConsoleLogModule { CaptureToMemory = true };
            log.OnLog = (lvl, msg) => { };
            log.Info("x");
            Assert.AreEqual(1, log.GetCapturedEntries().Count);

            log.Shutdown();

            Assert.AreEqual(0, log.GetCapturedEntries().Count);
            Assert.IsNull(log.OnLog);
        }

        [Test]
        public void RuntimeChangeMinimumLevel_TakesEffectImmediately()
        {
            var log = new ConsoleLogModule { MinimumLevel = LogLevel.Debug, CaptureToMemory = true };
            log.Debug("a");
            log.MinimumLevel = LogLevel.Error;
            log.Debug("b");
            log.Error("c");

            Assert.AreEqual(2, log.GetCapturedEntries().Count);
            Assert.AreEqual("a", log.GetCapturedEntries()[0].message);
            Assert.AreEqual("c", log.GetCapturedEntries()[1].message);
        }

        [Test]
        public void IntegratesWithModuleHost_OnInitCalled()
        {
            var host = new ModuleHost();
            var log = new ConsoleLogModule();
            host.Register<ILogModule>(log);
            host.Initialize();

            // 验证 host 能 Get 到
            Assert.AreSame(log, host.Get<ILogModule>());

            // 验证 Shutdown 路径
            host.Shutdown();
        }

        // —— 来自 Stage-2 review 必改：Shutdown 后调用 Log 不应崩 ——

        [Test]
        public void Log_AfterShutdown_IsSilentlyDropped()
        {
            var log = new ConsoleLogModule { CaptureToMemory = true };
            log.Info("before");
            log.Shutdown();

            // Shutdown 后调用不应抛
            Assert.DoesNotThrow(() => log.Info("after"));
            Assert.DoesNotThrow(() => log.Error("after", new InvalidOperationException()));
            Assert.AreEqual(0, log.GetCapturedEntries().Count, "Shutdown 后不再回填 captured");
        }

        [Test]
        public void OnInit_AfterPreviousShutdown_RestoresWritability()
        {
            // Shutdown 后再 OnInit（例如 ModuleHost 失败回滚后修复重试）应可写
            var log = new ConsoleLogModule { CaptureToMemory = true };
            log.Shutdown();
            log.OnInit(null);

            log.Info("reborn");

            Assert.AreEqual(1, log.GetCapturedEntries().Count);
            Assert.AreEqual("reborn", log.GetCapturedEntries()[0].message);
        }
    }
}
