using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TryGet.SourceGenerator
{
    /// <summary>
    /// V0.9.5 Iter 3：扫描 <c>[TryGet.Module(typeof(IFoo))]</c> 标记类，生成
    /// <c>__AssemblyManifest_&lt;asm&gt;</c> 内 dual-trigger init 方法，把 Module 注册到
    /// <c>TryGet.ModuleRegistry</c>。
    ///
    /// 设计要点：
    /// - <see cref="ForAttributeWithMetadataName"/> 入口，避免遍历整个语法树
    /// - <see cref="ModuleInfo"/> 是 value-equatable record（关键 incrementality 性能优化）
    /// - <c>[ModuleInitializer]</c>（C# 9.0+）+ <c>[RuntimeInitializeOnLoadMethod]</c>（Unity）
    ///   dual-trigger，<c>_initialized</c> flag 防重
    /// - <c>[UnityEngine.Scripting.Preserve]</c> 防 IL2CPP strip
    /// - 没有任何 [Module] 时不生成 Manifest 文件（noise-free）
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ModuleManifestGenerator : IIncrementalGenerator
    {
        private const string ModuleAttributeMetadataName = "TryGet.ModuleAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 找到所有 [Module(typeof(IFoo))] 标记的类（不在此过滤非法输入，留到输出阶段报诊断）
            var extractions = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ModuleAttributeMetadataName,
                    predicate: static (node, _) => true,
                    transform: static (ctx, _) => ExtractModule(ctx));

            var compilationAndExtractions = context.CompilationProvider
                .Combine(extractions.Collect());

            context.RegisterSourceOutput(compilationAndExtractions, static (spc, source) =>
            {
                var (compilation, exts) = source;

                // 先报告非法输入诊断（替代历史的静默 return null），再收集合法 Module 生成 manifest
                var modules = ImmutableArray.CreateBuilder<ModuleInfo>();
                foreach (var ext in exts)
                {
                    if (ext.Diagnostic is { } diagnostic)
                        spc.ReportDiagnostic(diagnostic.ToDiagnostic());
                    if (ext.Info is { } info)
                        modules.Add(info);
                }

                if (modules.Count == 0) return;

                string assemblyName = compilation.AssemblyName ?? "Unknown";
                string safeAsmId = MakeSafeIdentifier(assemblyName);
                spc.AddSource($"__AssemblyManifest_{safeAsmId}.g.cs", GenerateManifest(safeAsmId, modules.ToImmutable(), assemblyName));
            });
        }

        private static ModuleExtraction ExtractModule(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
                return default; // attribute 限定标在 class 上，正常不会发生

            if (classSymbol.IsAbstract || classSymbol.IsStatic)
                return Fail(GeneratorDiagnostics.ModuleNotConcrete, classSymbol, classSymbol.Name);

            // 取 [Module(typeof(IFoo))] 的第一个构造器参数
            var attr = ctx.Attributes.FirstOrDefault();
            if (attr is null || attr.ConstructorArguments.Length == 0)
                return default; // 缺 typeof 参数时编译器已对 attribute 自身报错

            var serviceTypeArg = attr.ConstructorArguments[0];
            if (serviceTypeArg.Value is not INamedTypeSymbol serviceTypeSymbol)
                return Fail(GeneratorDiagnostics.ModuleServiceTypeInvalid, classSymbol,
                    serviceTypeArg.Value?.ToString() ?? "?");

            if (serviceTypeSymbol.TypeKind != TypeKind.Interface)
                return Fail(GeneratorDiagnostics.ModuleServiceTypeInvalid, classSymbol, serviceTypeSymbol.Name);

            // C10：补上 attribute 注释一直声称、却从未执行的检查——类必须实现声明的服务接口。
            // 否则生成的 host.Register<IFoo>(new Bar()) 会在 IFoo 处编译失败，错误指向生成代码而非用户代码。
            if (!ImplementsInterface(classSymbol, serviceTypeSymbol))
                return Fail(GeneratorDiagnostics.ModuleDoesNotImplementService, classSymbol,
                    serviceTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            string classFullName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string serviceFullName = serviceTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new ModuleExtraction(new ModuleInfo(classFullName, serviceFullName), null);
        }

        private static ModuleExtraction Fail(DiagnosticDescriptor descriptor, ISymbol locationSymbol, string messageArg)
            => new(null, new DiagnosticInfo(descriptor, LocationInfo.From(locationSymbol), messageArg));

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol iface)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, iface))
                    return true;
            }
            return false;
        }

        private static string GenerateManifest(string safeAsmId, ImmutableArray<ModuleInfo> modules, string assemblyName)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("// <auto-generated by TryGet.SourceGenerator (V0.9.5 ModuleManifestGenerator) />");
            sb.AppendLine("#nullable disable");
            sb.AppendLine("namespace TryGet.Generated");
            sb.AppendLine("{");
            sb.Append("    internal static class __AssemblyManifest_").AppendLine(safeAsmId);
            sb.AppendLine("    {");
            sb.AppendLine("        private static bool _initialized;");
            sb.AppendLine();
            sb.AppendLine("#if UNITY_5_3_OR_NEWER");
            sb.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
            sb.AppendLine("        [global::UnityEngine.Scripting.Preserve]");
            sb.AppendLine("#endif");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("        public static void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_initialized) return;");
            sb.AppendLine("            _initialized = true;");
            sb.AppendLine();

            // C5：先注册所有 Module 的结构化元数据（双轨 API 新路径）
            var sorted = modules.Sort(static (a, b) => string.CompareOrdinal(a.ClassFullName, b.ClassFullName));
            foreach (var m in sorted)
            {
                sb.Append("            global::TryGet.ModuleRegistry.RegisterWithMetadata(")
                  .Append("typeof(").Append(m.ClassFullName).Append("), ")
                  .Append("typeof(").Append(m.ServiceFullName).Append("), ")
                  .Append('"').Append(assemblyName).AppendLine("\");");
            }

            sb.AppendLine();
            // 再注册执行委托（向后兼容的旧路径）
            sb.AppendLine("            global::TryGet.ModuleRegistry.Register(host =>");
            sb.AppendLine("            {");
            foreach (var m in sorted)
            {
                sb.Append("                host.Register<")
                  .Append(m.ServiceFullName)
                  .Append(">(new ")
                  .Append(m.ClassFullName)
                  .AppendLine("());");
            }

            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string MakeSafeIdentifier(string assemblyName)
        {
            var sb = new StringBuilder(assemblyName.Length);
            foreach (char c in assemblyName)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Value-equatable record（关键 incrementality 性能优化）。
    /// 仅含 string 字段，不持 ISymbol / SyntaxNode 引用，避免 Compilation 锁定。
    /// </summary>
    internal readonly record struct ModuleInfo(string ClassFullName, string ServiceFullName);

    /// <summary>
    /// transform 阶段的产出：合法 Module 的 <see cref="ModuleInfo"/>，或非法输入的 <see cref="DiagnosticInfo"/>。
    /// 两者都可缓存（值相等 / descriptor 引用稳定），不破坏 incremental pipeline。
    /// </summary>
    internal readonly record struct ModuleExtraction(ModuleInfo? Info, DiagnosticInfo? Diagnostic);
}

namespace System.Runtime.CompilerServices
{
    // netstandard2.0 polyfill for C# 9+ records / init accessors
    internal static class IsExternalInit { }
}
