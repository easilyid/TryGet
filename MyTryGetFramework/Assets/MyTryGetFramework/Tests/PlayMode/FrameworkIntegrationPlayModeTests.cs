using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Async;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// 多子系统协同 + 调度器真实时间精度的端到端验证——最贴近用户真实接入体验。
    ///
    /// - 协同链路：Procedure 进入 → 注册周期 Timer → Timer 回调 Publish 事件 → EventHandler rent/return 对象池，
    ///   全部在真实 Unity 帧下跑通；Stop 流程后 Timer 被取消、链路停止（验证退出清理不泄漏）。
    /// - 时间精度：scheduler.Delay(秒) 在真实帧下经过约定秒数完成（自动断言版的 RuntimeVerificationEntry），
    ///   WaitForFrames 真实推进对应帧数。
    /// </summary>
    [TestFixture]
    public sealed class FrameworkIntegrationPlayModeTests
    {
        [UnityTest]
        public IEnumerator MultiModule_ProcedureTimerEventPool_CooperateAcrossFrames_AndStopCleansUp()
        {
            var gameObject = new GameObject(nameof(MultiModule_ProcedureTimerEventPool_CooperateAcrossFrames_AndStopCleansUp));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<GameplayHostEntry>();
                yield return null; // Awake: 注册 Timer/Pool/Procedure/Coordinator + Initialize

                var coordinator = entry.Coordinator;
                var procedures = entry.Procedures;

                procedures.Start("Gameplay"); // OnEnter: ScheduleRepeat → 周期 Publish<SpawnEvent>

                for (int i = 0; i < 240 && coordinator.SpawnEventCount < 3; i++)
                    yield return null;

                Assert.That(coordinator.SpawnEventCount, Is.GreaterThanOrEqualTo(3),
                    "Procedure→Timer→Event→Handler 全链路应在真实帧下多次触发");
                Assert.That(coordinator.BulletsRented, Is.EqualTo(coordinator.SpawnEventCount),
                    "每次 spawn 事件应从对象池 rent 一次");
                Assert.That(coordinator.BulletsCreated, Is.LessThan(coordinator.BulletsRented),
                    "rent/return 循环应复用对象，factory 创建次数远小于租用次数");

                int countAtStop = coordinator.SpawnEventCount;
                procedures.Stop(); // OnExit: 取消 Timer

                for (int i = 0; i < 30; i++)
                    yield return null;
                Assert.That(coordinator.SpawnEventCount, Is.EqualTo(countAtStop),
                    "流程 Stop 后 Timer 被取消，链路停止，不再 spawn");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator MultiModule_NonPersistentSceneUnload_ShutsDownChainAndStopsSpawning()
        {
            var scene = SceneManager.CreateScene("TryGet_FrameworkIntegrationUnload_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(MultiModule_NonPersistentSceneUnload_ShutsDownChainAndStopsSpawning));
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<GameplayHostEntry>();
            yield return null;

            var coordinator = entry.Coordinator;
            var procedures = entry.Procedures;
            var timer = entry.Timer;
            var poolModule = entry.PoolModule;

            procedures.Start("Gameplay");
            for (int i = 0; i < 240 && coordinator.SpawnEventCount < 2; i++)
                yield return null;

            Assert.That(coordinator.SpawnEventCount, Is.GreaterThanOrEqualTo(2),
                "测试前提：场景卸载前，Procedure→Timer→Event→Pool 链路应已经在真实帧下运行。");
            Assert.That(timer.PendingCount, Is.GreaterThan(0),
                "GameplayProcedure 进入后应有一个周期 Timer 正在驱动 spawn。");

            var pooledBeforeUnload = poolModule.GetOrCreatePool(() => new PooledBullet());
            int countBeforeUnload = coordinator.SpawnEventCount;

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(entry == null || !entry.HasHost, Is.True);
            Assert.That(procedures.StackDepth, Is.EqualTo(0),
                "场景卸载触发 TryGetMonoEntry.OnDestroy 后，ProcedureModule.Shutdown 应清空流程栈。");
            Assert.That(timer.PendingCount, Is.EqualTo(0),
                "Procedure OnExit 取消 gameplay timer，TimerModule.Shutdown 也应清空所有 pending timer。");

            var pooledAfterUnload = poolModule.GetOrCreatePool(() => new PooledBullet());
            Assert.That(pooledAfterUnload, Is.Not.SameAs(pooledBeforeUnload),
                "场景卸载触发 PoolModule.Shutdown 后，池字典应被清空。");

            for (int i = 0; i < 30; i++)
                yield return null;

            Assert.That(coordinator.SpawnEventCount, Is.EqualTo(countBeforeUnload),
                "非持久 Entry 所属场景卸载后，旧协同链路不应继续由真实 Unity 帧产生 spawn。");
        }

        [UnityTest]
        public IEnumerator Scheduler_Delay_ElapsesApproximatelyRealSeconds()
        {
            var gameObject = new GameObject(nameof(Scheduler_Delay_ElapsesApproximatelyRealSeconds));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new DelayTimingProbe();
                probe.RunDelay(entry.Scheduler, 0.2f).Forget();

                for (int i = 0; i < 300 && !probe.Done; i++)
                    yield return null;

                Assert.That(probe.Done, Is.True, "Delay 应在真实帧下完成");
                Assert.That(probe.ElapsedScaledSeconds, Is.EqualTo(0.2f).Within(0.15f),
                    "Delay(秒) 应在约定真实秒数后完成（含一帧 overshoot 容差）");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Scheduler_WaitForFrames_AdvancesRequestedFrames()
        {
            var gameObject = new GameObject(nameof(Scheduler_WaitForFrames_AdvancesRequestedFrames));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<SchedulerProbeEntry>();
                yield return null;

                var probe = new DelayTimingProbe();
                probe.RunWaitForFrames(entry.Scheduler, 3).Forget();

                for (int i = 0; i < 60 && !probe.Done; i++)
                    yield return null;

                Assert.That(probe.Done, Is.True, "WaitForFrames 应在真实帧下完成");
                Assert.That(probe.ElapsedFrames, Is.GreaterThanOrEqualTo(3),
                    "WaitForFrames(3) 应至少推进 3 个 Unity 帧");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    public struct SpawnEvent { }

    public sealed class PooledBullet { }

    public interface IGameplayCoordinator : IModule { }

    /// <summary>业务协调模块：订阅 SpawnEvent，收到后从对象池 rent/return（验证 Event ↔ Pool 协同与复用）。</summary>
    public sealed class GameplayCoordinatorModule : IGameplayCoordinator
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IPoolModule) };

        private IModuleSystem _host;
        private IObjectPool<PooledBullet> _bulletPool;

        public int SpawnEventCount { get; private set; }
        public int BulletsRented { get; private set; }
        public int BulletsCreated { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            _host = host;
            _bulletPool = host.Get<IPoolModule>().GetOrCreatePool(() =>
            {
                BulletsCreated++;
                return new PooledBullet();
            });
            host.EventModule.Subscribe<SpawnEvent>(OnSpawn);
        }

        public void Shutdown()
        {
            _host?.EventModule.Unsubscribe<SpawnEvent>(OnSpawn);
        }

        private void OnSpawn(SpawnEvent evt)
        {
            SpawnEventCount++;
            var bullet = _bulletPool.Rent();
            BulletsRented++;
            _bulletPool.Return(bullet);
        }
    }

    /// <summary>游戏流程：进入后周期性 Publish SpawnEvent；离开时取消 Timer（避免泄漏）。</summary>
    public sealed class GameplayProcedure : ProcedureBase
    {
        private readonly ITimerModule _timer;
        private readonly IEventModule _events;
        private TimerHandle _handle;

        public GameplayProcedure(ITimerModule timer, IEventModule events)
        {
            _timer = timer;
            _events = events;
        }

        public override void OnEnter(IProcedureModule module)
        {
            _handle = _timer.ScheduleRepeat(0.05f, () => _events.Publish(new SpawnEvent()));
        }

        public override void OnExit(IProcedureModule module)
        {
            _timer.Cancel(_handle);
            base.OnExit(module);
        }
    }

    /// <summary>组合注册 Timer + Pool + Procedure + 业务协调模块的入口，并预置 Gameplay 流程。</summary>
    public sealed class GameplayHostEntry : TryGetMonoEntry
    {
        public GameplayCoordinatorModule Coordinator { get; } = new GameplayCoordinatorModule();
        public IProcedureModule Procedures { get; private set; }
        public ITimerModule Timer { get; private set; }
        public IPoolModule PoolModule { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            var timer = new TimerModule();
            var pool = new PoolModule();
            var procedures = new ProcedureModule();

            host.Register<ITimerModule>(timer);
            host.Register<IPoolModule>(pool);
            host.Register<IProcedureModule>(procedures);
            host.Register<IGameplayCoordinator>(Coordinator);

            procedures.AddProcedure("Gameplay", new GameplayProcedure(timer, host.EventModule));
            Procedures = procedures;
            Timer = timer;
            PoolModule = pool;
        }
    }

    /// <summary>承载 Delay / WaitForFrames 的真实耗时测量（避免在迭代器测试体内闭包捕获状态）。</summary>
    public sealed class DelayTimingProbe
    {
        public bool Done { get; private set; }
        public float ElapsedScaledSeconds { get; private set; }
        public long ElapsedFrames { get; private set; }

        public async TGTask RunDelay(ITGTaskScheduler scheduler, float seconds)
        {
            float start = Time.time;
            await scheduler.Delay(seconds);
            ElapsedScaledSeconds = Time.time - start;
            Done = true;
        }

        public async TGTask RunWaitForFrames(ITGTaskScheduler scheduler, int frames)
        {
            int start = Time.frameCount;
            await scheduler.WaitForFrames(frames);
            ElapsedFrames = Time.frameCount - start;
            Done = true;
        }
    }
}
