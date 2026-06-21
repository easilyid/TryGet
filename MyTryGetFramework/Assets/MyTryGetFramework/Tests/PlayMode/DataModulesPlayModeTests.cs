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
    [TestFixture]
    public sealed class DataModulesPlayModeTests
    {
        [UnityTest]
        public IEnumerator DataModules_NonPersistentSceneUnload_ShutdownClearsMemoryStores()
        {
            var scene = SceneManager.CreateScene("TryGet_DataUnload_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(DataModules_NonPersistentSceneUnload_ShutdownClearsMemoryStores));
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<DataHostEntry>();
            yield return null;

            Assert.That(entry.Probe.Completed, Is.True,
                "测试前提：Data probe 应在真实 Unity Update 中写入 KV/Config/Asset 并完成 LoadAsync。");
            Assert.That(entry.KV.Count, Is.EqualTo(1));
            Assert.That(entry.Config.Count, Is.EqualTo(1));
            Assert.That(entry.Assets.LoadedCount, Is.EqualTo(1));
            Assert.That(entry.Probe.LoadedAsset, Is.SameAs(entry.Probe.Asset));

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(entry == null || !entry.HasHost, Is.True);
            Assert.That(entry.KV.Count, Is.EqualTo(0),
                "非持久 Entry 所属场景卸载时，MemoryKVStore.Shutdown 应清空运行期键值。");
            Assert.That(entry.Config.Count, Is.EqualTo(0),
                "非持久 Entry 所属场景卸载时，MemoryConfigSource.Shutdown 应清空运行期配置。");
            Assert.That(entry.Assets.LoadedCount, Is.EqualTo(0),
                "非持久 Entry 所属场景卸载时，MemoryAssetSource.Shutdown 应清空已加载资源表。");
        }

        [UnityTest]
        public IEnumerator DataModules_PersistentSceneUnload_KeepsMemoryStoresUntilDestroy()
        {
            var scene = SceneManager.CreateScene("TryGet_DataPersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(DataModules_PersistentSceneUnload_KeepsMemoryStoresUntilDestroy));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentDataHostEntry>();
                yield return null;

                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
                Assert.That(entry.Probe.Completed, Is.True);
                Assert.That(entry.KV.Count, Is.EqualTo(1));
                Assert.That(entry.Config.Count, Is.EqualTo(1));
                Assert.That(entry.Assets.LoadedCount, Is.EqualTo(1));

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.KV.Get<int>("session-score"), Is.EqualTo(77),
                    "持久 Entry 卸载原场景后，内存 KV 数据应继续可用。");
                Assert.That(entry.Config.Has("weapon"), Is.True,
                    "持久 Entry 卸载原场景后，内存配置应继续可用。");
                Assert.That(entry.Assets.TryGet<DataRuntimeAsset>("asset/player", out var loaded), Is.True,
                    "持久 Entry 卸载原场景后，内存资源应继续可用。");
                Assert.That(loaded, Is.SameAs(entry.Probe.Asset));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
                Assert.That(entry.KV.Count, Is.EqualTo(0));
                Assert.That(entry.Config.Count, Is.EqualTo(0));
                Assert.That(entry.Assets.LoadedCount, Is.EqualTo(0));
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    public sealed class DataRuntimeAsset { }

    public interface IDataProbeModule : IModule, IUpdateModule { }

    public sealed class DataProbeModule : IDataProbeModule
    {
        private IModuleSystem _host;
        private bool _started;

        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => new[]
        {
            typeof(IKVStore),
            typeof(IConfigSource),
            typeof(IAssetSource),
        };

        public bool Completed { get; private set; }
        public DataRuntimeAsset Asset { get; } = new DataRuntimeAsset();
        public DataRuntimeAsset LoadedAsset { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            _host = host;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (_started)
                return;

            _started = true;
            Run().Forget();
        }

        public void Shutdown()
        {
            _host = null;
        }

        private async TGTask Run()
        {
            var kv = _host.Get<IKVStore>();
            var config = _host.Get<IConfigSource>();
            var assets = _host.Get<IAssetSource>();

            kv.Set("session-score", 77);
            ((MemoryConfigSource)config).SetRaw("weapon", new byte[] { 1, 2, 3 });
            ((MemoryAssetSource)assets).Add("asset/player", Asset);

            LoadedAsset = await assets.LoadAsync<DataRuntimeAsset>("asset/player");
            Completed = true;
        }
    }

    public sealed class DataHostEntry : TryGetMonoEntry
    {
        public MemoryKVStore KV { get; } = new MemoryKVStore();
        public MemoryConfigSource Config { get; } = new MemoryConfigSource();
        public MemoryAssetSource Assets { get; } = new MemoryAssetSource();
        public DataProbeModule Probe { get; } = new DataProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IKVStore>(KV);
            host.Register<IConfigSource>(Config);
            host.Register<IAssetSource>(Assets);
            host.Register<IDataProbeModule>(Probe);
        }
    }

    public sealed class PersistentDataHostEntry : TryGetMonoEntry
    {
        public MemoryKVStore KV { get; } = new MemoryKVStore();
        public MemoryConfigSource Config { get; } = new MemoryConfigSource();
        public MemoryAssetSource Assets { get; } = new MemoryAssetSource();
        public DataProbeModule Probe { get; } = new DataProbeModule();
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IKVStore>(KV);
            host.Register<IConfigSource>(Config);
            host.Register<IAssetSource>(Assets);
            host.Register<IDataProbeModule>(Probe);
        }
    }
}
