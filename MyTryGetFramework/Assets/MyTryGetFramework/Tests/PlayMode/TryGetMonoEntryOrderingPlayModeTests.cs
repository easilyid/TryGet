using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryOrderingPlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_UsesModuleTopologyForInitFrameDispatchAndShutdown()
        {
            var gameObject = new GameObject(nameof(TryGetMonoEntry_UsesModuleTopologyForInitFrameDispatchAndShutdown));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<OrderingTryGetMonoEntry>();

                Assert.That(entry.InitLog, Is.EqualTo(new[]
                {
                    "init:bootstrap",
                    "init:foundation",
                    "init:feature",
                    "init:presentation"
                }));

                yield return new WaitForEndOfFrame();
                entry.ResetFrameLog();

                for (int i = 0; i < 10 && !entry.FrameLog.Contains("end:presentation"); i++)
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }

                Assert.That(entry.FrameLog, Is.EqualTo(new[]
                {
                    "early:bootstrap",
                    "early:foundation",
                    "early:feature",
                    "early:presentation",
                    "update:bootstrap",
                    "update:foundation",
                    "update:feature",
                    "update:presentation",
                    "late:bootstrap",
                    "late:foundation",
                    "late:feature",
                    "late:presentation",
                    "end:bootstrap",
                    "end:foundation",
                    "end:feature",
                    "end:presentation"
                }));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.ShutdownLog, Is.EqualTo(new[]
                {
                    "shutdown:presentation",
                    "shutdown:feature",
                    "shutdown:foundation",
                    "shutdown:bootstrap"
                }));
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TryGetMonoEntry_MissingDependencyDuringInitialize_ClearsHostWithoutLifecycleCalls()
        {
            var gameObject = new GameObject(nameof(TryGetMonoEntry_MissingDependencyDuringInitialize_ClearsHostWithoutLifecycleCalls));
            var destroyRequested = false;

            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("ModuleDependencyMissingException"));

                var entry = gameObject.AddComponent<MissingDependencyTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasHost, Is.False);
                Assert.That(entry.Module.InitCount, Is.EqualTo(0),
                    "A missing dependency is detected before any module reaches OnInit.");
                Assert.That(entry.Module.ShutdownCount, Is.EqualTo(0),
                    "A module that never reached OnInit must not be shut down during failed boot cleanup.");
                LogAssert.NoUnexpectedReceived();

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
    }

    public interface IOrderingBootstrapModule : IModule
    {
    }

    public interface IOrderingFoundationModule : IModule
    {
    }

    public interface IOrderingFeatureModule : IModule
    {
    }

    public interface IOrderingPresentationModule : IModule
    {
    }

    public interface IMissingDependencyProbeModule : IModule
    {
    }

    public interface IUnavailableOrderingDependencyModule : IModule
    {
    }

    public sealed class OrderingTryGetMonoEntry : TryGetMonoEntry
    {
        public List<string> InitLog { get; } = new List<string>();
        public List<string> FrameLog { get; } = new List<string>();
        public List<string> ShutdownLog { get; } = new List<string>();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IOrderingPresentationModule>(
                new OrderingPresentationModule(InitLog, FrameLog, ShutdownLog));
            host.Register<IOrderingFeatureModule>(
                new OrderingFeatureModule(InitLog, FrameLog, ShutdownLog));
            host.Register<IOrderingFoundationModule>(
                new OrderingFoundationModule(InitLog, FrameLog, ShutdownLog));
            host.Register<IOrderingBootstrapModule>(
                new OrderingBootstrapModule(InitLog, FrameLog, ShutdownLog));
        }

        public void ResetFrameLog()
        {
            FrameLog.Clear();
        }
    }

    public sealed class MissingDependencyTryGetMonoEntry : TryGetMonoEntry
    {
        public MissingDependencyProbeModule Module { get; } = new MissingDependencyProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IMissingDependencyProbeModule>(Module);
        }
    }

    public abstract class OrderingProbeModule :
        IEarlyUpdateModule,
        IUpdateModule,
        ILateUpdateModule,
        IEndOfFrameModule
    {
        private readonly List<string> _initLog;
        private readonly List<string> _frameLog;
        private readonly List<string> _shutdownLog;

        protected OrderingProbeModule(
            List<string> initLog,
            List<string> frameLog,
            List<string> shutdownLog)
        {
            _initLog = initLog;
            _frameLog = frameLog;
            _shutdownLog = shutdownLog;
        }

        public abstract int Priority { get; }
        public abstract IReadOnlyList<Type> DependsOn { get; }
        protected abstract string Name { get; }

        public void OnInit(IModuleSystem host)
        {
            _initLog.Add("init:" + Name);
        }

        public void Shutdown()
        {
            _shutdownLog.Add("shutdown:" + Name);
        }

        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            _frameLog.Add("early:" + Name);
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            _frameLog.Add("update:" + Name);
        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            _frameLog.Add("late:" + Name);
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            _frameLog.Add("end:" + Name);
        }
    }

    public sealed class OrderingBootstrapModule : OrderingProbeModule, IOrderingBootstrapModule
    {
        public OrderingBootstrapModule(
            List<string> initLog,
            List<string> frameLog,
            List<string> shutdownLog)
            : base(initLog, frameLog, shutdownLog)
        {
        }

        public override int Priority => -100;
        public override IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        protected override string Name => "bootstrap";
    }

    public sealed class OrderingFoundationModule : OrderingProbeModule, IOrderingFoundationModule
    {
        public OrderingFoundationModule(
            List<string> initLog,
            List<string> frameLog,
            List<string> shutdownLog)
            : base(initLog, frameLog, shutdownLog)
        {
        }

        public override int Priority => 20;
        public override IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        protected override string Name => "foundation";
    }

    public sealed class OrderingFeatureModule : OrderingProbeModule, IOrderingFeatureModule
    {
        public OrderingFeatureModule(
            List<string> initLog,
            List<string> frameLog,
            List<string> shutdownLog)
            : base(initLog, frameLog, shutdownLog)
        {
        }

        public override int Priority => -500;
        public override IReadOnlyList<Type> DependsOn => new[] { typeof(IOrderingFoundationModule) };
        protected override string Name => "feature";
    }

    public sealed class OrderingPresentationModule : OrderingProbeModule, IOrderingPresentationModule
    {
        public OrderingPresentationModule(
            List<string> initLog,
            List<string> frameLog,
            List<string> shutdownLog)
            : base(initLog, frameLog, shutdownLog)
        {
        }

        public override int Priority => -1000;
        public override IReadOnlyList<Type> DependsOn => new[] { typeof(IOrderingFeatureModule) };
        protected override string Name => "presentation";
    }

    public sealed class MissingDependencyProbeModule : IMissingDependencyProbeModule
    {
        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => new[] { typeof(IUnavailableOrderingDependencyModule) };
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
}
