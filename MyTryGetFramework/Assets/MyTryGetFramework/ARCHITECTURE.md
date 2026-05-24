# MyTryGetFramework V0.5 — 完整架构（Common Modules + Unity Adapter 套件）

> V0.2 落地 ModuleHost 基础设施 + Common 三件套，V0.3 拆 World 为 EntityWorld（IModule），
> V0.4 落地 Common Modules 五件套（Resource/UI/Save/Localization/Audio），
> V0.5 补齐 Core 服务（Input/Config/Scene）+ TimerModule/AudioModule 增强 + 5 个 Unity Adapter。
> 原 V0.1 内容见 git 历史。

## 程序集布局（V0.5）

```
Assets/MyTryGetFramework/
├── Runtime/
│   ├── Core/                                     # 纯 C# 核心 (noEngineReferences: true)
│   │   ├── MyTryGetFramework.Core.asmdef
│   │   ├── Module/                               # V0.2：框架根 + 服务定位 + 生命周期
│   │   │   ├── IModule.cs / IUpdateModule.cs / ILateUpdateModule.cs
│   │   │   ├── IEventBus.cs / IModuleHost.cs / ModuleHost.cs
│   │   │   └── ModuleExceptions.cs               # 类型化异常
│   │   ├── Common/                               # 跨端 Common Module
│   │   │   ├── ILogModule.cs + ConsoleLogModule.cs (V0.2)
│   │   │   ├── ITimerModule.cs + TimerModule.cs (V0.2 → V0.5 增强：ScheduleRepeat + Pause/Resume)
│   │   │   ├── IPoolModule.cs + PoolModule.cs + IObjectPool.cs (V0.2)
│   │   │   ├── BitArray256.cs + TypeIndex.cs (V0.3)
│   │   │   ├── IProcedure.cs + IProcedureModule.cs + ProcedureModule.cs (V0.3)
│   │   │   ├── IResourceModule.cs + MemoryResourceModule.cs (V0.4)
│   │   │   ├── IUIModule.cs + MemoryUIModule.cs (V0.4)
│   │   │   ├── ISaveModule.cs + MemorySaveModule.cs (V0.4)
│   │   │   ├── ILocalizationModule.cs + MemoryLocalizationModule.cs (V0.4)
│   │   │   ├── IAudioModule.cs + MemoryAudioModule.cs (V0.4 → V0.5 增强：Pause/Resume)
│   │   │   ├── IInputModule.cs + MemoryInputModule.cs (V0.5)
│   │   │   ├── IConfigModule.cs + MemoryConfigModule.cs (V0.5)
│   │   │   └── ISceneModule.cs + MemorySceneModule.cs (V0.5)
│   │   ├── Entity/                               # V0.3：玩法层根 + Entity 体系
│   │   │   ├── IEntityWorld.cs + EntityWorld.cs (Priority=-100)
│   │   │   ├── Entity.cs + EntityId.cs + Handle.cs
│   │   │   ├── Tag.cs + Phase.cs + Aspect.cs
│   │   ├── SystemBase.cs / SystemGroup.cs / Query.cs
│   │   ├── EntityEventDispatcher.cs + IEntityEventDispatcher.cs
│   │   └── IWorldEventBus.cs + WorldEventBus.cs  # World-scope vs Global-scope 并存（V0.5 撤销 deprecation）
│   └── Unity/                                    # Unity Adapter (noEngineReferences: false)
│       ├── MyTryGetFramework.Unity.asmdef        # references: Core, Unity.InputSystem, UnityEngine.UI
│       ├── WorldProxy.cs                         # MonoBehaviour 入口
│       ├── Audio/UnityAudioModule.cs             # V0.5 Iter 6：AudioSource 池
│       ├── Input/UnityInputModule.cs             # V0.5 Iter 7：接 InputSystem 1.18
│       ├── Save/PlayerPrefsSaveModule.cs         # V0.5 Iter 8：PlayerPrefs
│       ├── UI/UGUIUIModule.cs                    # V0.5 Iter 9：UGUI Canvas
│       └── Scene/UnitySceneModule.cs             # V0.5 Iter 10：SceneManager
└── Tests/
    ├── EditMode/                                 # 纯 C# 测试 (noEngineReferences: true, 200+ 测试)
    │   ├── MyTryGetFramework.Tests.asmdef
    │   ├── ModuleHost / EntityWorld / Aspect / Query / Pool / Timer / Log 系列
    │   ├── BitArray256Tests / QueryMaskStressTests / V03MustFix / V03FinalHardening
    │   ├── ProcedureModuleTests + LoginFlowDemoTests
    │   ├── MemoryResource/UI/Save/Localization/AudioModuleTests + MainMenuFlowDemoTests (V0.4)
    │   └── MemoryInput/Config/SceneModuleTests + TimerV05/AudioV05 + SceneFlowDemoTests (V0.5)
    └── PlayMode/                                 # V0.5 新建（Unity Adapter 测试基建）
        ├── MyTryGetFramework.Tests.PlayMode.asmdef  # references: Core, Unity, TestRunner, InputSystem, UI
        ├── UnityAudioModulePlayModeTests.cs        # 11
        ├── UnityInputModulePlayModeTests.cs        # 11
        ├── PlayerPrefsSaveModulePlayModeTests.cs   # 16
        ├── UGUIUIModulePlayModeTests.cs            # 11
        └── UnitySceneModulePlayModeTests.cs        # 13
```

