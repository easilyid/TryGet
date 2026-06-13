# MyTryGetFramework Source Generator

TryGet 的编译期自动注册源码生成器 —— **源码工程**。

`[Module]` / `[EventHandler]` 标记的业务代码由这里的生成器在编译期产出注册代码（`__AssemblyManifest_*` / `__EventHandlerManifest_*`），零运行时反射。

## 为什么 Unity 里是一个 dll？

Unity 无法把 Roslyn 增量生成器的 `.cs` 源码直接当生成器编译（生成器需 netstandard2.0 + 引用 `Microsoft.CodeAnalysis`，且必须在编译器宿主里加载）。所以**所有 Unity 生态的生成器/分析器都以预编译 dll 形式进库** —— 参考框架无一例外：

| 框架 | 生成器源码工程 | 进 Unity/库的产物 |
|---|---|---|
| AlicizaX | `SourceGenerateTools/` | `Plugins/*/EventSourceGenerator.dll`、`UISourceGenerator.dll` |
| MyFramework | `ToolProject/AnalyzerUnity/` | `Analyzers/AnalyzerUnity.dll` |
| BigCat | `Wjybxx.BigCat.Apt` 等 | （.NET 库形态） |

因此 `MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll` **不能消除**，它是 Unity 加载生成器的唯一形式。

**关键约定：本目录的源码才是事实源（source of truth）；那个 dll 是构建产物。** 任何对生成器行为的修改都改这里的 `.cs`，再用下面的脚本重建 dll。

## 目录结构

```
Tools/
├─ MyTryGetFramework.SourceGenerator.sln          # 打开它来开发/调试生成器
├─ build.ps1 / build.sh                           # 一键：测试 + Release 构建 + 同步 dll
├─ MyTryGetFramework.SourceGenerator/             # 生成器本体（netstandard2.0）
│  └─ src/
│     ├─ Generators/ModuleManifestGenerator.cs    # [Module] → ModuleRegistry 注册
│     ├─ Generators/EventHandlerGenerator.cs      # [EventHandler] → EventHandlerRegistry 订阅
│     └─ Diagnostics/GeneratorDiagnostics.cs      # TG0001-TG0006 编译期诊断（C10）
└─ MyTryGetFramework.SourceGenerator.Tests/       # GeneratorDriver 单元测试（net8.0）
```

## 怎么改 + 怎么重建 dll

1. 改 `MyTryGetFramework.SourceGenerator/src/` 下的生成器源码。
2. 在 `MyTryGetFramework.SourceGenerator.Tests/` 加/改测试。
3. 跑构建脚本（先测试、后 Release 构建并同步 dll）：
   ```bash
   pwsh Tools/build.ps1      # Windows
   ./Tools/build.sh          # Git Bash / macOS / Linux
   ```
4. 回 Unity 让其重新 import；把更新后的 `MyTryGetFramework.SourceGenerator.dll` 与源码一起 `git commit`。

只跑测试（不碰 dll）：
```bash
dotnet test Tools/MyTryGetFramework.SourceGenerator.sln
```

## dll 同步规则

- 生成器 csproj 的 `CopyToUnityAssets` target **仅在 `Release` 构建时触发**同步。
- `Debug` 构建与 `dotnet test`（依赖生成器工程）**不会**改动 Unity 现役 dll —— 避免日常测试污染二进制、减少 git 漂移。
- `<Deterministic>true</Deterministic>` 确保相同源码 + 相同 SDK 产出逐字节一致的 dll。

> 历史教训：V2.0「收敛模块系统命名」把运行时 `AssemblyManifestRegistry` 改名为 `ModuleRegistry`，但漏改了这里的生成器源码，且因为没人重建 dll，错误被掩盖了很久（Unity 用的是旧 dll、生成正确名字；git 源码却生成已不存在的类）。现在有了测试工程 + 构建脚本，这类漂移会被 `BaselineGenerationTests` 立即捕获。

## 编译期诊断（C10）

非法的 `[Module]` / `[EventHandler]` 用法不再被静默丢弃，而是报编译错误（带源码位置）：

| ID | 触发条件 |
|---|---|
| TG0001 | `[Module]` 标在 abstract / static 类上 |
| TG0002 | `[Module]` 的服务类型不是接口 |
| TG0003 | `[Module]` 标记的类未实现声明的服务接口 |
| TG0004 | `[EventHandler]` 方法不是 static |
| TG0005 | `[EventHandler]` 方法签名不是 `void M(TEvent evt)` |
| TG0006 | `[EventHandler]` 事件参数不是 struct |
