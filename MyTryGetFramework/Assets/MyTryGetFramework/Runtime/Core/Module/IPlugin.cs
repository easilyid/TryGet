using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 横切关注点插件契约（V0.9 起，吸收 hsenl IPlug 设计，命名升级 Init/Dispose → Install/Uninstall）。
    ///
    /// 与 <see cref="IModule"/> 对比：
    /// - <see cref="IModule"/> = "服务"，业务 <c>host.Get&lt;T&gt;()</c> 拉依赖
    /// - <see cref="IPlugin"/> = "横切关注点"，hook 到 <see cref="IPlugPoint"/> 插点上，框架在内部触发
    ///
    /// 一个 plugin 可同时实现多个 <see cref="IPlugPoint"/>，被注册到不同插点 — 同实例共享状态。
    ///
    /// 生命周期：
    /// 1. <see cref="IPluginHost.AddPlugin{TPoint, T}"/> 注册时自动调 <see cref="Install"/>
    /// 2. 框架在插点触发时调 plugin 的对应 <see cref="IPlugPoint"/> 派生方法
    /// 3. <see cref="IPluginHost.RemovePlugin{TPoint, T}"/> 移除时自动调 <see cref="Uninstall"/>
    ///
    /// Priority：同一 <see cref="IPlugPoint"/> 内多 plugin 按 Priority 升序触发（小先大后）。
    /// 不同 IPlugPoint 之间 Priority 不互相影响。
    /// </summary>
    public interface IPlugin
    {
        /// <summary>插件被注册到 host 时调用。可拿 host 引用做初始化。</summary>
        void Install(IPluginHost host);

        /// <summary>插件被移除 / host Shutdown 时调用。释放资源。</summary>
        void Uninstall(IPluginHost host);

        /// <summary>同插点内的触发顺序（小先大后）。无序需求时返 0。</summary>
        int Priority { get; }
    }

    /// <summary>
    /// 插件宿主契约（V0.9 起）。<see cref="IModuleHost"/> 继承此接口让 host 直接可挂插件。
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>注册插件到指定插点 <typeparamref name="TPoint"/>。同时调插件的 <see cref="IPlugin.Install"/>。</summary>
        void AddPlugin<TPoint, T>(T plugin) where TPoint : IPlugPoint where T : IPlugin, TPoint;

        /// <summary>移除注册（按插点 + 类型双键）。返回是否真的移除了。调 <see cref="IPlugin.Uninstall"/>。</summary>
        bool RemovePlugin<TPoint, T>() where TPoint : IPlugPoint where T : IPlugin, TPoint;

        /// <summary>查询指定插点 + 类型的插件实例。未注册返 null。</summary>
        T GetPlugin<TPoint, T>() where TPoint : IPlugPoint where T : class, IPlugin, TPoint;

        /// <summary>枚举指定插点的所有插件（按 Priority 升序）。</summary>
        IEnumerable<TPoint> GetPluginsAt<TPoint>() where TPoint : IPlugPoint;

        /// <summary>已注册插件总数（跨所有插点，诊断用）。一个 plugin 注册到 2 个插点计 2 次。</summary>
        int PluginCount { get; }
    }

    /// <summary>
    /// 插点标记接口（V0.9 起，对应 hsenl IPlugGroup）。
    /// 业务定义具体插点（如 <c>IModuleHostBeforeUpdate</c>）作为 IPlugPoint 派生接口 + 业务方法签名。
    /// </summary>
    public interface IPlugPoint { }
}
