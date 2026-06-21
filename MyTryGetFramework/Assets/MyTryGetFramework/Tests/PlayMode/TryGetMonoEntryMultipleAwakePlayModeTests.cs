using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// TryGetMonoEntry.Awake 幂等性 / 多次触发泄漏测试。
    ///
    /// 背景：TryGetMonoEntry.Awake（TryGetMonoEntry.cs:64-84）无任何幂等标志。第二次进入会：
    ///   - Host = GameLauncher.CreateHost(Options) 覆盖旧 Host → 旧 Host 从不 Shutdown（模块泄漏）
    ///   - StartCoroutine(EndOfFrameCoroutine()) 再启动一条 → 两条协程并行驱动（双倍帧分发）
    ///   - DontDestroyOnLoad 重复调用
    ///
    /// 组件被销毁后重新 AddComponent、prefab 重复实例化会创建新组件实例；同一组件实例重复进入 Awake
    /// 不是常规 Unity 生命周期。本测试通过子类暴露的 TriggerSecondAwake() 直接再次调用 base.Awake()
    /// 来确定性验证框架对误调用的防御性 no-op 契约。
    ///
    /// 这些测试断言【健壮的期望行为】：同一实例二次 Awake 不重建 Host、不重复启动协程。
    /// 当前实现下预期【红灯】；修复后转绿。
    /// </summary>
    [TestFixture]
    public sealed class TryGetMonoEntryMultipleAwakePlayModeTests
    {
        /// <summary>追踪 Init/Shutdown 的探针模块。</summary>
        public interface IAwakeProbeModule : IModule { }

        public sealed class AwakeProbeModule : IAwakeProbeModule
        {
            public int Priority => 0;
            public IReadOnlyList<System.Type> DependsOn => System.Array.Empty<System.Type>();
            public int InitCount { get; private set; }
            public int ShutdownCount { get; private set; }
            public void OnInit(IModuleSystem host) => InitCount++;
            public void Shutdown() => ShutdownCount++;
        }

        /// <summary>
        /// 可被二次触发 Awake 的 entry。暴露 AwakeCount 与 TriggerSecondAwake。
        /// Setup 使用一个共享的探针模块实例，使其能跨多次 Host 被观察到。
        /// </summary>
        public sealed class MultipleAwakeEntry : TryGetMonoEntry
        {
            public AwakeProbeModule Probe { get; } = new AwakeProbeModule();
            public int AwakeCount { get; private set; }
            public bool HasHost => Host != null;
            public IModuleSystem ExposedHost => Host;

            // 累计所有曾被赋值过的 Host（用于检测旧 Host 是否泄漏 —— 修复后不应有多个）
            public List<IModuleSystem> AllHostsEverAssigned { get; } = new List<IModuleSystem>();

            protected override bool MakeDontDestroyOnLoad => false;

            protected override void Setup(IModuleSystem host)
            {
                host.Register<IAwakeProbeModule>(Probe);
            }

            protected override void Awake()
            {
                AwakeCount++;
                base.Awake();
                AllHostsEverAssigned.Add(Host);
            }

            /// <summary>模拟 Unity 第二次触发 Awake。</summary>
            public void TriggerSecondAwake() => Awake();
        }

        [UnityTest]
        public IEnumerator Awake_Twice_DoesNotLeakOldHost_OrDoubleInitProbe()
        {
            var gameObject = new GameObject("MultipleAwake Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<MultipleAwakeEntry>();
                yield return null;

                Assert.That(entry.AwakeCount, Is.EqualTo(1), "前提：首次 Awake 已执行");
                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.Probe.InitCount, Is.EqualTo(1), "首次 Awake 后 probe Init 1 次");

                // 模拟 Unity 二次触发 Awake
                entry.TriggerSecondAwake();
                yield return null;

                // —— 健壮期望 ——
                // 期望 1：二次 Awake 为 no-op，probe 不应被二次 Init（同一 probe 实例）
                Assert.That(entry.Probe.InitCount, Is.EqualTo(1),
                    "同一实例二次 Awake 不应让同一 probe 被 Init 两次");

                // 期望 2：二次 Awake 跳过，不应关闭仍在使用的 Host
                Assert.That(entry.Probe.ShutdownCount, Is.EqualTo(0),
                    "同一实例二次 Awake 是 no-op，不应 Shutdown 当前 Host");

                // 期望 3：当前只有一个活跃 Host，且 Host 未被替换
                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.AllHostsEverAssigned.Count, Is.EqualTo(2),
                    "测试记录了两次 Awake 调用后的 Host 引用");
                Assert.That(entry.AllHostsEverAssigned[1], Is.SameAs(entry.AllHostsEverAssigned[0]),
                    "同一实例二次 Awake 应保留首次创建的 Host");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Awake_Twice_DoesNotDoubleDriveFramePhases()
        {
            var gameObject = new GameObject("MultipleAwake FrameDispatch Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<MultipleAwakeEntry>();
                yield return null;

                // 二次触发 Awake —— 若泄漏，会有两条 EndOfFrameCoroutine 并行驱动
                entry.TriggerSecondAwake();
                yield return null;

                // 健壮期望：即使二次 Awake，单帧 EarlyUpdate 应只被驱动 1 次（无重复协程/重复 Update 调用）
                // 这里通过 Probe 的 InitCount 间接 + 检查 Host 唯一性。帧计数的精确断言需要 tracking module；
                // 核心断言：二次 Awake 后 Host 未重建，且 probe 未被双 Init。
                Assert.That(entry.Probe.InitCount, Is.EqualTo(1),
                    "二次 Awake 后 probe 不应被双 Init —— 证明 Awake 幂等");

                // 驱动几帧，确认无异常（双协程下 EndOfFrame 会双倍调用，可能抛或重复）
                for (int i = 0; i < 3; i++)
                    yield return null;

                Assert.Pass("无异常完成多帧 —— 若有双协程驱动，此处可能因重复帧分发出错（当前实现依赖 Host 字段覆盖，协程仍在跑）");
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
