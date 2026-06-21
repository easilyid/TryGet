using NUnit.Framework;

namespace TryGet.SourceGenerator.Tests
{
    /// <summary>
    /// C10：生成器对非法 [Module] / [EventHandler] 输入报告编译期诊断（替代历史的静默 return null）。
    /// 每个用例的用户代码本身能解析，仅触发生成器的契约检查。
    /// </summary>
    [TestFixture]
    public class GeneratorDiagnosticsTests
    {
        // ---------- [Module] : TG0001-TG0003 / TG0007 ----------

        [Test]
        public void TG0001_ModuleOnAbstractClass_Reported()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public abstract class Foo : IFoo {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.HasDiagnostic("TG0001"), Is.True, "abstract 类标 [Module] 应报 TG0001");
            Assert.That(result.GeneratedSources, Is.Empty, "非法输入不应生成 manifest");
        }

        [Test]
        public void TG0002_ServiceTypeNotInterface_Reported()
        {
            const string src = @"
namespace App
{
    public class Concrete {}

    [TryGet.Module(typeof(Concrete))]
    public sealed class Foo : TryGet.IModule {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.HasDiagnostic("TG0002"), Is.True, "服务类型不是接口应报 TG0002");
        }

        [Test]
        public void TG0003_ClassDoesNotImplementService_Reported()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : TryGet.IModule {} // 实现 IModule 但未实现 IFoo
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.HasDiagnostic("TG0003"), Is.True, "类未实现声明的服务接口应报 TG0003");
            Assert.That(result.GeneratedSources, Is.Empty,
                "未实现接口时不应生成会编译失败的 Register 调用");
        }

        [Test]
        public void TG0007_ModuleWithoutPublicParameterlessConstructor_Reported()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}
    public interface IBar : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo
    {
        public Foo(int value) {}
    }

    [TryGet.Module(typeof(IBar))]
    public sealed class Bar : IBar
    {
        private Bar() {}
    }
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.DiagnosticCount("TG0007"), Is.EqualTo(2),
                "没有 public parameterless constructor 的 [Module] 类型应各报一次 TG0007");
            Assert.That(result.GeneratedSources, Is.Empty,
                "无公共无参构造器时不应生成会编译失败的 Register 调用");
        }

        // ---------- [EventHandler] : TG0004-TG0006 ----------

        [Test]
        public void TG0004_HandlerNotStatic_Reported()
        {
            const string src = @"
namespace App
{
    public struct Ev { public int V; }

    public class Handlers
    {
        [TryGet.EventHandler]
        public void OnEv(Ev e) {}
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.HasDiagnostic("TG0004"), Is.True, "非 static handler 应报 TG0004");
        }

        [Test]
        public void TG0005_HandlerWrongSignature_Reported()
        {
            const string src = @"
namespace App
{
    public struct Ev { public int V; }

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static int OnEv(Ev e) => 0; // 返回非 void

        [TryGet.EventHandler]
        public static void TwoArgs(Ev a, Ev b) {} // 参数数 != 1
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.DiagnosticCount("TG0005"), Is.EqualTo(2), "两个签名不符方法应各报一次 TG0005");
        }

        [Test]
        public void TG0006_EventParamNotStruct_Reported()
        {
            const string src = @"
namespace App
{
    public class NotStruct {}

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static void OnEv(NotStruct e) {}
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.HasDiagnostic("TG0006"), Is.True, "事件参数非 struct 应报 TG0006");
        }

        [Test]
        public void GeneratorDiagnostics_HaveSourceLocation()
        {
            AssertDiagnosticHasSourceLocation("TG0001", GeneratorTestHelper.Run(new ModuleManifestGenerator(), @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public abstract class Foo : IFoo {}
}"));
            AssertDiagnosticHasSourceLocation("TG0002", GeneratorTestHelper.Run(new ModuleManifestGenerator(), @"
namespace App
{
    public class Concrete {}

    [TryGet.Module(typeof(Concrete))]
    public sealed class Foo : TryGet.IModule {}
}"));
            AssertDiagnosticHasSourceLocation("TG0003", GeneratorTestHelper.Run(new ModuleManifestGenerator(), @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : TryGet.IModule {}
}"));
            AssertDiagnosticHasSourceLocation("TG0007", GeneratorTestHelper.Run(new ModuleManifestGenerator(), @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo
    {
        public Foo(int value) {}
    }
}"));
            AssertDiagnosticHasSourceLocation("TG0004", GeneratorTestHelper.Run(new EventHandlerGenerator(), @"
namespace App
{
    public struct Ev { public int V; }

    public class Handlers
    {
        [TryGet.EventHandler]
        public void OnEv(Ev e) {}
    }
}"));
            AssertDiagnosticHasSourceLocation("TG0005", GeneratorTestHelper.Run(new EventHandlerGenerator(), @"
namespace App
{
    public struct Ev { public int V; }

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static int OnEv(Ev e) => 0;
    }
}"));
            AssertDiagnosticHasSourceLocation("TG0006", GeneratorTestHelper.Run(new EventHandlerGenerator(), @"
namespace App
{
    public class NotStruct {}

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static void OnEv(NotStruct e) {}
    }
}"));
        }

        // ---------- 合法输入不误报 ----------

        [Test]
        public void ValidModule_NoDiagnostics()
        {
            const string src = @"
namespace App
{
    public interface IFoo : TryGet.IModule {}

    [TryGet.Module(typeof(IFoo))]
    public sealed class Foo : IFoo {}
}";
            var result = GeneratorTestHelper.Run(new ModuleManifestGenerator(), src);

            Assert.That(result.GeneratorDiagnostics, Is.Empty, "合法 Module 不应报任何诊断");
        }

        [Test]
        public void ValidEventHandler_NoDiagnostics()
        {
            const string src = @"
namespace App
{
    public struct Ev { public int V; }

    public static class Handlers
    {
        [TryGet.EventHandler]
        public static void OnEv(Ev e) {}
    }
}";
            var result = GeneratorTestHelper.Run(new EventHandlerGenerator(), src);

            Assert.That(result.GeneratorDiagnostics, Is.Empty, "合法 handler 不应报任何诊断");
        }

        private static void AssertDiagnosticHasSourceLocation(string id, GeneratorTestHelper.Result result)
        {
            foreach (var diagnostic in result.GeneratorDiagnostics)
            {
                if (diagnostic.Id != id)
                    continue;

                Assert.That(diagnostic.Location, Is.Not.EqualTo(Microsoft.CodeAnalysis.Location.None),
                    $"{id} 应指向用户源码位置，而不是 Location.None。");
                Assert.That(diagnostic.Location.IsInSource, Is.True,
                    $"{id} 应能让 IDE 在用户源码上画红线。");
                return;
            }

            Assert.Fail($"{id} was not reported.");
        }
    }
}
