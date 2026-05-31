using System.Runtime.CompilerServices;

// 让 EditMode Test 程序集能访问 Core internal 类型，
// 用于覆盖 TGTask、EventModule 等内部诊断与测试钩子。生产代码仍保持 internal 不暴露。
[assembly: InternalsVisibleTo("MyTryGetFramework.Tests")]
