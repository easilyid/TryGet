using System.Collections.Generic;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.5 完整 demo：场景切换流程。串联 V0.5 三件套（Input / Config / Scene）+ 部分 V0.4 模块。
    ///
    /// 流程：
    /// 1. 启动加载 MainMenu 场景，注册武器表
    /// 2. 玩家按 Enter（SimulatePress）→ Procedure 检测到 → 切场景
    /// 3. Battle 场景加载 → 从 Config 读 sword 数据生成 Weapon → 播 BGM
    ///
    /// 验证 V0.5 全栈编排可用 + ModuleHost 拓扑排序在更大规模下仍稳定。
    /// </summary>
    [TestFixture]
    public class SceneFlowDemoTests
    {
        private class WeaponConfig
        {
            public string Name;
            public int Atk;
        }

        private class Weapon
        {
            public string Name;
            public int Atk;
        }

        // —— Procedure 1: MainMenu ——

        private class MainMenuProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public MainMenuProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("MainMenu.OnEnter");

                // 注册武器配置（Adapter 在生产环境会从 Luban 加载）
                var cfg = m.Host.Get<IConfigModule>();
                cfg.Register("weapon.sword", new WeaponConfig { Name = "Sword", Atk = 100 });
                cfg.Register("weapon.bow", new WeaponConfig { Name = "Bow", Atk = 60 });

                // 加载主菜单场景
                m.Host.Get<ISceneModule>().Load("MainMenu");

                // 播放主菜单 BGM
                m.Host.Get<IAudioModule>().Play("bgm/menu", AudioCategory.BGM);
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                // 检测 Enter 按下边沿 → 切场景
                var input = m.Host.Get<IInputModule>();
                if (input.WasPressedThisFrame("Confirm"))
                {
                    _log.Add("MainMenu.ConfirmPressed → Battle");
                    m.TransitionTo("Battle");
                }
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("MainMenu.OnExit");
                // 切场景前清理：卸载主菜单场景 + 停 BGM
                m.Host.Get<ISceneModule>().UnloadAll();
                m.Host.Get<IAudioModule>().StopAll(AudioCategory.BGM);
            }
        }

        // —— Procedure 2: Battle ——

        private class BattleProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public Weapon SpawnedWeapon;

            public BattleProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("Battle.OnEnter");

                // 加载战斗场景
                var scenes = m.Host.Get<ISceneModule>();
                scenes.Load("Battle");
                scenes.Load("BattleUI"); // additive 加载 UI 子场景
                scenes.SetActive("Battle");

                // 从 Config 拉武器数据，生成 Weapon 实例
                var swordCfg = m.Host.Get<IConfigModule>().Get<WeaponConfig>("weapon.sword");
                SpawnedWeapon = new Weapon { Name = swordCfg.Name, Atk = swordCfg.Atk };
                _log.Add($"Battle.WeaponSpawned name={SpawnedWeapon.Name} atk={SpawnedWeapon.Atk}");

                // 播放战斗 BGM
                m.Host.Get<IAudioModule>().Play("bgm/battle", AudioCategory.BGM);
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("Battle.OnExit");
            }
        }

        // —— 测试 1：完整流程 ——

        [Test]
        public void FullSceneFlow_MainMenuPressEnter_TransitionsToBattle()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            // 注册 V0.4 + V0.5 必需模块
            host.Register<IConfigModule>(new MemoryConfigModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IInputModule>(new MemoryInputModule());
            host.Register<ISceneModule>(new MemorySceneModule());
            var procedures = new ProcedureModule();
            host.Register<IProcedureModule>(procedures);

            host.Initialize();

            var battle = new BattleProcedure(log);
            procedures.AddProcedure("MainMenu", new MainMenuProcedure(log));
            procedures.AddProcedure("Battle", battle);

            procedures.Start("MainMenu");

            // 验证 MainMenu 初始状态
            Assert.AreEqual("MainMenu", host.Get<ISceneModule>().ActiveScene);
            Assert.IsTrue(host.Get<IAudioModule>().IsPlaying("bgm/menu"));

            // 帧 1：无输入，仍在 MainMenu
            host.Update(0.016f, 0.016f);
            Assert.AreEqual("MainMenu", procedures.CurrentState);

            // 帧 2：玩家按 Enter（模拟）
            var input = (MemoryInputModule)host.Get<IInputModule>();
            input.SimulatePress("Confirm");

            // 帧 3：Procedure 看到边沿 → 切 Battle
            host.Update(0.016f, 0.016f);

            Assert.AreEqual("Battle", procedures.CurrentState);
            Assert.AreEqual("Battle", host.Get<ISceneModule>().ActiveScene);
            Assert.AreEqual(2, host.Get<ISceneModule>().LoadedCount, "Battle + BattleUI 加法加载");

            // 验证 Config 读取成功 + 武器生成
            Assert.NotNull(battle.SpawnedWeapon);
            Assert.AreEqual("Sword", battle.SpawnedWeapon.Name);
            Assert.AreEqual(100, battle.SpawnedWeapon.Atk);

            // 验证音频切换：menu 已停 + battle 已播
            Assert.IsFalse(host.Get<IAudioModule>().IsPlaying("bgm/menu"));
            Assert.IsTrue(host.Get<IAudioModule>().IsPlaying("bgm/battle"));

            // 验证 Procedure 编排顺序
            CollectionAssert.Contains(log, "MainMenu.OnEnter");
            CollectionAssert.Contains(log, "MainMenu.ConfirmPressed → Battle");
            CollectionAssert.Contains(log, "MainMenu.OnExit");
            CollectionAssert.Contains(log, "Battle.OnEnter");
            CollectionAssert.Contains(log, "Battle.WeaponSpawned name=Sword atk=100");

            host.Shutdown();
        }

        // —— 测试 2：Input edge 在 Update 期间可消费，LateUpdate 后清空（IUpdateModule vs ILateUpdateModule 调度顺序）——

        [Test]
        public void InputEdge_ConsumedByProcedureUpdate_ClearedByLateUpdate()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            host.Register<IConfigModule>(new MemoryConfigModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IInputModule>(new MemoryInputModule());
            host.Register<ISceneModule>(new MemorySceneModule());
            var procedures = new ProcedureModule();
            host.Register<IProcedureModule>(procedures);
            host.Initialize();

            procedures.AddProcedure("MainMenu", new MainMenuProcedure(log));
            procedures.AddProcedure("Battle", new BattleProcedure(log));
            procedures.Start("MainMenu");

            var input = (MemoryInputModule)host.Get<IInputModule>();

            // 模拟"OS 输入"：在 host.Update 前 SimulatePress
            input.SimulatePress("Confirm");
            Assert.IsTrue(input.WasPressedThisFrame("Confirm"));

            // host.Update 期间 Procedure.OnUpdate 看到 edge → 切场景
            // MemoryInputModule 是 ILateUpdateModule（不参与 host.Update 调度），
            // 所以 Procedure (-200) 在 Update 阶段能看到 _pressedThisFrame
            host.Update(0.016f, 0.016f);

            Assert.AreEqual("Battle", procedures.CurrentState,
                "Input edge 在 Update 期间可被业务消费（设计修正 v0.5.0.3）");

            // host.LateUpdate 后清边
            host.LateUpdate(0.016f, 0.016f);
            Assert.IsFalse(input.WasPressedThisFrame("Confirm"), "LateUpdate 后清边");

            host.Shutdown();
        }
    }
}
