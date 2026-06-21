using Microsoft.CodeAnalysis;

namespace TryGet.SourceGenerator
{
    /// <summary>
    /// C10：生成器对非法输入报告的编译期诊断描述符。
    ///
    /// 此前两个生成器对非法 <c>[Module]</c> / <c>[EventHandler]</c> 输入一律静默 <c>return null</c>，
    /// 开发者写错时无任何编译期反馈、只能在运行时发现「没注册上」。这里改为 <see cref="ReportDiagnostic"/>
    /// （带 Location，IDE 直接红线），来源参考 AlicizaX UIMetaSourceGenerator。
    ///
    /// 诊断 ID 段：TG0001-TG0003 / TG0007 = [Module]；TG0004-TG0006 = [EventHandler]。
    /// </summary>
    internal static class GeneratorDiagnostics
    {
        private const string Category = "TryGet.SourceGenerator";

        public static readonly DiagnosticDescriptor ModuleNotConcrete = new(
            "TG0001",
            "[Module] 必须标记在可实例化的具体类上",
            "[Module] 标记的类型 '{0}' 是 abstract 或 static，生成器无法用 new 实例化注册",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ModuleServiceTypeInvalid = new(
            "TG0002",
            "[Module] 的服务类型必须是接口",
            "[Module(typeof({0}))] 的服务类型必须是接口（与 IModuleSystem.Register<T> 约束一致）",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ModuleDoesNotImplementService = new(
            "TG0003",
            "[Module] 标记的类型未实现声明的服务接口",
            "[Module] 标记的类型未实现其声明的服务接口 '{0}'，生成的 Register 调用将无法编译",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ModuleMissingPublicParameterlessConstructor = new(
            "TG0007",
            "[Module] 标记的类型必须有 public 无参构造器",
            "[Module] 标记的类型 '{0}' 必须有 public 无参构造器，生成器需要用 new T() 创建 Module 实例",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor EventHandlerNotStatic = new(
            "TG0004",
            "[EventHandler] 方法必须是 static",
            "[EventHandler] 方法 '{0}' 必须是 static（生成器以静态方法组形式订阅）",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor EventHandlerInvalidSignature = new(
            "TG0005",
            "[EventHandler] 方法签名不符",
            "[EventHandler] 方法 '{0}' 的签名必须为 void M(TEvent evt)（单参数、返回 void）",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor EventHandlerParamNotStruct = new(
            "TG0006",
            "[EventHandler] 事件参数必须是 struct",
            "[EventHandler] 方法的事件参数类型 '{0}' 必须是 struct（IEventModule 约束 T : struct）",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
    }

    /// <summary>
    /// 诊断信息由 transform 阶段产出、在 RegisterSourceOutput 阶段经 <see cref="ToDiagnostic"/> 还原并报告。
    /// 诊断必须保留 Roslyn 原始 source <see cref="Location"/>；只用 file path/span 重建会变成 external
    /// file location，IDE 无法把 TG0001-TG0006 稳定标到用户源码。
    /// </summary>
    internal readonly record struct DiagnosticInfo(DiagnosticDescriptor Descriptor, Location Location, string MessageArg)
    {
        public Diagnostic ToDiagnostic() =>
            Diagnostic.Create(Descriptor, Location ?? Microsoft.CodeAnalysis.Location.None, MessageArg);

        public static Location GetSourceLocation(ISymbol symbol)
        {
            if (symbol is null) return Microsoft.CodeAnalysis.Location.None;
            foreach (var location in symbol.Locations)
            {
                if (location.SourceTree is not null)
                    return location;
            }
            return Microsoft.CodeAnalysis.Location.None;
        }
    }
}
