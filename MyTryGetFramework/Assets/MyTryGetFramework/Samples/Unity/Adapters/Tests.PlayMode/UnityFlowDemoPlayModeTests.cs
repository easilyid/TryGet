using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// V0.6 起点：跨 Adapter 综合 PlayMode demo（Plan agent V0.5 关门评审推荐）。
    ///
    /// 流程：Boot Procedure → MainMenu Procedure → Battle Procedure，
    /// 串联 4 个真实 Unity Adapter（Audio / Input / Save / UI）+ Memory(Scene / Config) + Procedure。
    /// 证明 V0.5 完整 Adapter 套件在真实 Unity 运行时下能编排，不爆。
    ///
    /// 区别于 V0.4 MainMenuFlowDemoTests（全 Memory）/ V0.5 SceneFlowDemoTests（全 Memory），
    /// 本 demo 验证 Adapter 与 Core 接口的契约一致性 + 跨 Adapter 拓扑稳定性。
    /// </summary>
    [TestFixture]
    public class UnityFlowDemoPlayModeTests
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

        private Keyboard _keyboard;
        private const string TestKeyPrefix = "TryGet_V06Demo_";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        }

        private AudioClip MakeClip(float seconds = 1f)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(seconds * sampleRate);
            var clip = AudioClip.Create("DemoClip", sampleCount, 1, sampleRate, false);
            clip.SetData(new float[sampleCount], 0);
            return clip;
        }

        private GameObject MakeUIPrefab(string name)
        {
            return new GameObject(name, typeof(RectTransform), typeof(Image));
        }

        // —— Procedures ——

        private class BootProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public BootProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("Boot.OnEnter");

                // 1) 从 PlayerPrefs 读语言偏好（首次为 default）
                var save = m.Host.Get<ISaveModule>();
                var lang = save.GetString("TryGet_V06Demo_lang", "zh-CN");

                // 2) 注册武器配置
                var cfg = m.Host.Get<IConfigModule>();
                cfg.Register("weapon.sword", new WeaponConfig { Name = "Sword", Atk = 100 });

                _log.Add($"Boot.Ready lang={lang}");

                // 3) 立即切 MainMenu
                m.TransitionTo("MainMenu");
            }

            public override void OnExit(IProcedureModule m) => _log.Add("Boot.OnExit");
        }

        private class MainMenuProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public MainMenuProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("MainMenu.OnEnter");

                // 加载场景（Memory，不实际加载 Unity 场景）+ 打开 UGUI + 播 UnityAudio BGM
                m.Host.Get<ISceneModule>().Load("MainMenu");
                m.Host.Get<IUIModule>().Open("MainMenu");
                m.Host.Get<IAudioModule>().Play("bgm/menu", AudioCategory.BGM);
            }

            public override void OnUpdate(IProcedureModule m, float dt, float ud)
            {
                if (m.Host.Get<IInputModule>().WasPressedThisFrame("Confirm"))
                {
                    _log.Add("MainMenu.Confirm → Battle");
                    m.TransitionTo("Battle");
                }
            }

            public override void OnExit(IProcedureModule m)
            {
                _log.Add("MainMenu.OnExit");
                // 清理：场景卸载 + UI 关闭 + BGM 停
                m.Host.Get<ISceneModule>().UnloadAll();
                m.Host.Get<IUIModule>().CloseAll();
                m.Host.Get<IAudioModule>().StopAll(AudioCategory.BGM);
            }
        }

        private class BattleProcedure : ProcedureBase
        {
            private readonly List<string> _log;
            public Weapon SpawnedWeapon;

            public BattleProcedure(List<string> log) { _log = log; }

            public override void OnEnter(IProcedureModule m)
            {
                _log.Add("Battle.OnEnter");

                m.Host.Get<ISceneModule>().Load("Battle");
                m.Host.Get<IUIModule>().Open("BattleHUD");

                // 读武器配置生成实例
                var cfg = m.Host.Get<IConfigModule>().Get<WeaponConfig>("weapon.sword");
                SpawnedWeapon = new Weapon { Name = cfg.Name, Atk = cfg.Atk };
                _log.Add($"Battle.WeaponSpawned name={SpawnedWeapon.Name} atk={SpawnedWeapon.Atk}");

                m.Host.Get<IAudioModule>().Play("bgm/battle", AudioCategory.BGM);

                // 持久化"进入战斗时间戳"
                m.Host.Get<ISaveModule>().SetInt("TryGet_V06Demo_lastBattleAt", 1716534000);
                m.Host.Get<ISaveModule>().Save();
            }
        }

        [UnityTest]
        public IEnumerator FullUnityFlow_BootMainMenuBattle_CrossAdapterOrchestration()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            // 全套注册：4 真实 Unity Adapter + Memory(Scene + Config) + Procedure
            host.Register<IConfigModule>(new MemoryConfigModule());
            host.Register<ISaveModule>(new PlayerPrefsSaveModule());
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new UnityAudioModule(poolSize: 4));
            host.Register<IInputModule>(new UnityInputModule());
            host.Register<IUIModule>(new UGUIUIModule());
            host.Register<ISceneModule>(new MemorySceneModule());
            var procedures = new ProcedureModule();
            host.Register<IProcedureModule>(procedures);

            host.Initialize();
            yield return null;

            // 准备资源：UI prefab + AudioClip
            var menuPrefab = MakeUIPrefab("MainMenu");
            var hudPrefab = MakeUIPrefab("BattleHUD");
            var ui = (UGUIUIModule)host.Get<IUIModule>();
            ui.RegisterPrefab("MainMenu", menuPrefab);
            ui.RegisterPrefab("BattleHUD", hudPrefab);

            var audio = (UnityAudioModule)host.Get<IAudioModule>();
            audio.RegisterClip("bgm/menu", MakeClip());
            audio.RegisterClip("bgm/battle", MakeClip());

            // 注册 Input action
            var input = (UnityInputModule)host.Get<IInputModule>();
            input.RegisterButton("Confirm", "<Keyboard>/enter");

            var battle = new BattleProcedure(log);
            procedures.AddProcedure("Boot", new BootProcedure(log));
            procedures.AddProcedure("MainMenu", new MainMenuProcedure(log));
            procedures.AddProcedure("Battle", battle);

            procedures.Start("Boot"); // Boot.OnEnter 直接切 MainMenu

            // 验证 MainMenu 初始状态：场景 + UI + BGM 都生效
            Assert.AreEqual("MainMenu", procedures.CurrentState);
            Assert.IsTrue(host.Get<ISceneModule>().IsLoaded("MainMenu"));
            Assert.IsTrue(host.Get<IUIModule>().IsOpen("MainMenu"));
            Assert.IsTrue(host.Get<IAudioModule>().IsPlaying("bgm/menu"));

            // 帧 1：无输入
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);
            Assert.AreEqual("MainMenu", procedures.CurrentState);

            // 模拟"OS 输入"：Enter 按下
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Enter));
            InputSystem.Update();

            // 帧 2：Procedure 看到 edge → 切 Battle
            host.Update(0.016f, 0.016f);
            host.LateUpdate(0.016f, 0.016f);

            yield return null;

            // 验证 Battle 状态
            Assert.AreEqual("Battle", procedures.CurrentState, "Confirm 按下后应切 Battle");
            Assert.IsTrue(host.Get<ISceneModule>().IsLoaded("Battle"));
            Assert.IsFalse(host.Get<ISceneModule>().IsLoaded("MainMenu"), "MainMenu 已 UnloadAll");
            Assert.IsTrue(host.Get<IUIModule>().IsOpen("BattleHUD"));
            Assert.IsFalse(host.Get<IUIModule>().IsOpen("MainMenu"));
            Assert.IsTrue(host.Get<IAudioModule>().IsPlaying("bgm/battle"));
            Assert.IsFalse(host.Get<IAudioModule>().IsPlaying("bgm/menu"));

            // 验证武器生成
            Assert.NotNull(battle.SpawnedWeapon);
            Assert.AreEqual("Sword", battle.SpawnedWeapon.Name);
            Assert.AreEqual(100, battle.SpawnedWeapon.Atk);

            // 验证 PlayerPrefs 真持久化（用第二个 instance 读）
            Assert.AreEqual(1716534000, PlayerPrefs.GetInt("TryGet_V06Demo_lastBattleAt"));

            // 验证编排顺序
            CollectionAssert.Contains(log, "Boot.OnEnter");
            CollectionAssert.Contains(log, "Boot.Ready lang=zh-CN");
            CollectionAssert.Contains(log, "Boot.OnExit");
            CollectionAssert.Contains(log, "MainMenu.OnEnter");
            CollectionAssert.Contains(log, "MainMenu.Confirm → Battle");
            CollectionAssert.Contains(log, "MainMenu.OnExit");
            CollectionAssert.Contains(log, "Battle.OnEnter");
            CollectionAssert.Contains(log, "Battle.WeaponSpawned name=Sword atk=100");

            host.Shutdown();
            Object.Destroy(menuPrefab);
            Object.Destroy(hudPrefab);
        }

        [Test]
        public void RegistrationOrder_AllUnityAdapters_NoCircularDeps()
        {
            // 验证 7 个 Module 注册顺序任意，ModuleHost 按 Priority 拓扑排序无环
            var host = new ModuleHost();
            host.Register<IUIModule>(new UGUIUIModule());           // -300
            host.Register<IConfigModule>(new MemoryConfigModule()); // -460
            host.Register<IAudioModule>(new UnityAudioModule(2));   // -380
            host.Register<ISceneModule>(new MemorySceneModule());   // -250
            host.Register<IInputModule>(new UnityInputModule());    // -350
            host.Register<ISaveModule>(new PlayerPrefsSaveModule());// -450
            host.Register<IResourceModule>(new MemoryResourceModule()); // -400

            host.Initialize();

            Assert.NotNull(host.Get<IConfigModule>());
            Assert.NotNull(host.Get<ISaveModule>());
            Assert.NotNull(host.Get<IResourceModule>());
            Assert.NotNull(host.Get<IAudioModule>());
            Assert.NotNull(host.Get<IInputModule>());
            Assert.NotNull(host.Get<IUIModule>());
            Assert.NotNull(host.Get<ISceneModule>());

            host.Shutdown();
        }
    }
}
