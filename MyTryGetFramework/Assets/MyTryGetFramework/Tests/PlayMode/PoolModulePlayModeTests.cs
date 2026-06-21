using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// PoolModule 在真实 Unity PlayMode 生命周期下的端到端验证。
    /// 区别于 EditMode 的直接 Shutdown 调用，这里通过 TryGetMonoEntry + Scene unload 触发 OnDestroy。
    /// </summary>
    [TestFixture]
    public sealed class PoolModulePlayModeTests
    {
        [UnityTest]
        public IEnumerator Pool_PlayMode_SceneUnload_ShutdownClearsPools()
        {
            var scene = SceneManager.CreateScene("TryGet_PoolUnload_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(Pool_PlayMode_SceneUnload_ShutdownClearsPools));
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<PoolHostEntry>();
            yield return null;

            Assert.That(entry.HasHost, Is.True);

            var module = entry.PoolModule;
            var oldPool = module.GetOrCreatePool(() => new RuntimePoolItem());
            var active = oldPool.Rent();
            oldPool.Return(active);
            active = oldPool.Rent();

            var oldDiagnostics = oldPool.GetDiagnostics();
            Assert.That(oldDiagnostics.CurrentActive, Is.EqualTo(1));
            Assert.That(oldDiagnostics.IdleCount, Is.EqualTo(0));

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(entry == null || !entry.HasHost, Is.True);

            var newPoolAfterShutdown = module.GetOrCreatePool(() => new RuntimePoolItem());
            Assert.That(newPoolAfterShutdown, Is.Not.SameAs(oldPool),
                "TryGetMonoEntry OnDestroy 应调用 PoolModule.Shutdown，清空模块持有的池字典。");
            Assert.That(newPoolAfterShutdown.GetDiagnostics().CurrentActive, Is.EqualTo(0));
            Assert.That(newPoolAfterShutdown.GetDiagnostics().IdleCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Pool_PlayMode_PersistentEntry_SceneUnload_KeepsPoolStateUntilDestroy()
        {
            var scene = SceneManager.CreateScene("TryGet_PoolPersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(Pool_PlayMode_PersistentEntry_SceneUnload_KeepsPoolStateUntilDestroy));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentPoolHostEntry>();
                yield return null;

                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                var module = entry.PoolModule;
                var pool = module.GetOrCreatePool(() => new RuntimePoolItem());
                var pooledBeforeUnload = pool.Rent();
                pool.Return(pooledBeforeUnload);

                Assert.That(pool.GetDiagnostics().IdleCount, Is.EqualTo(1));

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(entry.HasHost, Is.True);

                var poolAfterUnload = module.GetOrCreatePool(() => new RuntimePoolItem());
                Assert.That(poolAfterUnload, Is.SameAs(pool),
                    "持久 PoolHostEntry 卸载原场景时不应 Shutdown PoolModule 或清空池字典。");

                var pooledAfterUnload = poolAfterUnload.Rent();
                Assert.That(pooledAfterUnload, Is.SameAs(pooledBeforeUnload),
                    "持久 PoolHostEntry 卸载原场景后，池内 idle 对象应继续可复用。");
                poolAfterUnload.Return(pooledAfterUnload);

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
                var poolAfterDestroy = module.GetOrCreatePool(() => new RuntimePoolItem());
                Assert.That(poolAfterDestroy, Is.Not.SameAs(pool),
                    "持久 Entry 最终销毁时应 Shutdown PoolModule 并清空池字典。");
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Pool_PlayMode_NewEntry_DoesNotInheritPreviousEntryPoolState()
        {
            var first = new GameObject("TryGet Pool First Entry");
            var second = default(GameObject);
            var firstDestroyed = false;
            var secondDestroyed = false;

            try
            {
                var firstEntry = first.AddComponent<PoolHostEntry>();
                yield return null;

                var firstPool = firstEntry.PoolModule.GetOrCreatePool(() => new RuntimePoolItem());
                var leakedFromFirstEntry = firstPool.Rent();
                Assert.That(leakedFromFirstEntry, Is.Not.Null);
                Assert.That(firstPool.GetDiagnostics().CurrentActive, Is.EqualTo(1));

                UnityEngine.Object.Destroy(first);
                firstDestroyed = true;
                yield return null;

                Assert.That(firstEntry == null || !firstEntry.HasHost, Is.True);

                second = new GameObject("TryGet Pool Second Entry");
                var secondEntry = second.AddComponent<PoolHostEntry>();
                yield return null;

                var secondPool = secondEntry.PoolModule.GetOrCreatePool(() => new RuntimePoolItem());
                var secondDiagnosticsBeforeRent = secondPool.GetDiagnostics();
                Assert.That(secondDiagnosticsBeforeRent.CurrentActive, Is.EqualTo(0));
                Assert.That(secondDiagnosticsBeforeRent.TotalRented, Is.EqualTo(0));
                Assert.That(secondDiagnosticsBeforeRent.IdleCount, Is.EqualTo(0));

                var itemFromSecondEntry = secondPool.Rent();
                Assert.That(itemFromSecondEntry, Is.Not.SameAs(leakedFromFirstEntry),
                    "新 Entry 的 PoolModule 应从干净状态创建池，不应复用上一 Entry 的活跃对象。");
                Assert.That(secondPool.GetDiagnostics().CurrentActive, Is.EqualTo(1));

                UnityEngine.Object.Destroy(second);
                secondDestroyed = true;
                yield return null;

                Assert.That(secondEntry == null || !secondEntry.HasHost, Is.True);
            }
            finally
            {
                if (!firstDestroyed && first != null)
                    UnityEngine.Object.Destroy(first);
                if (!secondDestroyed && second != null)
                    UnityEngine.Object.Destroy(second);
            }
        }

        [UnityTest]
        public IEnumerator Pool_PlayMode_OnReturnException_IsolatedDuringRealFrameUpdate()
        {
            var gameObject = new GameObject(nameof(Pool_PlayMode_OnReturnException_IsolatedDuringRealFrameUpdate));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PoolReturnExceptionHostEntry>();
                yield return null;

                for (int i = 0; i < 10 && !entry.Probe.Completed; i++)
                    yield return null;

                var probe = entry.Probe;
                Assert.That(probe.Completed, Is.True,
                    "业务模块在真实 Update 中 Return 时即使 onReturn 抛异常，也不应打断后续帧驱动。");
                Assert.That(probe.ReturnAttempts, Is.EqualTo(2));
                Assert.That(probe.ReusedSameInstance, Is.True,
                    "onReturn 异常隔离后，对象仍应进入 idle 栈并被后续 Rent 复用。");
                Assert.That(probe.Diagnostics.TotalRented, Is.EqualTo(2));
                Assert.That(probe.Diagnostics.TotalReturned, Is.EqualTo(2));
                Assert.That(probe.Diagnostics.CurrentActive, Is.EqualTo(0));
                Assert.That(probe.Diagnostics.IdleCount, Is.EqualTo(1));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    public sealed class RuntimePoolItem { }

    public sealed class RuntimeThrowingReturnPoolItem { }

    public interface IPoolReturnExceptionProbeModule : IModule, IUpdateModule
    {
    }

    public sealed class PoolReturnExceptionProbeModule : IPoolReturnExceptionProbeModule
    {
        private IObjectPool<RuntimeThrowingReturnPoolItem> _pool;
        private RuntimeThrowingReturnPoolItem _first;

        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IPoolModule) };
        public bool Completed { get; private set; }
        public int ReturnAttempts { get; private set; }
        public bool ReusedSameInstance { get; private set; }
        public PoolDiagnostics Diagnostics { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            _pool = host.Get<IPoolModule>().GetOrCreatePool(
                () => new RuntimeThrowingReturnPoolItem(),
                onReturn: _ =>
                {
                    ReturnAttempts++;
                    throw new InvalidOperationException("onReturn boom");
                });
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (Completed)
                return;

            _first = _pool.Rent();
            _pool.Return(_first);

            var second = _pool.Rent();
            ReusedSameInstance = ReferenceEquals(_first, second);
            _pool.Return(second);

            Diagnostics = _pool.GetDiagnostics();
            Completed = true;
        }

        public void Shutdown()
        {
            _pool = null;
            _first = null;
        }
    }

    public sealed class PoolHostEntry : TryGetMonoEntry
    {
        public PoolModule PoolModule { get; } = new PoolModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IPoolModule>(PoolModule);
        }
    }

    public sealed class PersistentPoolHostEntry : TryGetMonoEntry
    {
        public PoolModule PoolModule { get; } = new PoolModule();
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IPoolModule>(PoolModule);
        }
    }

    public sealed class PoolReturnExceptionHostEntry : TryGetMonoEntry
    {
        public PoolModule PoolModule { get; } = new PoolModule();
        public PoolReturnExceptionProbeModule Probe { get; } = new PoolReturnExceptionProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IPoolModule>(PoolModule);
            host.Register<IPoolReturnExceptionProbeModule>(Probe);
        }
    }
}
