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
        public void EventHandler_NoHandlers_GeneratesNothing()
        {
            const string src = @"
namespace App { public static class Handlers {} }";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.GeneratedSources, Is.Empty, "没有 [EventHandler] 时不应生成 manifest");
        }
    }
}
