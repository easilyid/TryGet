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
    /// EventModule 在真实 Unity 场景生命周期下的订阅清理验证。
    /// </summary>
    [TestFixture]
    public sealed class EventModulePlayModeTests
    {
        [UnityTest]
        public IEnumerator EventSubscription_PlayModeSceneUnload_UnsubscribesModuleHandler()
        {
            var scene = SceneManager.CreateScene("TryGet_EventUnload_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(EventSubscription_PlayModeSceneUnload_UnsubscribesModuleHandler));
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var entry = gameObject.AddComponent<EventHostEntry>();
            yield return null;

            var bus = entry.EventBus;
            var module = entry.Module;

            Assert.That(entry.HasHost, Is.True);
            Assert.That(module.InitCount, Is.EqualTo(1));
            Assert.That(bus.GetSubscriberCount<SceneLifecycleEvent>(), Is.EqualTo(1));

            bus.Publish(new SceneLifecycleEvent { Value = 3 });
            Assert.That(module.Total, Is.EqualTo(3));

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;

            Assert.That(gameObject == null, Is.True);
            Assert.That(entry == null || !entry.HasHost, Is.True);
            Assert.That(module.ShutdownCount, Is.EqualTo(1));
            Assert.That(bus.GetSubscriberCount<SceneLifecycleEvent>(), Is.EqualTo(0),
                "模块 Shutdown 应解绑它托管的 EventModule 订阅。");

            bus.Publish(new SceneLifecycleEvent { Value = 5 });
            Assert.That(module.Total, Is.EqualTo(3),
                "场景卸载后旧模块 handler 不应继续收到事件。");
        }

        [UnityTest]
        public IEnumerator EventSubscription_PersistentEntry_SceneUnload_KeepsModuleHandlerUntilDestroy()
        {
            var scene = SceneManager.CreateScene("TryGet_EventPersistent_" + Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject(nameof(EventSubscription_PersistentEntry_SceneUnload_KeepsModuleHandlerUntilDestroy));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentEventHostEntry>();
                yield return null;

                var bus = entry.EventBus;
                var module = entry.Module;

                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
                Assert.That(entry.HasHost, Is.True);
                Assert.That(bus.GetSubscriberCount<SceneLifecycleEvent>(), Is.EqualTo(1));

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                Assert.That(gameObject == null, Is.False);
                Assert.That(entry.HasHost, Is.True);
                Assert.That(module.ShutdownCount, Is.EqualTo(0));
                Assert.That(bus.GetSubscriberCount<SceneLifecycleEvent>(), Is.EqualTo(1),
                    "持久 EventHostEntry 卸载原场景时不应解绑模块托管的事件订阅。");

                bus.Publish(new SceneLifecycleEvent { Value = 7 });
                Assert.That(module.Total, Is.EqualTo(7),
                    "持久 EventHostEntry 卸载原场景后，模块 handler 应继续接收 EventModule 事件。");

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry == null || !entry.HasHost, Is.True);
                Assert.That(module.ShutdownCount, Is.EqualTo(1));
                Assert.That(bus.GetSubscriberCount<SceneLifecycleEvent>(), Is.EqualTo(0),
                    "持久 Entry 最终销毁时仍应解绑模块托管的事件订阅。");
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EventPublish_PlayModeUpdate_HandlerExceptionDoesNotStopOtherHandlers()
        {
            var gameObject = new GameObject(nameof(EventPublish_PlayModeUpdate_HandlerExceptionDoesNotStopOtherHandlers));
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<EventExceptionHostEntry>();
                yield return null;

                for (int i = 0; i < 10 && !entry.Module.Published; i++)
                    yield return null;

                var module = entry.Module;
                Assert.That(module.Published, Is.True);
                Assert.That(module.ThrowingHandlerCount, Is.EqualTo(1));
                Assert.That(module.SafeHandlerTotal, Is.EqualTo(9),
                    "某个 handler 抛异常时，同一事件的其它 handler 仍应被调用。");
                Assert.That(module.HandlerExceptionCount, Is.EqualTo(1),
                    "handler 异常应通过 HandlerException 钩子暴露给业务诊断。");
                Assert.That(module.LastExceptionEventType, Is.EqualTo(typeof(HandlerExceptionEvent)));
                Assert.That(module.LastException, Is.TypeOf<InvalidOperationException>());

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

    public struct SceneLifecycleEvent
    {
        public int Value;
    }

    public struct HandlerExceptionEvent
    {
        public int Value;
    }

    public interface ISceneLifecycleEventProbeModule : IModule { }

    public sealed class SceneLifecycleEventProbeModule : ISceneLifecycleEventProbeModule
    {
        private IEventModule _bus;

        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int Total { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            InitCount++;
            _bus = host.EventModule;
            _bus.Subscribe<SceneLifecycleEvent>(OnSceneLifecycleEvent);
        }

        public void Shutdown()
        {
            ShutdownCount++;
            _bus?.Unsubscribe<SceneLifecycleEvent>(OnSceneLifecycleEvent);
            _bus = null;
        }

        private void OnSceneLifecycleEvent(SceneLifecycleEvent evt)
        {
            Total += evt.Value;
        }
    }

    public interface IHandlerExceptionProbeModule : IModule, IUpdateModule { }

    public sealed class HandlerExceptionProbeModule : IHandlerExceptionProbeModule
    {
        private IEventModule _bus;

        public int Priority => 0;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
        public bool Published { get; private set; }
        public int ThrowingHandlerCount { get; private set; }
        public int SafeHandlerTotal { get; private set; }
        public int HandlerExceptionCount { get; private set; }
        public Type LastExceptionEventType { get; private set; }
        public Exception LastException { get; private set; }

        public void OnInit(IModuleSystem host)
        {
            _bus = host.EventModule;
            _bus.HandlerException += OnHandlerException;
            _bus.Subscribe<HandlerExceptionEvent>(ThrowingHandler);
            _bus.Subscribe<HandlerExceptionEvent>(SafeHandler);
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (Published)
                return;

            _bus.Publish(new HandlerExceptionEvent { Value = 9 });
            Published = true;
        }

        public void Shutdown()
        {
            if (_bus != null)
            {
                _bus.Unsubscribe<HandlerExceptionEvent>(ThrowingHandler);
                _bus.Unsubscribe<HandlerExceptionEvent>(SafeHandler);
                _bus.HandlerException -= OnHandlerException;
                _bus = null;
            }
        }

        private void ThrowingHandler(HandlerExceptionEvent evt)
        {
            ThrowingHandlerCount++;
            throw new InvalidOperationException("handler boom");
        }

        private void SafeHandler(HandlerExceptionEvent evt)
        {
            SafeHandlerTotal += evt.Value;
        }

        private void OnHandlerException(Type eventType, Exception exception)
        {
            HandlerExceptionCount++;
            LastExceptionEventType = eventType;
            LastException = exception;
        }
    }

    public sealed class EventHostEntry : TryGetMonoEntry
    {
        public SceneLifecycleEventProbeModule Module { get; } = new SceneLifecycleEventProbeModule();
        public IEventModule EventBus { get; private set; }
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            EventBus = host.EventModule;
            host.Register<ISceneLifecycleEventProbeModule>(Module);
        }
    }

    public sealed class PersistentEventHostEntry : TryGetMonoEntry
    {
        public SceneLifecycleEventProbeModule Module { get; } = new SceneLifecycleEventProbeModule();
        public IEventModule EventBus { get; private set; }
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            EventBus = host.EventModule;
            host.Register<ISceneLifecycleEventProbeModule>(Module);
        }
    }

    public sealed class EventExceptionHostEntry : TryGetMonoEntry
    {
        public HandlerExceptionProbeModule Module { get; } = new HandlerExceptionProbeModule();
        public bool HasHost => Host != null;

        protected override bool MakeDontDestroyOnLoad => false;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<IHandlerExceptionProbeModule>(Module);
        }
    }
}
