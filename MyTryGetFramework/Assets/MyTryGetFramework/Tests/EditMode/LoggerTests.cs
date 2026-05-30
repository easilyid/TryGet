using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// ILogger / ConsoleLogger 测试。
    /// </summary>
    [TestFixture]
    public class LoggerTests
    {
        [Test]
        public void ConsoleLogger_DefaultLevel_AcceptsAllFromDebug()
        {
            var log = new ConsoleLogger { CaptureToMemory = true };
            log.Trace("trace-msg");
            log.Debug("debug-msg");
            log.Info("info-msg");

            var entries = log.GetCapturedEntries();
            Assert.AreEqual(2, entries.Count, "Trace 应低于 Debug 被过滤");
            Assert.AreEqual(LogLevel.Debug, entries[0].level);
            Assert.AreEqual(LogLevel.Info, entries[1].level);
        }

        [Test]
        public void ConsoleLogger_TraceLevel_AcceptsTrace()
        {
            var log = new ConsoleLogger { MinimumLevel = LogLevel.Trace, CaptureToMemory = true };
            log.Trace("very-detailed");

            var entries = log.GetCapturedEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Trace, entries[0].level);
            Assert.AreEqual("very-detailed", entries[0].message);
        }

        [Test]
        public void ConsoleLogger_ErrorLevel_FiltersBelow()
        {
            var log = new ConsoleLogger { MinimumLevel = LogLevel.Error, CaptureToMemory = true };
            log.Trace("trace");
            log.Debug("debug");
            log.Info("info");
            log.Warn("warn");
            log.Error("error");

            var entries = log.GetCapturedEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Error, entries[0].level);
        }

        [Test]
        public void ConsoleLogger_ErrorWithException_IncludesException()
        {
            var log = new ConsoleLogger { CaptureToMemory = true };
            log.Error("op-failed", new InvalidOperationException("boom"));

            var entries = log.GetCapturedEntries();
            Assert.AreEqual(1, entries.Count);
            StringAssert.Contains("op-failed", entries[0].message);
            StringAssert.Contains("InvalidOperationException", entries[0].message);
            StringAssert.Contains("boom", entries[0].message);
        }

        [Test]
        public void ConsoleLogger_ErrorWithNullException_NoCrash()
        {
            var log = new ConsoleLogger { CaptureToMemory = true };
            log.Error("standalone-error", null);

            var entries = log.GetCapturedEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("standalone-error", entries[0].message);
        }

        [Test]
        public void ConsoleLogger_OnLogHook_FiresPerWrite()
        {
            int hookCount = 0;
            LogLevel? lastLevel = null;
            var log = new ConsoleLogger();
            log.OnLog = (lvl, msg) => { hookCount++; lastLevel = lvl; };

            log.Info("first");
            log.Error("second");

            Assert.AreEqual(2, hookCount);
            Assert.AreEqual(LogLevel.Error, lastLevel);
        }

        [Test]
        public void ConsoleLogger_AfterShutdown_SilentlyIgnoresWrites()
        {
            var log = new ConsoleLogger { CaptureToMemory = true };
            log.Info("before");
            log.Shutdown();

            Assert.DoesNotThrow(() => log.Info("after"));
        }

        [Test]
        public void ConsoleLogger_RegisteredAsILogger_RetrievableFromHost()
        {
            var host = new ModuleHost();
            var logger = new ConsoleLogger();
            host.Register<ILogger>(logger);
            host.Initialize();

            Assert.AreSame(logger, host.Get<ILogger>());

            host.Shutdown();
        }

        [Test]
        public void ConsoleLogger_Priority_NegativeOneThousand()
        {
            Assert.AreEqual(-1000, new ConsoleLogger().Priority);
        }
    }
}
