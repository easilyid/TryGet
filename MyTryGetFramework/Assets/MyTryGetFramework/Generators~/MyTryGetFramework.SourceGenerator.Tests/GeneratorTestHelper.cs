using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TryGet.SourceGenerator.Tests
{
    /// <summary>
    /// 用 Roslyn <see cref="CSharpGeneratorDriver"/> 在纯 .NET 下驱动生成器并收集结果。
    ///
    /// 被测输入代码所需的 TryGet 运行时类型由 <see cref="TryGetStub"/> 内联提供，
    /// 使测试自包含、不依赖 Unity 工程编译。<see cref="Run"/> 既返回生成器报告的诊断，
    /// 也返回「原始输入 + 生成代码」合并 compilation 的编译错误——后者用于验证生成代码
    /// 真能编译（例如锁定 ModuleRegistry 名字、防止生成器再漂移到不存在的类型）。
    /// </summary>
    internal static class GeneratorTestHelper
    {
        /// <summary>
        /// 最小 TryGet 运行时桩。仅含生成器输出代码与被测 attribute 所需的类型，
        /// 形状与 Core 真实定义（IModuleSystem.Register&lt;T&gt; where T:class,IModule 等）一致。
        /// C5 起：包含 RegisterWithMetadata（双轨 API）。
        /// </summary>
        public const string TryGetStub = @"
using System;
namespace TryGet
{
    public interface IModule {}
    public interface IEventModule { void Subscribe<T>(Action<T> handler) where T : struct; }
    public interface IModuleSystem { void Register<T>(T module) where T : class, IModule; }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModuleAttribute : Attribute { public ModuleAttribute(Type serviceType) {} }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EventHandlerAttribute : Attribute {}
    public static class ModuleRegistry {
        public static void RegisterWithMetadata(Type implType, Type serviceType, string asm) {}
        public static void Register(Action<IModuleSystem> registration) {}
    }
    public static class EventHandlerRegistry {
        public static void RegisterWithMetadata(string sig, Type eventType, string asm) {}
        public static void Register(Action<IEventModule> registration) {}
    }
}";

        public sealed class Result
        {
            public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; init; }
            public IReadOnlyList<string> GeneratedSources { get; init; } = new List<string>();
            public ImmutableArray<Diagnostic> CompilationErrors { get; init; }

            public string AllGeneratedText => string.Join("\n", GeneratedSources);

            public bool HasDiagnostic(string id) =>
                GeneratorDiagnostics.Any(d => d.Id == id);

            public int DiagnosticCount(string id) =>
                GeneratorDiagnostics.Count(d => d.Id == id);
        }

        public static Result Run(IIncrementalGenerator generator, string userSource)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(TryGetStub, parseOptions),
                CSharpSyntaxTree.ParseText(userSource, parseOptions),
            };

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                trees,
                Basic.Reference.Assemblies.Net80.References.All,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            var runResult = driver.GetRunResult();

            var generated = runResult.Results
                .SelectMany(r => r.GeneratedSources)
                .Select(s => s.SourceText.ToString())
                .ToList();

            var compileErrors = outputCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();

            return new Result
            {
                GeneratorDiagnostics = runResult.Diagnostics,
                GeneratedSources = generated,
                CompilationErrors = compileErrors,
            };
        }
    }
}
