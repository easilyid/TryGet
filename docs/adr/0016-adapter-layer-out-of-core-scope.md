# Adapter 层退出 Core 范围（Adapter Layer Out of Core Scope）

## Status

Accepted

## Context

V0.4-V0.6 期间 TryGet 大量产出聚集在 Unity Adapter（Audio/Input/Save/UI/Scene 5 件套）+ PlayMode 测试基建 + Adapter 综合 demo + Adapter 契约 ADR（ADR-0014/0015）。

用户 2026/05/24 明确指出方向偏离：

> "我暂时其实不需要你去给我扩展什么 Network YooAsset 热更新等，我需要的是商业框架的基础架构，其余 Unity 内的内容其实都算是业务内容，这些可以后续扩展的不是么？先重新理清，是按 TEngine, hsenl, et, fantasy 等的架构路线。"

战略文档 `docs/strategy/V2-direction-pivot.md` 做了 4 框架对标研究：
- **ET / Fantasy / Hsenl 没有把 UI/Audio/Input/Scene 当 Core**——这些是业务/扩展层。
- **TEngine 把 Unity 写进了 Core**——服务端复用能力为 0，**正是用户要避免的反模式**。
- TryGet 当前的 ModuleHost / EntityWorld / Aspect / Event / Procedure 与 ET/Fantasy"基础架构"边界**已经一致**，但 V0.4-V0.5 加的 5 个 Unity Adapter 把 Core 边界拉错了。

## Decision

**把以下内容整体迁出 Core，重新放置到 `Assets/MyTryGetFramework/Samples/Unity/Adapters/`**，作为"业务扩展层参考样例"保留代码价值：

### 完整迁移清单

#### A. Unity Adapter 实现（5 件套，原 Runtime/Unity/{Audio,Input,UI,Scene,Save}/）
| 源 | 目标 |
|---|---|
| `Runtime/Unity/Audio/UnityAudioModule.cs(.meta)` | `Samples/Unity/Adapters/Audio/` |
| `Runtime/Unity/Input/UnityInputModule.cs(.meta)` | `Samples/Unity/Adapters/Input/` |
| `Runtime/Unity/UI/UGUIUIModule.cs(.meta)` | `Samples/Unity/Adapters/UI/` |
| `Runtime/Unity/Scene/UnitySceneModule.cs(.meta)` | `Samples/Unity/Adapters/Scene/` |
| `Runtime/Unity/Save/PlayerPrefsSaveModule.cs(.meta)` | `Samples/Unity/Adapters/Save/` |

#### B. Core 业务化 Module 接口（迁出 Core/Common/）
| 源 | 目标 | 理由 |
|---|---|---|
| `Common/IAudioModule.cs + MemoryAudioModule.cs` | `Samples/Unity/Adapters/Audio/` | Audio 是业务 |
| `Common/IInputModule.cs + MemoryInputModule.cs` | `Samples/Unity/Adapters/Input/` | Input 是业务 |
| `Common/IUIModule.cs + MemoryUIModule.cs` | `Samples/Unity/Adapters/UI/` | UI 是业务 |
| `Common/ISceneModule.cs + MemorySceneModule.cs` | `Samples/Unity/Adapters/Scene/` | Scene 是 Unity 业务 |

#### C. PlayMode 测试 + asmdef（整体迁）
| 源 | 目标 |
|---|---|
| `Tests/PlayMode/UnityAudioModulePlayModeTests.cs` | `Samples/Unity/Adapters/Tests.PlayMode/` |
| `Tests/PlayMode/UnityInputModulePlayModeTests.cs` | 同上 |
| `Tests/PlayMode/UGUIUIModulePlayModeTests.cs` | 同上 |
| `Tests/PlayMode/UnitySceneModulePlayModeTests.cs` | 同上 |
| `Tests/PlayMode/PlayerPrefsSaveModulePlayModeTests.cs` | 同上 |
| `Tests/PlayMode/UnityFlowDemoPlayModeTests.cs` | 同上 |
| `Tests/PlayMode/MyTryGetFramework.Tests.PlayMode.asmdef` | 改名 `MyTryGetFramework.Samples.UnityAdapters.Tests.PlayMode.asmdef`，迁过去 |

#### D. EditMode 测试（业务化 Module 的 Memory 测试，跟着迁）
| 源 | 目标 |
|---|---|
| `Tests/EditMode/MemoryAudioModuleTests.cs` | `Samples/Unity/Adapters/Tests.EditMode/` |
| `Tests/EditMode/MemoryInputModuleTests.cs` | 同上 |
| `Tests/EditMode/MemoryUIModuleTests.cs` | 同上 |
| `Tests/EditMode/MemorySceneModuleTests.cs` | 同上 |
| `Tests/EditMode/AudioModuleV05Tests.cs`（增强测试） | 同上 |
| `Tests/EditMode/SceneFlowDemoTests.cs` | 同上 |
| `Tests/EditMode/MainMenuFlowDemoTests.cs` | 同上 |

