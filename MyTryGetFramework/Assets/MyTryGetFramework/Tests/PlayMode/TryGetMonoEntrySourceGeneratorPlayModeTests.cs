using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntrySourceGeneratorPlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_AppliesGeneratedModuleAndEventHandlerInPlayMode()
        {
            GeneratedPlayModeEventState.Reset();
            SecondaryGeneratedPlayModeEventState.Reset();
            var gameObject = new GameObject("TryGetMonoEntry Source Generator Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<GeneratedRegistrationTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasGeneratedModuleDuringSetup, Is.True);
                Assert.That(entry.HasSecondaryGeneratedModuleDuringSetup, Is.True);
                Assert.That(entry.GeneratedModule, Is.Not.Null);
                Assert.That(entry.SecondaryGeneratedModule, Is.Not.Null);
                Assert.That(entry.GeneratedModule.InitCount, Is.EqualTo(1));
                Assert.That(entry.SecondaryGeneratedModule.InitCount, Is.EqualTo(1));
                Assert.That(entry.GeneratedEventTotalDuringSetup, Is.EqualTo(23));
                Assert.That(entry.SecondaryGeneratedEventTotalDuringSetup, Is.EqualTo(29));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.GeneratedModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.SecondaryGeneratedModule.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);

                GeneratedPlayModeEventState.Reset();
                SecondaryGeneratedPlayModeEventState.Reset();
            }
        }

        [UnityTest]
        public IEnumerator ModuleRegistry_PlayModeGeneratedRegistrations_HaveMetadataAndApplyToMultipleHosts()
        {
            yield return null;

            Assert.That(HasGeneratedModuleMetadata(), Is.True,
                "PlayMode 中 ModuleRegistry.Snapshot 应包含测试程序集由 Source Generator 生成的模块元数据。");
            Assert.That(HasGeneratedEventHandlerMetadata(), Is.True,
                "PlayMode 中 EventHandlerRegistry.Snapshot 应包含测试程序集由 Source Generator 生成的事件处理器元数据。");

            IModuleSystem host1 = null;
            IModuleSystem host2 = null;
            try
            {
                host1 = GameLauncher.CreateHost();
                host1.Initialize();
                Assert.That(host1.TryGet<IGeneratedPlayModeModule>(out var generated1), Is.True);
                Assert.That(generated1, Is.TypeOf<GeneratedPlayModeModule>());
                Assert.That(((GeneratedPlayModeModule)generated1).InitCount, Is.EqualTo(1));

                GeneratedPlayModeEventState.Reset();
                host1.EventModule.Publish(new GeneratedPlayModeEvent { Value = 11 });
                Assert.That(GeneratedPlayModeEventState.Total, Is.EqualTo(11));

                host2 = GameLauncher.CreateHost();
                host2.Initialize();
                Assert.That(host2.TryGet<IGeneratedPlayModeModule>(out var generated2), Is.True);
                Assert.That(generated2, Is.TypeOf<GeneratedPlayModeModule>());
                Assert.That(((GeneratedPlayModeModule)generated2).InitCount, Is.EqualTo(1));
                Assert.That(generated2, Is.Not.SameAs(generated1),
                    "ModuleRegistry.ApplyAll 应为每个新 host 注册新的模块实例。");

                GeneratedPlayModeEventState.Reset();
                host2.EventModule.Publish(new GeneratedPlayModeEvent { Value = 7 });
                Assert.That(GeneratedPlayModeEventState.Total, Is.EqualTo(7));
            }
            finally
            {
                host2?.Shutdown();
                host1?.Shutdown();
                GeneratedPlayModeEventState.Reset();
            }
        }

        [UnityTest]
        public IEnumerator ModuleRegistry_PlayModeGeneratedMetadata_DoesNotDuplicateAcrossFramesOrHosts()
        {
            yield return null;

            int moduleMetadataBefore = CountGeneratedModuleMetadata();
            int secondaryModuleMetadataBefore = CountSecondaryGeneratedModuleMetadata();
            int handlerMetadataBefore = CountGeneratedEventHandlerMetadata();
            int secondaryHandlerMetadataBefore = CountSecondaryGeneratedEventHandlerMetadata();

            Assert.That(moduleMetadataBefore, Is.EqualTo(1),
                "PlayMode Source Generator 生成的测试模块 metadata 应只注册一次。");
            Assert.That(secondaryModuleMetadataBefore, Is.EqualTo(1),
                "PlayMode Source Generator 生成的第二个测试模块 metadata 应只注册一次。");
            Assert.That(handlerMetadataBefore, Is.EqualTo(1),
                "PlayMode Source Generator 生成的测试事件 handler metadata 应只注册一次。");
            Assert.That(secondaryHandlerMetadataBefore, Is.EqualTo(1),
                "PlayMode Source Generator 生成的第二个测试事件 handler metadata 应只注册一次。");

            IModuleSystem host1 = null;
            IModuleSystem host2 = null;
            try
            {
                host1 = GameLauncher.CreateHost();
                host1.Initialize();
                host2 = GameLauncher.CreateHost();
                host2.Initialize();

                yield return null;
                yield return null;

                Assert.That(CountGeneratedModuleMetadata(), Is.EqualTo(moduleMetadataBefore),
                    "多次创建 host 或推进真实 PlayMode 帧不应重复追加同一生成模块 metadata。");
                Assert.That(CountSecondaryGeneratedModuleMetadata(), Is.EqualTo(secondaryModuleMetadataBefore),
                    "多次创建 host 或推进真实 PlayMode 帧不应重复追加第二个生成模块 metadata。");
                Assert.That(CountGeneratedEventHandlerMetadata(), Is.EqualTo(handlerMetadataBefore),
                    "多次创建 host 或推进真实 PlayMode 帧不应重复追加同一生成事件 handler metadata。");
                Assert.That(CountSecondaryGeneratedEventHandlerMetadata(), Is.EqualTo(secondaryHandlerMetadataBefore),
                    "多次创建 host 或推进真实 PlayMode 帧不应重复追加第二个生成事件 handler metadata。");
            }
            finally
            {
                host2?.Shutdown();
                host1?.Shutdown();
                GeneratedPlayModeEventState.Reset();
                SecondaryGeneratedPlayModeEventState.Reset();
            }
        }

        [UnityTest]
        public IEnumerator ModuleRegistry_PlayModeGeneratedRegistrations_ApplyMultipleModulesAndHandlers()
        {
            GeneratedPlayModeEventState.Reset();
            SecondaryGeneratedPlayModeEventState.Reset();
            yield return null;

            Assert.That(CountGeneratedModuleMetadata(), Is.EqualTo(1));
            Assert.That(CountSecondaryGeneratedModuleMetadata(), Is.EqualTo(1),
                "同一 PlayMode 测试程序集中的第二个 [Module] 也应生成 metadata。");
            Assert.That(CountGeneratedEventHandlerMetadata(), Is.EqualTo(1));
            Assert.That(CountSecondaryGeneratedEventHandlerMetadata(), Is.EqualTo(1),
                "同一 PlayMode 测试程序集中的第二个 [EventHandler] 也应生成 metadata。");

            IModuleSystem host = null;
            try
            {
                host = GameLauncher.CreateHost();
                host.Initialize();

                Assert.That(host.TryGet<IGeneratedPlayModeModule>(out var generated), Is.True);
                Assert.That(host.TryGet<ISecondaryGeneratedPlayModeModule>(out var secondary), Is.True);
                Assert.That(generated, Is.TypeOf<GeneratedPlayModeModule>());
                Assert.That(secondary, Is.TypeOf<SecondaryGeneratedPlayModeModule>());
                Assert.That(((GeneratedPlayModeModule)generated).InitCount, Is.EqualTo(1));
                Assert.That(((SecondaryGeneratedPlayModeModule)secondary).InitCount, Is.EqualTo(1));

                host.EventModule.Publish(new GeneratedPlayModeEvent { Value = 3 });
                host.EventModule.Publish(new SecondaryGeneratedPlayModeEvent { Value = 5 });

                Assert.That(GeneratedPlayModeEventState.Total, Is.EqualTo(3));
                Assert.That(SecondaryGeneratedPlayModeEventState.Total, Is.EqualTo(5));
            }
            finally
            {
                host?.Shutdown();
                GeneratedPlayModeEventState.Reset();
                SecondaryGeneratedPlayModeEventState.Reset();
            }
        }

        private static bool HasGeneratedModuleMetadata()
        {
            return CountGeneratedModuleMetadata() > 0;
        }

        private static int CountGeneratedModuleMetadata()
        {
            var snapshot = ModuleRegistry.Snapshot();
            int count = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].ImplementationType == typeof(GeneratedPlayModeModule) &&
                    snapshot[i].ServiceType == typeof(IGeneratedPlayModeModule) &&
                    !string.IsNullOrEmpty(snapshot[i].SourceAssembly) &&
                    snapshot[i].SourceAssembly != "unknown")
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountSecondaryGeneratedModuleMetadata()
        {
            var snapshot = ModuleRegistry.Snapshot();
            int count = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].ImplementationType == typeof(SecondaryGeneratedPlayModeModule) &&
                    snapshot[i].ServiceType == typeof(ISecondaryGeneratedPlayModeModule) &&
                    !string.IsNullOrEmpty(snapshot[i].SourceAssembly) &&
                    snapshot[i].SourceAssembly != "unknown")
                {
                    count++;
                }
            }
            return count;
        }

        private static bool HasGeneratedEventHandlerMetadata()
        {
            return CountGeneratedEventHandlerMetadata() > 0;
        }

        private static int CountGeneratedEventHandlerMetadata()
        {
            var snapshot = EventHandlerRegistry.Snapshot();
            int count = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].EventType == typeof(GeneratedPlayModeEvent) &&
                    snapshot[i].HandlerSignature.Contains(nameof(GeneratedPlayModeEventHandlers.OnGeneratedPlayModeEvent)) &&
                    !string.IsNullOrEmpty(snapshot[i].SourceAssembly) &&
                    snapshot[i].SourceAssembly != "unknown")
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountSecondaryGeneratedEventHandlerMetadata()
        {
            var snapshot = EventHandlerRegistry.Snapshot();
            int count = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].EventType == typeof(SecondaryGeneratedPlayModeEvent) &&
                    snapshot[i].HandlerSignature.Contains(nameof(GeneratedPlayModeEventHandlers.OnSecondaryGeneratedPlayModeEvent)) &&
                    !string.IsNullOrEmpty(snapshot[i].SourceAssembly) &&
                    snapshot[i].SourceAssembly != "unknown")
                {
                    count++;
                }
            }
            return count;
        }
    }

    public struct GeneratedPlayModeEvent
    {
        public int Value;
    }

    public struct SecondaryGeneratedPlayModeEvent
    {
        public int Value;
    }

    public static class GeneratedPlayModeEventState
    {
        public static int Total { get; private set; }

        public static void Reset()
        {
            Total = 0;
        }

        public static void Add(int value)
        {
            Total += value;
        }
    }

    public static class SecondaryGeneratedPlayModeEventState
    {
        public static int Total { get; private set; }

        public static void Reset()
        {
            Total = 0;
        }

        public static void Add(int value)
        {
            Total += value;
        }
    }

    public static class GeneratedPlayModeEventHandlers
    {
        [EventHandler]
        public static void OnGeneratedPlayModeEvent(GeneratedPlayModeEvent evt)
        {
            GeneratedPlayModeEventState.Add(evt.Value);
        }

        [EventHandler]
        public static void OnSecondaryGeneratedPlayModeEvent(SecondaryGeneratedPlayModeEvent evt)
        {
            SecondaryGeneratedPlayModeEventState.Add(evt.Value);
        }
    }

    public interface IGeneratedPlayModeModule : IModule
    {
    }

    public interface ISecondaryGeneratedPlayModeModule : IModule
    {
    }

    [Module(typeof(IGeneratedPlayModeModule))]
    public sealed class GeneratedPlayModeModule : IGeneratedPlayModeModule
    {
        public int Priority => 100;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    [Module(typeof(ISecondaryGeneratedPlayModeModule))]
    public sealed class SecondaryGeneratedPlayModeModule : ISecondaryGeneratedPlayModeModule
    {
        public int Priority => 101;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    public sealed class GeneratedRegistrationTryGetMonoEntry : TryGetMonoEntry
    {
        public bool HasGeneratedModuleDuringSetup { get; private set; }
        public bool HasSecondaryGeneratedModuleDuringSetup { get; private set; }
        public GeneratedPlayModeModule GeneratedModule { get; private set; }
        public SecondaryGeneratedPlayModeModule SecondaryGeneratedModule { get; private set; }
        public int GeneratedEventTotalDuringSetup { get; private set; }
        public int SecondaryGeneratedEventTotalDuringSetup { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            HasGeneratedModuleDuringSetup =
                host.TryGet<IGeneratedPlayModeModule>(out var generatedModule);
            HasSecondaryGeneratedModuleDuringSetup =
                host.TryGet<ISecondaryGeneratedPlayModeModule>(out var secondaryGeneratedModule);
            GeneratedModule = generatedModule as GeneratedPlayModeModule;
            SecondaryGeneratedModule = secondaryGeneratedModule as SecondaryGeneratedPlayModeModule;

            host.EventModule.Publish(new GeneratedPlayModeEvent { Value = 23 });
            host.EventModule.Publish(new SecondaryGeneratedPlayModeEvent { Value = 29 });
            GeneratedEventTotalDuringSetup = GeneratedPlayModeEventState.Total;
            SecondaryGeneratedEventTotalDuringSetup = SecondaryGeneratedPlayModeEventState.Total;
        }
    }
}
