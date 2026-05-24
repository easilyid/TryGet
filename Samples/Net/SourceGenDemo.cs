using System.Collections.Generic;
using TryGet;

namespace TryGet.Samples.Net
{
    /// <summary>
    /// V0.9.5 Iter 3 — Source Generator 自动注册 demo。
    ///
    /// <see cref="GreetingModule"/> 类标了 <see cref="ModuleAttribute"/>，
    /// 编译期 Source Generator 会在此 assembly 生成 <c>__AssemblyManifest_TryGet_Samples_Net</c>，
    /// 该类在 .NET 端通过 <c>[ModuleInitializer]</c>、Unity 端通过
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 自动把注册委托交给
    /// <see cref="AssemblyManifestRegistry"/>。
    ///
    /// <see cref="Bootstrap.CreateHost"/> 在创建 host 时调
    /// <see cref="AssemblyManifestRegistry.ApplyAll"/> 应用所有委托，于是 IGreetingModule
    /// 不需要在 setup 里手动 <c>host.Register&lt;IGreetingModule&gt;(...)</c>。
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
        public void OnInit(IModuleHost host) { }
        public void Shutdown() { }

        public string Greet(string who) => $"Hello from V0.9.5 auto-registered Module, {who}!";
    }
}
