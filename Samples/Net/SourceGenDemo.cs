using System.Collections.Generic;
using TryGet;

namespace TryGet.Samples.Net
{
    /// <summary>
    /// V0.9.5 Iter 3 + Iter 5 — Source Generator 自动注册 demo。
    ///
    /// <see cref="GreetingModule"/> 类标了 <see cref="ModuleAttribute"/>，
    /// 编译期 Source Generator 会在此 assembly 生成 <c>__AssemblyManifest_TryGet_Samples_Net</c>，
    /// 该类在 .NET 端通过 <c>[ModuleInitializer]</c>、Unity 端通过
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 自动把注册委托交给
    /// <see cref="ModuleRegistry"/>。
    ///
    /// <see cref="GameplayHandlers.OnTickEvent"/> 标了 <see cref="EventHandlerAttribute"/>，
    /// Generator 在 <c>__EventHandlerManifest_TryGet_Samples_Net</c> 内通过同样的 dual-trigger
    /// init 把 handler 注册到 <see cref="EventHandlerRegistry"/>。
    ///
    /// <see cref="GameLauncher.CreateHost"/> 调 <see cref="ModuleRegistry.ApplyAll"/> +
    /// <see cref="EventHandlerRegistry.ApplyAll"/> 让两者都生效。
    /// </summary>
    public interface IGreetingModule : IModule
    {
        string Greet(string who);
    }

    [Module(typeof(IGreetingModule))]
    public sealed class GreetingModule : IGreetingModule
    {
        public int Priority => 0;
        public IReadOnlyList<System.Type> DependsOn => System.Array.Empty<System.Type>();
        public void OnInit(IModuleSystem host) { }
        public void Shutdown() { }

        public string Greet(string who) => $"Hello from V0.9.5 auto-registered Module, {who}!";
    }

    /// <summary>Iter 5 demo 事件类型。struct 满足 IEventBus 约束。</summary>
    public readonly struct TickEvent
    {
        public readonly int Index;
        public TickEvent(int index) { Index = index; }
    }

    /// <summary>Iter 5 demo handler 容器。带 [EventHandler] 的静态方法被 Generator 自动 Subscribe。</summary>
    public static class GameplayHandlers
    {
        public static int LastTickIndex = -1;

        [EventHandler]
        public static void OnTickEvent(TickEvent evt)
        {
            LastTickIndex = evt.Index;
        }
    }

}

