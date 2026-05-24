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
| 0014 | Network/HotReload Adapter 抽象 | V0.6 计划（V0.5 推迟，Plan agent 建议与真实实现共生设计） |
| 0015 | Adapter 契约偏离白名单（注册前置 / 诊断字段语义 / 运行环境前置） | V0.5 关门后 V0.6 落地 |

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

## V0.5 之后路线图（V0.6+）

按 `.scratch/framework-design-v2/design.md` §12：
- **V0.6 Iter 0**：文档 catch-up（本次完成）+ 跨 Adapter 综合 PlayMode demo
- **V0.6 Iter 1+**：YooAsset Adapter（需先装 YooAsset 包，物理阻塞）
- **V0.6 后期**：Network 接口 + Mirror Adapter 共生设计；MemoryPack 序列化；HybridCLR IHotfixLoader
- **V1.0**：编辑器工具集 + 完整模板项目 + 文档 + CI / Unity batch 测试自动化
