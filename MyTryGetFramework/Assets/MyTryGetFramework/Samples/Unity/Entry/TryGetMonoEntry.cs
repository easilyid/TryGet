using UnityEngine;

namespace TryGet.Samples.Unity
{
    /// <summary>
    /// V1.0 起 — Unity 端启动模板（MonoBehaviour）。
    ///
    /// 业务子类化此 abstract 类并 override <see cref="Setup"/> 注册自己的 Module。
    /// MonoBehaviour 生命周期事件桥接到 <see cref="ModuleHost"/>：
    /// <list type="bullet">
    ///   <item>Awake → <see cref="Bootstrap.CreateHost"/> + <see cref="Setup"/> + <see cref="IModuleHost.Initialize"/></item>
    ///   <item>Update → <see cref="IModuleHost.Update"/></item>
    ///   <item>LateUpdate → <see cref="IModuleHost.LateUpdate"/></item>
    ///   <item>OnDestroy → <see cref="IModuleHost.Shutdown"/></item>
    /// </list>
    ///
    /// **使用范本**：
    /// <code>
    /// public sealed class MyGameEntry : TryGetMonoEntry
    /// {
    ///     protected override BootstrapOptions Options => new BootstrapOptions { MinimumLogLevel = LogLevel.Debug };
    ///
    ///     protected override void Setup(IModuleHost host)
    ///     {
    ///         host.Register&lt;ITimerModule&gt;(new TimerModule());
    ///         host.Register&lt;IProcedureModule&gt;(new ProcedureModule());
    ///         // ... 业务 Module
    ///     }
    /// }
    /// </code>
    ///
    /// 在场景中挂一个 GameObject + MyGameEntry 组件，Unity Play 即启动框架。
    ///
    /// **不定义 IEntry interface 的理由**（ADR 风格说明）：
    /// 与 V0.7 <c>Samples/Net/Entry.cs</c> 一致 — 调研 Fantasy / ET / BigCat / hsenl 5 框架，
    /// 无一定义 IEntry interface。Unity MonoBehaviour 入口（无返回，事件驱动）与
    /// .NET <c>Main(string[])</c>（int 返回，主循环）形态本质不同，强行抽象 interface 增加心智成本
    /// 不带来灵活性。
    ///
    /// **DontDestroyOnLoad**：默认开启，让 host 跨场景持续。业务可在子类 Awake 内 override
    /// 或干脆把 GameObject 放进 Bootstrap 场景永远不切走。
    /// </summary>
    public abstract class TryGetMonoEntry : MonoBehaviour
    {
        /// <summary>当前 host。在 Awake 之后可用，OnDestroy 之后置 null。</summary>
        protected IModuleHost Host { get; private set; }

        /// <summary>子类可 override 提供自定义 Logger / Clock / Scheduler。</summary>
        protected virtual BootstrapOptions Options => BootstrapOptions.Default;

        /// <summary>子类必须 override 注册业务 Module。在 <see cref="IModuleHost.Initialize"/> 之前调用。</summary>
        protected abstract void Setup(IModuleHost host);

        /// <summary>子类可 override 控制 DontDestroyOnLoad 行为。默认 true。</summary>
        protected virtual bool MakeDontDestroyOnLoad => true;

        protected virtual void Awake()
        {
            if (MakeDontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            Host = Bootstrap.CreateHost(Options);
            Setup(Host);
            Host.Initialize();
        }

        protected virtual void Update()
        {
            Host?.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        protected virtual void LateUpdate()
        {
            Host?.LateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
        }

        protected virtual void OnDestroy()
        {
            Host?.Shutdown();
            Host = null;
        }
    }
}
