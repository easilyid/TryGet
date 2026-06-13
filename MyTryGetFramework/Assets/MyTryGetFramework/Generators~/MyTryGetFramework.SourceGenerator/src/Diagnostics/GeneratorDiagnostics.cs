using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TryGet.SourceGenerator
{
    /// <summary>
    /// C10：生成器对非法输入报告的编译期诊断描述符。
    ///
    /// 此前两个生成器对非法 <c>[Module]</c> / <c>[EventHandler]</c> 输入一律静默 <c>return null</c>，
    /// 开发者写错时无任何编译期反馈、只能在运行时发现「没注册上」。这里改为 <see cref="ReportDiagnostic"/>
    /// （带 Location，IDE 直接红线），来源参考 AlicizaX UIMetaSourceGenerator。
    ///
    /// 诊断 ID 段：TG0001-TG0003 = [Module]；TG0004-TG0006 = [EventHandler]。
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
    /// 可缓存的位置信息。不持有 <see cref="SyntaxNode"/> / <see cref="ISymbol"/> / <see cref="Location"/>
    /// 引用（那些会锁定 Compilation、破坏 incremental 缓存），仅存值类型字段，在输出阶段重建 Location。
    /// </summary>
    internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
    {
        public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

        public static LocationInfo? From(SyntaxNode? node)
        {
            if (node is null) return null;
            return From(node.GetLocation());
        }

        public static LocationInfo? From(ISymbol? symbol)
        {
            if (symbol is null) return null;
            foreach (var loc in symbol.Locations)
            {
                var info = From(loc);
                if (info is not null) return info;
            }
            return null;
        }

        private static LocationInfo? From(Location location)
        {
            if (location.SourceTree is null) return null;
            return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }
    }

    /// <summary>
    /// 可缓存的诊断信息（descriptor 引用稳定 + LocationInfo 值相等 + 单字符串参数），
    /// 由 transform 阶段产出、在 RegisterSourceOutput 阶段经 <see cref="ToDiagnostic"/> 还原并报告。
    /// </summary>
    internal readonly record struct DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo? Location, string MessageArg)
    {
        public Diagnostic ToDiagnostic() =>
            Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, MessageArg);
    }
}
