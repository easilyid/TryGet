# Shadow csproj 双端编译模式

## Status

Accepted

## Context

V2 定位（见 `.scratch/framework-design-v2/design.md` §1、§4）要求"客户端为主、双端门不挡死"：以 Unity 6.3 LTS 为基线，同时让 Core/Network/Common 三个跨端 asmdef 未来可在 .NET 8 Console 服务端复用。

Unity asmdef 的 `noEngineReferences: true` 选项**只阻止 Unity 引擎 dll 被引用**，并不会让该 asmdef 自动可用于纯 .NET 编译——asmdef 本身是 Unity 编译系统私有的配置，dotnet CLI / Rider .NET 项目 / CI 服务端流水线都不认识它。换句话说：单靠 asmdef 拦不住 `using UnityEngine`，但拦不住不等于能跑到服务端。

参考方案对比：
- **ET 框架**：源码物理上放在共享目录，Unity 端走 asmdef，服务端走手写 .csproj 引用同一批 .cs 文件。
- **Fantasy 框架**：同样的 Shadow csproj 模式，每个跨端模块旁边一份镜像 csproj。
- **纯 asmdef 方案**：不可行，dotnet CLI 不识别。
- **纯 csproj 方案**：Unity 端会卡 Asset 导入 / Player Build 流程。
- **代码生成器方案**：从 asmdef 自动生成 csproj（如 Unity 默认导出），生成产物不稳定，远期维护成本高。

V0.1 已规划了 Core/Network/Common 三个 noEngineReferences asmdef 作为双端候选，但当时没落实编译验证手段，"双端门"只是 lint，不是 build。

## Decision

Core / Network / Common 三个跨端 asmdef 各自配一份同名 `.csproj`，源码物理共享、编译产物分离。**Shadow csproj 模式**在 V0.2 立项时就落实，避免拖到 V2.0 服务端阶段再重切目录结构。

**布局（详见 design.md §4）：**

```
Assets/MyTryGetFramework/Runtime/
├── Core/        MyTryGetFramework.Core.asmdef        + MyTryGetFramework.Core.csproj
├── Network/     MyTryGetFramework.Network.asmdef     + MyTryGetFramework.Network.csproj
├── Common/     MyTryGetFramework.Common.asmdef      + MyTryGetFramework.Common.csproj
├── Unity/       MyTryGetFramework.Unity.asmdef       (仅 asmdef，Unity 限定)
└── Adapters/    各自独立 asmdef（可选搭配镜像 csproj）

ServerProject/                              ← 远期 .NET 8 视角
├── MyTryGetFramework.Core/                 → 引用 Assets/.../Core/**.cs
├── MyTryGetFramework.Network/
├── MyTryGetFramework.Common/
└── MyTryGetFramework.Server/               ← 服务端业务
```

**规则：**

1. **每个跨端 asmdef 旁边必有同名 csproj**。命名一致、根命名空间一致、目标 framework 取 `netstandard2.1` 以兼容 Unity 和 .NET 8。
2. **源码 100% 共享**：csproj 通过 `<Compile Include="..\..\Assets\...\**\*.cs" />` 引用 Unity 目录下的 .cs，不复制。
3. **编译产物分离**：Unity 用 asmdef 产出 Player Build 用的 dll；dotnet CLI 用 csproj 产出服务端 dll；两份 dll 不互换。
4. **CI 双跑**：V0.2 Gate Criteria 要求至少 `dotnet build Core.csproj` 在纯 .NET 8 Console 环境跑通，作为"双端门保留"的验证手段。
5. **Unity 限定模块不配 csproj**：`Unity/` 目录、Adapters/HybridCLR 等本身就依赖 UnityEngine，明确不参与服务端编译。
6. **Adapters/* 自由选择**：若 Adapter 实现本身跨端（例如未来 Adapters/CoreCLR），可配镜像 csproj；若依赖 Unity（Mirror Unity 集成层），则不配。

## Consequences

**好处：**
- "双端门"从 lint 升级为可验证的 build，CI 出错就立刻发现 Core 误依赖了 UnityEngine。
- 服务端阶段（V2.0）落地成本大幅下降——目录结构、命名空间、引用关系都已就位。
- 与 ET / Fantasy 等成熟框架的工程实践对齐，降低团队学习成本。
- V0.2 阶段就强制纪律，避免 V0.1→V2 迁移结束后再回头大改目录。

**成本：**
- 构建系统复杂度上升：每次新增跨端文件要确认 csproj `<Compile>` 通配能匹配到（多数情况靠通配自动覆盖，少数特例需手动维护）。
- IDE 体验略分裂：Unity 端走 Unity 生成的 sln，.NET 端走手写 csproj 的 sln，两套互不感知。
- Adapter 作者要明确判断"我跨端吗"，没判断好可能导致服务端编译断裂。
- CI 增加一条 dotnet build 流水线，机器时间成本小幅上升。