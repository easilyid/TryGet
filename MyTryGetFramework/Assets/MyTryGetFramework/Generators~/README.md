# MyTryGetFramework Source Generator

TryGet 的编译期自动注册源码生成器 —— **源码工程**，集成在框架包内。

`[Module]` / `[EventHandler]` 标记的业务代码由这里的生成器在编译期产出注册代码（`__AssemblyManifest_*` / `__EventHandlerManifest_*`），零运行时反射。

## 位置：框架内 `Generators~/`，源码随框架走

本工程位于 `MyTryGetFramework/Assets/MyTryGetFramework/Generators~/`。文件夹名以 `~` 结尾，**Unity 会完全忽略它**（不 import、不编译、不要 .meta）—— 所以生成器源码能放在框架包内、随框架分发，又不会被 Unity 当游戏代码编译（生成器引用 `Microsoft.CodeAnalysis`，普通 asmdef 编译会失败）。

这是 Unity 官方包的标准模式：**Netcode for Entities** 把生成器源码放在 `Source~/`，dll 从中构建。

## 为什么仍然需要一个 dll？

Unity 6（含本项目的 6000.3.10f1）无法把 Roslyn 生成器 `.cs` 源码直接当生成器加载 —— 官方文档明确：生成器**必须是预编译 dll**、放进 Assets、打 `RoslynAnalyzer` label，编译管线才会加载它（见 docs.unity3d.com `create-source-generator`、`roslyn-analyzers`）。所以 `Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll` **不能消除**，它是 Unity 加载生成器的唯一形式。

参考框架无一例外都是「独立源码工程 + 进库 dll」：

| 框架 | 生成器源码工程 | 进库的产物 dll |
|---|---|---|
| Unity Netcode for Entities | `Source~/` | `NetCodeSourceGenerator.dll` |
| AlicizaX | `SourceGenerateTools/` | `Plugins/*/EventSourceGenerator.dll`、`UISourceGenerator.dll` |
| MyFramework | `ToolProject/AnalyzerUnity/` | `Analyzers/AnalyzerUnity.dll` |
| BigCat | `Wjybxx.BigCat.Apt` 等 | （.NET 库形态） |

**关键约定：`Generators~/` 下的源码才是事实源（source of truth）；`Runtime/Core/Generators/` 下的 dll 是构建产物。** 改生成器行为只改这里的 `.cs`，再用脚本重建 dll。

## 目录结构

```
MyTryGetFramework/Assets/MyTryGetFramework/
├─ Runtime/Core/Generators/
│  └─ MyTryGetFramework.SourceGenerator.dll        # 产物（带 RoslynAnalyzer label，Unity 加载这个）
└─ Generators~/                                     # 源码工程（Unity 忽略 ~ 文件夹）
   ├─ MyTryGetFramework.SourceGenerator.sln         # 打开它来开发/调试生成器
   ├─ build.ps1 / build.sh                          # 一键：测试 + Release 构建 + 同步 dll
   ├─ MyTryGetFramework.SourceGenerator/            # 生成器本体（netstandard2.0）
   │  └─ src/
   │     ├─ Generators/ModuleManifestGenerator.cs   # [Module] → ModuleRegistry 注册
   │     ├─ Generators/EventHandlerGenerator.cs     # [EventHandler] → EventHandlerRegistry 订阅
   │     ├─ Generators/ModuleInitializerSupportGenerator.cs # ModuleInitializerAttribute polyfill
   │     └─ Diagnostics/GeneratorDiagnostics.cs     # TG0001-TG0007 编译期诊断（C10）
   └─ MyTryGetFramework.SourceGenerator.Tests/      # GeneratorDriver 单元测试（net8.0）
```

## 怎么改 + 怎么重建 dll

1. 改 `Generators~/MyTryGetFramework.SourceGenerator/src/` 下的生成器源码。
2. 在 `Generators~/MyTryGetFramework.SourceGenerator.Tests/` 加/改测试。
3. 跑构建脚本（先测试、后 Release 构建并同步 dll）：
   ```bash
   # 从仓库根：
   pwsh "MyTryGetFramework/Assets/MyTryGetFramework/Generators~/build.ps1"   # Windows
   bash "MyTryGetFramework/Assets/MyTryGetFramework/Generators~/build.sh"    # Git Bash / macOS / Linux
   ```
4. 回 Unity 让其重新 import；把更新后的 `MyTryGetFramework.SourceGenerator.dll` 与源码一起 `git commit`。

只跑测试（不碰 dll）：
```bash
dotnet test "MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.sln"
```

## dll 同步规则

- 生成器 csproj 的 `CopyToUnityAssets` target **仅在 `Release` 构建时触发**同步到 `Runtime/Core/Generators/`。
- `Debug` 构建与 `dotnet test`（依赖生成器工程）**不会**改动 Unity 现役 dll —— 避免日常测试污染二进制、减少 git 漂移。
- `<Deterministic>true</Deterministic>` 确保固定路径下相同源码 + 相同 SDK 产出逐字节一致的 dll。

> 历史教训：V2.0「收敛模块系统命名」把运行时 `AssemblyManifestRegistry` 改名为 `ModuleRegistry`，但漏改了生成器源码，且因为没人重建 dll，错误被掩盖了很久（Unity 用旧 dll 生成正确名字；git 源码却生成已不存在的类）。现在有了测试工程 + 构建脚本，这类漂移会被 `BaselineGenerationTests` 立即捕获。

## Unity 初始化触发

生成的 manifest `Initialize()` 采用双触发：

- `[ModuleInitializer]`：程序集加载时注册，覆盖 Unity Test Runner 中测试程序集不稳定触发 `RuntimeInitializeOnLoadMethod` 的场景。
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`：Unity 运行时入口兜底，并通过 `_initialized` 防重复注册。

`ModuleInitializerSupportGenerator` 会为缺少内置 `ModuleInitializerAttribute` 的目标框架生成 polyfill，保持 Unity / netstandard 目标可编译。

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
| TG0007 | `[Module]` 标记的类没有 public 无参构造器 |
