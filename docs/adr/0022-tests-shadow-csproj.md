# ADR-0022: Tests 影子 csproj — EditMode 测试脱离 Unity 可运行

## Status

Accepted（2026/06/14；`dotnet test` 448 通过）

## Context

ADR-0012 用 Core 影子 csproj 让 `Runtime/Core` 在纯 .NET 下可编译（双端门）。但**测试**一直只能在 Unity Editor 的 Test Runner 里跑——GPT 代码质量分析也反复指出「Unity EditMode 当前无法本地 / CI 重跑」是个痛点（每次改动都要手动开 Unity 重跑，无 CI 保护，且 Unity 被占用时连重跑都做不到）。

2026/06/14 核实发现：全部 33 个 EditMode 测试的 asmdef `MyTryGetFramework.Tests` 设 `noEngineReferences: true`，源码零 `using UnityEngine` / 零 `[UnityTest]`（grep 验证）——它们是纯 Core 逻辑 + NUnit。也就是说，这些测试**本来就不需要 Unity**，只是缺一个 Unity 之外的运行入口。

## Decision

**新增 Tests 影子 csproj `ServerProject/MyTryGetFramework.Tests/`，与 Core 影子 csproj（ADR-0012）对称。**

- `AssemblyName = MyTryGetFramework.Tests`，匹配 Core 的 `[assembly: InternalsVisibleTo("MyTryGetFramework.Tests")]`，使测试能访问 Core internal 诊断钩子。
- `<Compile Include="..\..\MyTryGetFramework\Assets\...\Tests\EditMode\**\*.cs" />` 反向引用 Unity 项目下的测试源，**不复制**；新增测试文件无需改 csproj（通配自动覆盖）。
- 引用 Core 影子 csproj + NUnit / NUnit3TestAdapter / Microsoft.NET.Test.Sdk（NUnit 3.x，对齐 Unity Test Framework 谱系的经典 Assert 模型）。
- `dotnet test` 一条命令跑通全部 Core 逻辑测试。

## Consequences

### 好处

- **本地 / CI 可跑全部 Core 测试**，不必开 Unity Editor——补齐 ADR-0012 只覆盖「编译」、未覆盖「测试运行」的缺口。
- 与 Core 双端门对称，形成「测试跨端门」：任何 `using UnityEngine` / `[UnityTest]` 渗入 EditMode 测试都会让此 csproj 编译失败，强制测试保持引擎无关。
- 改 Core 后即时反馈（一条 `dotnet test`），不再依赖手动 Unity 重跑。

### 风险

- NUnit 版本与 Unity Test Framework 可能有细微差异；用 NUnit 3.x 经典 Assert 模型对齐，降低分歧。
- 需要 Unity 引擎能力的测试（PlayMode / MonoBehaviour）**不**属于此工程——它们仍在 Unity 侧跑。本工程只覆盖纯 Core 的 EditMode 测试。

### 不允许的退路

- ✗ 让 EditMode 测试引入 `UnityEngine` 依赖「图方便」：会破坏测试跨端门、使 CI 无法跑。需要引擎的测试归 PlayMode。
- ✗ 复制测试源到 ServerProject：源码必须单一，靠 `<Compile Include>` 反向引用（与 ADR-0012 一致）。

## 关联

- 对标 / 延续：ADR-0012（Core 影子 csproj 双端编译）
- 覆盖验证对象包含：ADR-0021（TGTask 取消模型，`CancellationTests` 31 例）
- 实体：`ServerProject/MyTryGetFramework.Tests/`、`ServerProject/README.md`
