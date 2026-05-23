using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemoryLocalizationModule（ILocalizationModule 实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemoryLocalizationModuleTests
    {
        private static Dictionary<string, string> ZhTable() => new Dictionary<string, string>
        {
            { "ui.title", "标题" },
            { "ui.confirm", "确认" },
            { "ui.cancel", "取消" },
        };

        private static Dictionary<string, string> EnTable() => new Dictionary<string, string>
        {
            { "ui.title", "Title" },
            { "ui.confirm", "OK" },
            { "ui.cancel", "Cancel" },
        };

        [Test]
        public void Priority_BetweenSaveAndResource()
        {
            var l = new MemoryLocalizationModule();
            Assert.AreEqual(-420, l.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var l = new MemoryLocalizationModule();
            Assert.AreEqual(0, l.DependsOn.Count);
        }

        // —— 初始状态 ——

        [Test]
        public void Initial_NoLanguage_NoTables()
        {
            var l = new MemoryLocalizationModule();
            Assert.IsNull(l.CurrentLanguage);
            Assert.AreEqual(0, l.AvailableLanguages.Count);
            Assert.AreEqual(0, l.RegisteredCount);
        }

        [Test]
        public void T_BeforeSetLanguage_ReturnsKey()
        {
            var l = new MemoryLocalizationModule();
            Assert.AreEqual("ui.title", l.T("ui.title"), "未 SetLanguage 时 T(key) 返回 key 本身");
            Assert.AreEqual("fallback", l.T("ui.title", "fallback"), "T(key, default) 返回 default");
        }

        // —— RegisterTable ——

        [Test]
        public void RegisterTable_NewLanguage_AddsToList()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());

            Assert.AreEqual(1, l.AvailableLanguages.Count);
            Assert.AreEqual("zh-CN", l.AvailableLanguages[0]);
        }

        [Test]
        public void RegisterTable_NullLanguage_Throws()
        {
            var l = new MemoryLocalizationModule();
            Assert.Throws<ArgumentException>(() => l.RegisterTable(null, ZhTable()));
            Assert.Throws<ArgumentException>(() => l.RegisterTable("", ZhTable()));
        }

        [Test]
        public void RegisterTable_NullTable_Throws()
        {
            var l = new MemoryLocalizationModule();
            Assert.Throws<ArgumentNullException>(() => l.RegisterTable("zh-CN", null));
        }

        [Test]
        public void RegisterTable_NullKeyInTable_Throws()
        {
            var l = new MemoryLocalizationModule();
            var bad = new Dictionary<string, string> { { "", "value" } };
            Assert.Throws<ArgumentException>(() => l.RegisterTable("zh-CN", bad));
        }

        [Test]
        public void RegisterTable_NullValueInTable_Throws()
        {
            var l = new MemoryLocalizationModule();
            var bad = new Dictionary<string, string> { { "key", null } };
            Assert.Throws<ArgumentException>(() => l.RegisterTable("zh-CN", bad));
        }

        [Test]
        public void RegisterTable_DuplicateLanguage_Overwrites()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", new Dictionary<string, string> { { "k", "v1" } });
            l.RegisterTable("zh-CN", new Dictionary<string, string> { { "k", "v2" } });

            // AvailableLanguages 仍只一个
            Assert.AreEqual(1, l.AvailableLanguages.Count);

            l.SetLanguage("zh-CN");
            Assert.AreEqual("v2", l.T("k"), "覆盖后查询应得到新值");
        }

        [Test]
        public void RegisterTable_OverwriteCurrent_RefreshesCurrentTable()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", new Dictionary<string, string> { { "k", "v1" } });
            l.SetLanguage("zh-CN");
            Assert.AreEqual("v1", l.T("k"));

            // 覆盖 current 的表 → 应立即反映
            l.RegisterTable("zh-CN", new Dictionary<string, string> { { "k", "v2" } });
            Assert.AreEqual("v2", l.T("k"));
        }

        [Test]
        public void RegisterTable_ShallowCopy_ExternalMutationIsolated()
        {
            var l = new MemoryLocalizationModule();
            var src = new Dictionary<string, string> { { "k", "v1" } };
            l.RegisterTable("zh-CN", src);
            l.SetLanguage("zh-CN");

            // 外部修改原 dict 不应影响内部
            src["k"] = "MUTATED";
            src["new"] = "EXTRA";

            Assert.AreEqual("v1", l.T("k"));
            Assert.AreEqual("new", l.T("new"), "原 dict 新增的 key 不应进入内部");
        }

        // —— SetLanguage ——

        [Test]
        public void SetLanguage_Registered_BecomesCurrent()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");

            Assert.AreEqual("zh-CN", l.CurrentLanguage);
            Assert.AreEqual(3, l.RegisteredCount);
        }

        [Test]
        public void SetLanguage_Unregistered_Throws()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());

            var ex = Assert.Throws<InvalidOperationException>(() => l.SetLanguage("ja-JP"));
            StringAssert.Contains("not registered", ex.Message);
            // current 未被破坏
            Assert.IsNull(l.CurrentLanguage);
        }

        [Test]
        public void SetLanguage_NullOrEmpty_Throws()
        {
            var l = new MemoryLocalizationModule();
            Assert.Throws<ArgumentException>(() => l.SetLanguage(null));
            Assert.Throws<ArgumentException>(() => l.SetLanguage(""));
        }

        [Test]
        public void SetLanguage_Switch_QueryReflectsNew()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.RegisterTable("en-US", EnTable());

            l.SetLanguage("zh-CN");
            Assert.AreEqual("标题", l.T("ui.title"));

            l.SetLanguage("en-US");
            Assert.AreEqual("Title", l.T("ui.title"));
        }

        // —— T / TryGet ——

        [Test]
        public void T_Found_ReturnsTranslation()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");
            Assert.AreEqual("标题", l.T("ui.title"));
        }

        [Test]
        public void T_NotFound_ReturnsKey()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");
            Assert.AreEqual("ui.missing", l.T("ui.missing"), "漏译应返回 key 本身");
        }

        [Test]
        public void TWithDefault_NotFound_ReturnsDefault()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");
            Assert.AreEqual("默认", l.T("ui.missing", "默认"));
        }

        [Test]
        public void T_NullKey_ReturnsKey()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");
            Assert.IsNull(l.T(null), "null key 走 fallback");
            Assert.AreEqual("D", l.T(null, "D"));
        }

        [Test]
        public void TryGet_Found_ReturnsTrue()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");

            Assert.IsTrue(l.TryGet("ui.title", out var v));
            Assert.AreEqual("标题", v);
        }

        [Test]
        public void TryGet_NotFound_ReturnsFalse()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");

            Assert.IsFalse(l.TryGet("ui.missing", out var v));
            Assert.IsNull(v);
        }

        [Test]
        public void TryGet_BeforeSetLanguage_ReturnsFalse()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());

            Assert.IsFalse(l.TryGet("ui.title", out var v));
            Assert.IsNull(v);
        }

        // —— Shutdown ——

        [Test]
        public void Shutdown_ClearsEverything()
        {
            var l = new MemoryLocalizationModule();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");

            l.Shutdown();

            Assert.IsNull(l.CurrentLanguage);
            Assert.AreEqual(0, l.AvailableLanguages.Count);
            Assert.AreEqual(0, l.RegisteredCount);
            Assert.AreEqual("ui.title", l.T("ui.title"), "Shutdown 后 T 退回 key fallback");
        }

        // —— ModuleHost 集成 + 与 Save 协同 ——

        [Test]
        public void IntegratesWithModuleHost_BusinessCanTranslate()
        {
            var host = new ModuleHost();
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());
            host.Initialize();

            var l = host.Get<ILocalizationModule>();
            l.RegisterTable("zh-CN", ZhTable());
            l.SetLanguage("zh-CN");

            Assert.AreEqual("标题", l.T("ui.title"));

            host.Shutdown();
        }

        [Test]
        public void CoexistsWithSaveModule_LanguagePersistedByBusiness()
        {
            // 模拟"启动时从 Save 读语言偏好"流程
            var host = new ModuleHost();
            host.Register<ISaveModule>(new MemorySaveModule());
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());
            host.Initialize();

            // 业务先存"偏好语言"
            host.Get<ISaveModule>().SetString("lang", "en-US");

            // 业务在游戏启动后注册翻译表 + 按 Save 切换
            var l = host.Get<ILocalizationModule>();
            l.RegisterTable("zh-CN", ZhTable());
            l.RegisterTable("en-US", EnTable());

            var preferred = host.Get<ISaveModule>().GetString("lang", "zh-CN");
            l.SetLanguage(preferred);

            Assert.AreEqual("Title", l.T("ui.title"));

            host.Shutdown();
        }
    }
}
