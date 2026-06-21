using System;
using System.Collections.Generic;
using TryGet.Unity;
using UnityEngine;

namespace TryGet.Tests.PlayMode
{
    public interface ITrackingModule :
        IModule,
        IEarlyUpdateModule,
        IFixedUpdateModule,
        IUpdateModule,
        ILateUpdateModule,
        IEndOfFrameModule
    {
    }

    public sealed class TrackingModule : ITrackingModule
    {
        private int _order;

        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int EarlyUpdateCount { get; private set; }
        public int FixedUpdateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int LateUpdateCount { get; private set; }
        public int EndOfFrameCount { get; private set; }
        public int FirstEarlyUpdateOrder { get; private set; }
        public int FirstUpdateOrder { get; private set; }
        public int FirstLateUpdateOrder { get; private set; }
        public int FirstEndOfFrameOrder { get; private set; }
        public bool EarlyUpdateReceivedUnityTime { get; private set; }
        public bool FixedUpdateReceivedUnityTime { get; private set; }
        public bool UpdateReceivedUnityTime { get; private set; }
        public bool LateUpdateReceivedUnityTime { get; private set; }
        public bool EndOfFrameReceivedUnityTime { get; private set; }
        public IModuleSystem ObservedHost { get; private set; }

        public void ResetPhaseOrder()
        {
            _order = 0;
            FirstEarlyUpdateOrder = 0;
            FirstUpdateOrder = 0;
            FirstLateUpdateOrder = 0;
            FirstEndOfFrameOrder = 0;
        }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
            ObservedHost = host;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }

        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            EarlyUpdateCount++;
            EarlyUpdateReceivedUnityTime = Approximately(deltaTime, Time.deltaTime) &&
                                           Approximately(unscaledDeltaTime, Time.unscaledDeltaTime);
            if (FirstEarlyUpdateOrder == 0)
                FirstEarlyUpdateOrder = ++_order;
        }

        public void FixedUpdate(float deltaTime, float unscaledDeltaTime)
        {
            FixedUpdateCount++;
            FixedUpdateReceivedUnityTime = Approximately(deltaTime, Time.fixedDeltaTime) &&
                                           Approximately(unscaledDeltaTime, Time.fixedUnscaledDeltaTime);
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            UpdateCount++;
            UpdateReceivedUnityTime = Approximately(deltaTime, Time.deltaTime) &&
                                      Approximately(unscaledDeltaTime, Time.unscaledDeltaTime);
            if (FirstUpdateOrder == 0)
                FirstUpdateOrder = ++_order;
        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            LateUpdateCount++;
            LateUpdateReceivedUnityTime = Approximately(deltaTime, Time.deltaTime) &&
                                          Approximately(unscaledDeltaTime, Time.unscaledDeltaTime);
            if (FirstLateUpdateOrder == 0)
                FirstLateUpdateOrder = ++_order;
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            EndOfFrameCount++;
            EndOfFrameReceivedUnityTime = Approximately(deltaTime, Time.deltaTime) &&
                                          Approximately(unscaledDeltaTime, Time.unscaledDeltaTime);
            if (FirstEndOfFrameOrder == 0)
                FirstEndOfFrameOrder = ++_order;
        }

        private static bool Approximately(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= 0.0001f;
        }
    }

    public sealed class TrackingTryGetMonoEntry : TryGetMonoEntry
    {
        public TrackingModule Module { get; } = new TrackingModule();
        public bool SetupCalled { get; private set; }
        public bool HostWasInitializedDuringSetup { get; private set; }
        public bool HostWasInitializedAfterSetup { get; private set; }
        public bool HasHost => Host != null;
        public IModuleSystem ExposedHost => Host;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            SetupCalled = true;
            HostWasInitializedDuringSetup = host.IsInitialized;
            host.Register<ITrackingModule>(Module);
        }

        protected override void Awake()
        {
            base.Awake();
            HostWasInitializedAfterSetup = Host != null && Host.IsInitialized;
        }
    }
}
