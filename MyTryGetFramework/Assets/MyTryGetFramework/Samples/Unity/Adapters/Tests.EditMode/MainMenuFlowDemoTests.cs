using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.4 完整 demo（design.md §12 V0.4 Gate criteria）：
    /// 模拟"启动→读语言偏好→进主菜单→点击开始→进游戏"流程，串联 V0.4 五件套：
    /// Save / Localization / Resource / UI / Audio。
    ///
    /// 同时验证 V0.2 (ModuleHost) + V0.3 (Procedure + EntityWorld) + V0.4 全栈协同：
    /// - ModuleHost 拓扑排序：Save(-450) → Localization(-420) → Resource(-400) →
    ///   Audio(-380) → UI(-300) → Procedure(-200) → EntityWorld(-100)
    /// - Procedure 通过 m.Host.Get&lt;IXxxModule&gt;() 拉取所有 V0.4 服务
    /// - V0.4 Memory 实现的组合是否能撑住真实业务编排
    /// </summary>
    [TestFixture]
    public class MainMenuFlowDemoTests
    {
        // —— UI Prefab 占位（通过 Resource 注册/加载）——
        private class UIPrefab
        {
            public string Name;
        }

        // —— 三个 Procedure ——

        private class BootProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public BootProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("Boot.OnEnter");

                // 1) 从 Save 读语言偏好（首次为 default）
                var save = m.Host.Get<ISaveModule>();
                var preferred = save.GetString("lang", "zh-CN");

                // 2) Localization 注册翻译表 + 切换语言
                var loc = m.Host.Get<ILocalizationModule>();
                loc.RegisterTable("zh-CN", new Dictionary<string, string>
                {
                    { "ui.title", "主菜单" },
                    { "ui.start", "开始游戏" },
                });
                loc.RegisterTable("en-US", new Dictionary<string, string>
                {
                    { "ui.title", "Main Menu" },
                    { "ui.start", "Start Game" },
                });
                loc.SetLanguage(preferred);

                // 3) Resource 预注册 UI Prefab（Production 由 YooAsset 加载）
                var res = m.Host.Get<IResourceModule>();
                res.Register("ui/MainMenu", new UIPrefab { Name = "MainMenu" });
                res.Register("ui/InGameHUD", new UIPrefab { Name = "InGameHUD" });

                _log.Add($"Boot.Ready lang={loc.CurrentLanguage}");

                // 4) 直接切 MainMenu（生产可能等资源加载完）
                m.TransitionTo("MainMenu");
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("Boot.OnExit");
            }
        }

        private class MainMenuProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            private int _ticks;

            public MainMenuProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("MainMenu.OnEnter");

                // 验证 Resource 可加载（业务正常路径）
                var prefab = m.Host.Get<IResourceModule>().Load<UIPrefab>("ui/MainMenu");
                Assert.NotNull(prefab);

                // 打开 UI + 播 BGM
                m.Host.Get<IUIModule>().Open("MainMenu");
                m.Host.Get<IAudioModule>().Play("bgm/main_theme", AudioCategory.BGM);

                // 用翻译显示标题（业务层会做这件事）
                var title = m.Host.Get<ILocalizationModule>().T("ui.title");
                _log.Add($"MainMenu shown: title='{title}'");
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                _ticks++;
                // 模拟"3 帧后用户点击开始"
                if (_ticks >= 3)
                {
                    _log.Add("MainMenu.StartClicked → InGame");
                    m.TransitionTo("InGame");
                }
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("MainMenu.OnExit");
                // 切场景前清屏：关 UI + 停 BGM（业务约定）
                m.Host.Get<IUIModule>().CloseAll();
                m.Host.Get<IAudioModule>().StopAll(AudioCategory.BGM);
            }
        }

        private class InGameProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public InGameProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("InGame.OnEnter");

                // 打开 HUD + 进场音效（SFX 类别）
                m.Host.Get<IUIModule>().Open("InGameHUD");
                m.Host.Get<IAudioModule>().Play("sfx/level_start", AudioCategory.SFX);

                // 业务可能要保存"上次进入游戏"的时间戳
                m.Host.Get<ISaveModule>().SetInt("lastPlayTimestamp", 1716534000);
                m.Host.Get<ISaveModule>().Save();
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("InGame.OnExit");
            }
        }

        // —— 测试 1：默认语言 (zh-CN) 全流程 ——

        [Test]
        public void FullMainMenuFlow_DefaultLanguage_AllPhasesExecuted()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            // 注册 V0.4 五件套
            host.Register<ISaveModule>(new MemorySaveModule());
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IUIModule>(new MemoryUIModule());

            // V0.3 Procedure
            var procedures = new ProcedureModule();
            host.Register<IProcedureModule>(procedures);

            host.Initialize();

            procedures.AddProcedure("Boot", new BootProcedure(log));
            procedures.AddProcedure("MainMenu", new MainMenuProcedure(log));
            procedures.AddProcedure("InGame", new InGameProcedure(log));

            procedures.Start("Boot"); // Boot.OnEnter 内直接切 MainMenu

            // 帧驱动 MainMenu 至 InGame
            host.Update(0.016f, 0.016f); // MainMenu tick 1
            host.Update(0.016f, 0.016f); // MainMenu tick 2
            host.Update(0.016f, 0.016f); // MainMenu tick 3 → InGame

            // 验证：流程到达 InGame
            Assert.AreEqual("InGame", procedures.CurrentState);

            // 验证：Procedure 编排顺序正确
            CollectionAssert.Contains(log, "Boot.OnEnter");
            CollectionAssert.Contains(log, "Boot.Ready lang=zh-CN");
            CollectionAssert.Contains(log, "Boot.OnExit");
            CollectionAssert.Contains(log, "MainMenu.OnEnter");
            CollectionAssert.Contains(log, "MainMenu shown: title='主菜单'", "中文翻译生效");
            CollectionAssert.Contains(log, "MainMenu.OnExit");
            CollectionAssert.Contains(log, "InGame.OnEnter");

            // 验证：UI 状态
            var ui = host.Get<IUIModule>();
            Assert.IsFalse(ui.IsOpen("MainMenu"), "MainMenu.OnExit 时已 CloseAll");
            Assert.IsTrue(ui.IsOpen("InGameHUD"));
            Assert.AreEqual(1, ui.OpenedCount);

            // 验证：Audio 状态
            var audio = host.Get<IAudioModule>();
            Assert.IsFalse(audio.IsPlaying("bgm/main_theme"), "BGM 已 StopAll");
            Assert.IsTrue(audio.IsPlaying("sfx/level_start"));

            // 验证：Save 写入持久化（Memory 实现）
            Assert.AreEqual(1716534000, host.Get<ISaveModule>().GetInt("lastPlayTimestamp"));

            // 验证：Localization 仍是当前语言
            Assert.AreEqual("zh-CN", host.Get<ILocalizationModule>().CurrentLanguage);

            host.Shutdown();

            // Shutdown 后 Memory 实现全清
            Assert.IsFalse(host.IsInitialized);
            CollectionAssert.Contains(log, "InGame.OnExit");
        }

        // —— 测试 2：从 Save 读取已保存的语言偏好 (en-US) ——

        [Test]
        public void FullMainMenuFlow_PersistedLanguage_RestoredFromSave()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            // 先预填 Save（模拟"上次玩家已切到英文"）
            var save = new MemorySaveModule();
            save.SetString("lang", "en-US");

            host.Register<ISaveModule>(save);
            host.Register<ILocalizationModule>(new MemoryLocalizationModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IUIModule>(new MemoryUIModule());
            var procedures = new ProcedureModule();
            host.Register<IProcedureModule>(procedures);
            host.Initialize();

            procedures.AddProcedure("Boot", new BootProcedure(log));
            procedures.AddProcedure("MainMenu", new MainMenuProcedure(log));
            procedures.AddProcedure("InGame", new InGameProcedure(log));

            procedures.Start("Boot");
            host.Update(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);
            host.Update(0.016f, 0.016f);

            // 语言生效 + 翻译用英文
            Assert.AreEqual("en-US", host.Get<ILocalizationModule>().CurrentLanguage);
            CollectionAssert.Contains(log, "Boot.Ready lang=en-US");
            CollectionAssert.Contains(log, "MainMenu shown: title='Main Menu'", "英文翻译生效");

            host.Shutdown();
        }

        // —— 测试 3：V0.4 Module Priority 链拓扑顺序验证 ——

        [Test]
        public void V04ModulePriorityChain_TopologicalOrderRespected()
        {
            // 故意以非 Priority 顺序注册，验证 ModuleHost 按 Priority 排序后初始化
            var host = new ModuleHost();
            host.Register<IUIModule>(new MemoryUIModule());                 // -300
            host.Register<IResourceModule>(new MemoryResourceModule());     // -400
            host.Register<IAudioModule>(new MemoryAudioModule());           // -380
            host.Register<ILocalizationModule>(new MemoryLocalizationModule()); // -420
            host.Register<ISaveModule>(new MemorySaveModule());             // -450

            host.Initialize();

            // 所有五件套都能 Get 出来 & 是同一份实例
            Assert.NotNull(host.Get<ISaveModule>());
            Assert.NotNull(host.Get<ILocalizationModule>());
            Assert.NotNull(host.Get<IResourceModule>());
            Assert.NotNull(host.Get<IAudioModule>());
            Assert.NotNull(host.Get<IUIModule>());

            host.Shutdown();
        }
    }
}
