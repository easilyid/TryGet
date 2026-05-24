using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.7 Iter 2 — ILogger / ConsoleLogger / LogModuleAdapter 测试。
    /// </summary>
    [TestFixture]
    public class LoggerTests
    {
        // ===== ConsoleLogger 基础 =====

        [Test]
        public void ConsoleLogger_DefaultLevel_AcceptsAllFromDebug()
        {
            var log = new ConsoleLogger { CaptureToMemory = true };
            // MinimumLevel 默认 Debug，Trace 应被过滤
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

        // ===== ModuleHost 集成 =====

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
            // Logger 应极早 OnInit（其他 Module 在自己 OnInit 中能 Get<ILogger>）
            Assert.AreEqual(-1000, new ConsoleLogger().Priority);
        }

        // ===== LogModuleAdapter =====

        [Test]
        public void LogModuleAdapter_BridgesAllLevels()
        {
            #pragma warning disable CS0618 // Obsolete 后压制（V0.7 测试仍需访问 ILogModule 验证 bridge）
            var legacy = new ConsoleLogModule { CaptureToMemory = true };
            var adapter = new LogModuleAdapter(legacy);

            adapter.Debug("d");
            adapter.Info("i");
            adapter.Warn("w");
            adapter.Error("e");

            var entries = legacy.GetCapturedEntries();
            #pragma warning restore CS0618
            Assert.AreEqual(4, entries.Count);
            Assert.AreEqual(LogLevel.Debug, entries[0].level);
            Assert.AreEqual(LogLevel.Info, entries[1].level);
            Assert.AreEqual(LogLevel.Warn, entries[2].level);
            Assert.AreEqual(LogLevel.Error, entries[3].level);
        }

        [Test]
        public void LogModuleAdapter_TraceBridgesToDebugWithPrefix()
        {
            #pragma warning disable CS0618
            var legacy = new ConsoleLogModule { CaptureToMemory = true };
            var adapter = new LogModuleAdapter(legacy);

            adapter.Trace("detailed");

            var entries = legacy.GetCapturedEntries();
            #pragma warning restore CS0618
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Debug, entries[0].level, "Trace 桥接为 Debug");
            StringAssert.StartsWith("[TRACE] ", entries[0].message);
        }

        [Test]
        public void LogModuleAdapter_MinimumLevel_SyncsToInner()
        {
            #pragma warning disable CS0618
            var legacy = new ConsoleLogModule();
            var adapter = new LogModuleAdapter(legacy);

            adapter.MinimumLevel = LogLevel.Error;
            Assert.AreEqual(LogLevel.Error, legacy.MinimumLevel,
                "Adapter.MinimumLevel 应同步到 inner ILogModule");

            legacy.MinimumLevel = LogLevel.Warn;
            Assert.AreEqual(LogLevel.Warn, adapter.MinimumLevel,
                "Inner.MinimumLevel 改变应被 Adapter 读到");
            #pragma warning restore CS0618
        }

        [Test]
        public void LogModuleAdapter_NullInner_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LogModuleAdapter(null));
        }

        [Test]
        public void LogModuleAdapter_ErrorWithException_Forwarded()
        {
            #pragma warning disable CS0618
            var legacy = new ConsoleLogModule { CaptureToMemory = true };
            var adapter = new LogModuleAdapter(legacy);

            adapter.Error("wrapper-error", new InvalidOperationException("inner"));

            var entries = legacy.GetCapturedEntries();
            #pragma warning restore CS0618
            Assert.AreEqual(1, entries.Count);
            StringAssert.Contains("wrapper-error", entries[0].message);
            StringAssert.Contains("inner", entries[0].message);
        }
    }
}