## V0.5 Module Priority 完整链

```
Log(-1000) → Pool/Timer(-500) → Config(-460) → Save(-450) → Localization(-420) →
Resource(-400) → Audio(-380) → Input(-350) → UI(-300) → Scene(-250) →
Procedure(-200) → EntityWorld(-100) → 业务(0)
```

## 双端门（V0.2 落地 + 持续验证）

```
ServerProject/MyTryGetFramework.Core/
└── MyTryGetFramework.Core.csproj          # netstandard2.1，反向引用 Core/**/*.cs
```

`dotnet build` 持续验证 Core 不含 UnityEngine。Adapter 层（Runtime/Unity/）不参与跨端编译，
仅在 Unity Editor 通过 PlayMode 测试验证。

## 核心设计决策（ADR 索引）

| ADR | 决策 | Status |
|-----|------|--------|
| 0001-0005 | V0.1 EC + Ownership + Aspect | V0.5 继续有效 |
| 0006 | SystemGroup 纯调度 | Superseded by ADR-0011 |
| 0007 | Aspect 隔离纪律 | V0.5 继续有效 |
| 0008 | V0.1 System 注册 | Superseded by ADR-0011 |
| 0009 | Query All-of + None-of；Any-of 推迟 | V0.5 继续有效（BitArray256 加速） |
| 0010 | Entity-level + World-level Event | V0.5 撤销"合并到 IEventBus"计划（两个 EventBus 作用域分明） |
| 0011 | ModuleHost + IModule 契约 | V0.2 落地 |
| 0012 | Shadow csproj 双端编译 | V0.2 落地 + V0.5 持续验证 |
| 0013 | 保留 Aspect 隔离纪律到 V2 | V0.2 重申 |
| 0014 | Network/HotReload Adapter 抽象 | **Superseded by ADR-0016**（V1.1+ 真做时重写） |
| 0015 | Adapter 契约偏离白名单 | **Superseded by ADR-0016** |
| 0016 | **Adapter 层退出 Core 范围**（Audio/Input/UI/Scene 迁到 Samples/Unity/Adapters/） | V0.5.5 方向回切落地 |

## 依赖方向

```
Tests.PlayMode → Unity → Core
Tests.EditMode → Core
                  ↑ (Core 不依赖 Unity，Shadow csproj 验证)
```

## V0.5 ModuleHost 使用模式（Unity 完整版）

