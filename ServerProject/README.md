# ServerProject — Shadow csproj 双端编译验证

按 ADR-0012「Shadow csproj 双端编译模式」落地。

## 用途

让 `MyTryGetFramework.Core` asmdef 的源码在 **.NET（非 Unity）环境**也能编过，验证
`noEngineReferences=true` 的 asmdef 是真正跨端可用的，而不只是 Unity 编译期的 lint。

源码物理上只有一份（在 `MyTryGetFramework/Assets/.../Runtime/Core/` 下），csproj 通过
相对路径 `<Compile Include="..\..\MyTryGetFramework\Assets\...\*.cs" />` 反向引用，
**不复制**。

## 当前覆盖

| Shadow csproj                | 对应 asmdef                        | 状态     |
| ---------------------------- | ---------------------------------- | -------- |
| `MyTryGetFramework.Core/`    | `MyTryGetFramework.Core.asmdef`    | ✅ 编过 |
| `MyTryGetFramework.Network/` | （V0.5 落地后追加）                | 未规划   |

## 怎么编

```bash
cd ServerProject/MyTryGetFramework.Core
dotnet build
```

需要 .NET SDK 8+（推荐 10）。netstandard2.1 兼容 Unity 2021+ 与 .NET 8+。

## 双端门契约

Core 树中任何 `using UnityEngine` 或 `using Unity.*` 都会让此 csproj 编译失败——
**这就是 V0.2 双端门**。CI 跑 `dotnet build` 即可保证 Core 不被偷偷渗入引擎依赖。

## 关联文档

- ADR-0011：ModuleHost + IModule 契约
- ADR-0012：Shadow csproj 双端编译模式（本目录的源策略）
- `.scratch/framework-design-v2/design.md` §4：目录布局
