using NUnit.Framework;

namespace TryGet.SourceGenerator.Tests
{
    /// <summary>
    /// 生成器在「合法输入」下的基线行为锁定。
    ///
    /// 关键回归点：ModuleManifestGenerator 必须生成对 <c>TryGet.ModuleRegistry</c> 的调用
    /// （而非历史命名 AssemblyManifestRegistry）；这通过断言生成代码与桩一起编译无错来保证——
    /// 若生成器再次漂移到不存在的注册表类型，CompilationErrors 非空，本测试失败。
    /// </summary>
    [TestFixture]
    public class BaselineGenerationTests
    {
        [Test]
        public void ModuleManifest_ValidModule_GeneratesModuleRegistryCall()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.AllGeneratedText, Does.Contain("global::TryGet.ModuleRegistry.Register"),
                "应生成对 ModuleRegistry 的注册调用");
            Assert.That(result.AllGeneratedText, Does.Contain("host.Register<global::App.IFoo>(new global::App.Foo())"),
                "应生成 host.Register<服务接口>(new 实现())");
            Assert.That(result.CompilationErrors, Is.Empty,
                "生成代码必须与运行时桩一起编译通过（锁定 ModuleRegistry 名字）：\n" + result.AllGeneratedText);
        }

        [Test]
        public void ModuleManifest_NestedNamespace_GeneratesFullyQualifiedNames()
        {
            const string src = @"
namespace App.Sub.Deep
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.CompilationErrors, Is.Empty, result.AllGeneratedText);
            Assert.That(result.AllGeneratedText,
                Does.Contain("host.Register<global::App.Sub.Deep.IFoo>(new global::App.Sub.Deep.Foo())"),
                "嵌套命名空间下的模块注册必须生成完全限定名，避免 Unity 工程常见 namespace 层级下解析漂移。");
        }

        [Test]
        public void ModuleManifest_UnityBuild_UsesRuntimeInitializeAndModuleInitializer()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src, "UNITY_5_3_OR_NEWER");

            Assert.That(result.CompilationErrors, Is.Empty, result.AllGeneratedText);
            Assert.That(result.AllGeneratedText, Does.Contain("RuntimeInitializeOnLoadMethod"));
            Assert.That(result.AllGeneratedText, Does.Contain("ModuleInitializer"));
            Assert.That(result.AllGeneratedText, Does.Contain("if (_initialized && IsRegistered()) return;"),
                "Registry 被测试清空后，Unity runtime init 再次触发时应允许重新注册。");
            Assert.That(result.AllGeneratedText, Does.Contain("global::TryGet.ModuleRegistry.Snapshot()"));
        }

        [Test]
        public void ModuleManifest_NoModules_GeneratesNothing()
        {
            const string src = @"
namespace App { public sealed class Plain {} }";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.GeneratedSources, Is.Empty, "没有 [Module] 时不应生成 manifest");
        }

        [Test]
        public void EventHandler_ValidHandler_GeneratesSubscribeCall()
        {
            const string src = @"
namespace App
{
    public struct DamageEvent { public int Amount; }

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static void OnDamage(DamageEvent evt) {}
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.AllGeneratedText, Does.Contain("global::TryGet.EventHandlerRegistry.Register"),
                "应生成对 EventHandlerRegistry 的注册调用");
            Assert.That(result.AllGeneratedText, Does.Contain("bus.Subscribe<global::App.DamageEvent>(global::App.Handlers.OnDamage)"),
                "应生成 bus.Subscribe<事件>(处理方法)");
            Assert.That(result.CompilationErrors, Is.Empty,
                "生成代码必须编译通过：\n" + result.AllGeneratedText);
        }

        [Test]
        public void EventHandler_UnityBuild_UsesRuntimeInitializeAndModuleInitializer()
        {
            const string src = @"
namespace App
{
    public struct DamageEvent { public int Amount; }

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static void OnDamage(DamageEvent evt) {}
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src, "UNITY_5_3_OR_NEWER");

            Assert.That(result.CompilationErrors, Is.Empty, result.AllGeneratedText);
            Assert.That(result.AllGeneratedText, Does.Contain("RuntimeInitializeOnLoadMethod"));
            Assert.That(result.AllGeneratedText, Does.Contain("ModuleInitializer"));
            Assert.That(result.AllGeneratedText, Does.Contain("if (_initialized && IsRegistered()) return;"),
                "Registry 被测试清空后，Unity runtime init 再次触发时应允许重新注册。");
            Assert.That(result.AllGeneratedText, Does.Contain("global::TryGet.EventHandlerRegistry.Snapshot()"));
        }

        [Test]
        public void ModuleInitializerSupport_GeneratesPolyfill()
        {
            var result = GeneratorTestHelper.Run(new ModuleInitializerSupportGenerator(), "namespace App { public sealed class Plain {} }");

            Assert.That(result.CompilationErrors, Is.Empty, result.AllGeneratedText);
            Assert.That(result.AllGeneratedText, Does.Contain("ModuleInitializerAttribute"));
        }

        [Test]
        public void EventHandler_NoHandlers_GeneratesNothing()
        {
            const string src = @"
namespace App { public static class Handlers {} }";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.GeneratedSources, Is.Empty, "没有 [EventHandler] 时不应生成 manifest");
        }

        // ========== C5 Snapshot 修复回归点：单程序集多条目 → N metadata + 1 register ==========
        // 这是 ModuleRegistry/EventHandlerRegistry.Snapshot 必须按 metadata 数（而非委托数）计长的根因输入：
        // 若按委托数（每程序集 1）计长，单程序集 2+ 模块/handler 时 Snapshot 会截断丢弃多余 metadata。

        [Test]
        public void ModuleManifest_TwoModulesOneAssembly_EmitsTwoMetadataOneRegister()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}
    public interface IBar : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo {}

    [TryGet.Module(typeof(IBar))]
    public sealed class Bar : IBar {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.CompilationErrors, Is.Empty, result.AllGeneratedText);
            Assert.That(CountOf(result.AllGeneratedText, "global::TryGet.ModuleRegistry.RegisterWithMetadata("),
                Is.EqualTo(2), "每个 [Module] 应各发一条 RegisterWithMetadata（metadata 数 = 模块数）");
            Assert.That(CountOf(result.AllGeneratedText, "global::TryGet.ModuleRegistry.Register(host =>"),
                Is.EqualTo(1), "整个程序集只发一个 Register 委托（N:1，正是 Snapshot 易丢 metadata 的场景）");
        }

        [Test]
        public void EventHandler_TwoHandlersOneAssembly_EmitsTwoMetadataOneRegister()
        {
            const string src = @"
namespace App
{
    public struct DamageEvent { public int Amount; }
    public struct HealEvent { public int Amount; }

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static void OnDamage(DamageEvent evt) {}

        [TryGet.EventHandler]
        public static void OnHeal(HealEvent evt) {}
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.CompilationErrors, Is.Empty, result.AllGeneratedText);
            Assert.That(CountOf(result.AllGeneratedText, "global::TryGet.EventHandlerRegistry.RegisterWithMetadata("),
                Is.EqualTo(2), "每个 [EventHandler] 应各发一条 RegisterWithMetadata（metadata 数 = handler 数）");
            Assert.That(CountOf(result.AllGeneratedText, "global::TryGet.EventHandlerRegistry.Register(bus =>"),
                Is.EqualTo(1), "整个程序集只发一个 Register 委托（N:1，正是 Snapshot 易丢 metadata 的场景）");
        }

        private static int CountOf(string haystack, string needle)
        {
            int count = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += needle.Length;
            }
            return count;
        }
    }
}