#### E. asmdef 调整
- 新建 `Samples/Unity/Adapters/MyTryGetFramework.Samples.UnityAdapters.asmdef`
  - references: `MyTryGetFramework.Core`, `Unity.InputSystem`, `UnityEngine.UI`
  - noEngineReferences: false
- 新建 `Samples/Unity/Adapters/Tests.PlayMode/MyTryGetFramework.Samples.UnityAdapters.Tests.PlayMode.asmdef`
  - references: Core + Samples.UnityAdapters + TestRunner + InputSystem + InputSystem.TestFramework + UnityEngine.UI
- 新建 `Samples/Unity/Adapters/Tests.EditMode/MyTryGetFramework.Samples.UnityAdapters.Tests.EditMode.asmdef`
  - references: Core + Samples.UnityAdapters + TestRunner
- 修改 `Runtime/Unity/MyTryGetFramework.Unity.asmdef` references **去掉** Unity.InputSystem + UnityEngine.UI（不再需要）

### 保留在 Core 不迁移

**Core/Common/ 保留**（V0.8 重构方向已规划但不在本 ADR）：
- `ILogModule + ConsoleLogModule`（V0.7 改名 `ILogger + ConsoleLogger`）
- `ITimerModule + TimerModule`
- `IPoolModule + PoolModule + IObjectPool`
- `IProcedure + IProcedureModule + ProcedureModule`
- `IResourceModule + MemoryResourceModule`（V0.8 重构为 `IAssetSource + ISerializer`）
- `ISaveModule + MemorySaveModule`（V0.8 重构为 `IKeyValueStore`，PlayerPrefsSaveModule 已迁 Samples/）
- `ILocalizationModule + MemoryLocalizationModule`（V0.8 评估降级 Optional/）
- `IConfigModule + MemoryConfigModule`（V0.8 重构为 `IConfigSource + ConfigLoader<T>`）
- `BitArray256 + TypeIndex`

**Core/Entity/ 保留**（无变更）：
- `IEntityWorld + EntityWorld`
- `Entity + EntityId + Handle + Aspect + Tag + Phase`
- `SystemBase + SystemGroup + Query`
- `EntityEventDispatcher + IEntityEventDispatcher`
- `IWorldEventBus + WorldEventBus`（V0.5 已撤销 deprecation）

**Runtime/Unity/ 保留**（极简）：
- `WorldProxy.cs`（V0.3 MonoBehaviour 入口，仍是 Unity 端唯一示例）

### 关联 ADR Superseded

- **ADR-0014**（Network/HotReload Adapter 抽象）：Status 改为 **Superseded by ADR-0016**。Network/HotReload 在 V1.1+ 真做时重写。
- **ADR-0015**（Adapter 契约偏离白名单）：Status 改为 **Superseded by ADR-0016**。Adapter 退场后此 ADR 没有承载对象。

## Consequences

### 好处
- **Core 重新对齐 ET/Fantasy 的"基础架构"边界**：Core 跑在 dotnet console 也能完整工作
- **代码价值保留**：5 个 Adapter + 62 PlayMode 测试不丢，作为业务参考样例
- **明确边界后下一步路线清晰**：V0.6 ITask 异步原语 / V0.7 双端入口 / V0.8 配置序列化重构 / V0.9 Source Generator / V1.0 真双端样例
- **TEngine 反模式被避开**：避免 Unity API 进 Core

### 风险
- **git mv 操作量大**（~30 个文件 + 8 个 .meta 文件夹 + 3 个 asmdef）：必须保 git 历史可追溯
- **Unity .meta GUID 关联**：必须 git mv 而非 rm + add，否则丢失 Asset 引用
- **Samples/ 在 Unity 项目里的特殊性**：要确保 asmdef 名不冲突、references 不循环
- **缓解**：分多 commit 做（每个 Module 一 commit），每步 dotnet build 验证

### 不允许的退路
- ✗ 把 Adapter 留在 Core 但加注释"以后会迁出"：拖延决策只会让边界继续模糊
- ✗ 物理删除而不保留代码：62 PlayMode 测试 + 5 Adapter 设计投资沉没成本太大
- ✗ 拆独立仓库：当前阶段管理双仓库同步成本高于收益

## 实施时序

按 commit 粒度拆：
1. **本 commit**（ADR-0016 + ARCHITECTURE.md 更新）：只动文档
2. **后续 commit**（Adapter 退场执行）：按 Module 粒度做 git mv（Audio → Input → UI → Scene → Save → PlayMode tests + asmdef 调整）
3. **后续 commit**（V0.6 ITask PRD）：写 `docs/design/V0.6-ITask.md`，对标 ETTask/FTask/Hsenl Task 设计自研路线

## 关联

- `docs/strategy/V2-direction-pivot.md`：完整战略文档（4 框架对标 + 5 维度审视 + 路线图）
- ADR-0011 ModuleHost + IModule 契约：未受本 ADR 影响
- ADR-0012 Shadow csproj 双端编译：未受影响
- ADR-0014 Network/HotReload Adapter：Superseded by 本 ADR
- ADR-0015 Adapter 契约偏离白名单：Superseded by 本 ADR
