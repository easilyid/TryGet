using System.Runtime.CompilerServices;

// V0.6 起：让 EditMode Test 程序集能访问 internal 类型（TaskBody.Reset / ITaskBody.Version 等），
// 用于覆盖 ITask Version 防过期等内部行为。生产代码仍保持 internal 不暴露。
[assembly: InternalsVisibleTo("MyTryGetFramework.Tests")]