```csharp
var host = new ModuleHost();

// Core Memory 实现（适合测试 / Headless）
host.Register<ILogModule>(new ConsoleLogModule());
host.Register<ITimerModule>(new TimerModule());
host.Register<IPoolModule>(new PoolModule());
host.Register<IConfigModule>(new MemoryConfigModule());
host.Register<ILocalizationModule>(new MemoryLocalizationModule());
host.Register<IResourceModule>(new MemoryResourceModule());
host.Register<IProcedureModule>(new ProcedureModule());
host.Register<IEntityWorld>(new EntityWorld("Game"));

// Unity Adapter（生产环境）
host.Register<IAudioModule>(new UnityAudioModule(poolSize: 16));
host.Register<IInputModule>(new UnityInputModule());
host.Register<ISaveModule>(new PlayerPrefsSaveModule());
host.Register<IUIModule>(new UGUIUIModule());
host.Register<ISceneModule>(new UnitySceneModule());

host.Initialize();             // 按 Priority 拓扑序 OnInit
host.Update(dt, unscaledDt);   // IUpdateModule 调度
host.LateUpdate(dt, ud);       // ILateUpdateModule 调度（如 UnityInputModule 在此清 edge）
host.Shutdown();               // 逆序
```

## 运行测试

- **EditMode**（200+ 测试，全跨端）：Unity Editor → Window → General → Test Runner → EditMode
- **PlayMode**（62 Unity Adapter 测试）：Test Runner → PlayMode
- **跨端编译**：`cd ServerProject/MyTryGetFramework.Core && dotnet build`

## V0.5 之后路线图（V0.6+）— 战略方向回切后重定义

> 2026/05/24 起依 `docs/strategy/V2-direction-pivot.md` 重定向。
> 用户明确要求"商业框架基础架构"对标 ET/Fantasy/TEngine/Hsenl，
> Unity Adapter/Network/热更归入业务扩展层。

### V0.5.5（进行中）— Adapter 退场
- Audio/Input/UI/Scene 整套（接口 + Memory + Adapter + 测试）迁到
  `Samples/Unity/Adapters/`，保留代码价值作为业务参考样例
- PlayerPrefsSaveModule Adapter 迁出，但 ISaveModule Core 接口保留（V0.8 重构为 IKeyValueStore）
- 见 ADR-0016

### V0.6 — ITask 异步原语（最致命缺口，自研 + 对标）
- **目标**：Core 获得跨端 async 能力，参考 ETTask / Fantasy FTask / Hsenl Task
  设计自研 ITask（用户决策：自研路线，不用 ValueTask 包装）
- 文件：`Core/Async/ITask.cs`、`ITaskCompletionSource.cs`、`TaskPool.cs` 等
- 先出 PRD `docs/design/V0.6-ITask.md`，对标 3 家 Task 实现的设计取舍

### V0.7 — 双端入口规范 + ILogger + IClock
- 对标 Fantasy `Platform.Unity.Entry` vs `Platform.Net.Entry`
- `ILogModule` → `ILogger`；新增 `IClock`（解耦 Unity Time）
- `Samples/Net/Program.cs`（dotnet console 启动）+ `Samples/Unity/TryGetMonoEntry.cs`

### V0.8 — 配置 + 序列化重构
- `IResourceModule` → `IAssetSource + ISerializer`
- `ISaveModule` → `IKeyValueStore`
- `IConfigModule` → `IConfigSource + ConfigLoader<T>`（类型化配置表）
- `ILocalizationModule` → 评估降级到 `Optional/`

### V0.9 — Source Generator 注册
- 对标 Fantasy SourceGenerator + `[ModuleInitializer]` 自动注册
- 消除手动 `host.Register<>()` 调用

### V1.0 — 真双端样例 + 文档冻结
- `Samples/Net/MmoServerDemo` + `Samples/Unity/MmoClientDemo` 共享 Aspect/Entity 代码
- 对标 ET All-in-One 轻量版

### V1.1+ — 业务扩展层（独立仓库或 Samples/）
- Network 抽象（LiteNetLib/KCP/WebSocket）
- HybridCLR Hotfix Loader
- YooAsset Adapter
- Luban 集成
