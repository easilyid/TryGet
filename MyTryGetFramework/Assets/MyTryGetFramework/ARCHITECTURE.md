# MyTryGetFramework V2.0 — 纯客户端服务框架 + Procedure Stack

> V2.0 路线 C 重定向（ADR-0020）：基于 TEngine/BigCat/hsenl/Fantasy 四框架对比分析，
> 从"双端框架"转向"纯客户端服务框架"。
> ECS/IPlugin/服务端契约全部移除。Core 文件数从 86 降到 55（精简 36%，含 V2.2 FrameLoop 提前落地的多阶段接口）。

## 程序集布局

```
Assets/MyTryGetFramework/
├── Runtime/
│   ├── Core/                                     # 纯 C# 核心 (noEngineReferences: true)
│   │   ├── MyTryGetFramework.Core.asmdef
│   │   ├── AssemblyInfo.cs                       # InternalsVisibleTo Tests
│   │   ├── EventBus.cs                           # IEventBus 默认实现
│   │   ├── Module/                               # 框架骨架
│   │   │   ├── IModule.cs / IUpdateModule.cs / ILateUpdateModule.cs
│   │   │   ├── IEarlyUpdateModule.cs / IFixedUpdateModule.cs / IEndOfFrameModule.cs / FramePhase.cs  # V2.2 多阶段 Update
│   │   │   ├── IModuleHost.cs / ModuleHost.cs
│   │   │   ├── IEventBus.cs / IEventScope.cs / EventBusScopeExtensions.cs
│   │   │   ├── Bootstrap.cs / BootstrapOptions.cs
│   │   │   ├── ModuleAttribute.cs / AssemblyManifestRegistry.cs
│   │   │   ├── EventHandlerAttribute.cs / EventHandlerRegistry.cs
│   │   │   └── ModuleExceptions.cs
│   │   ├── Async/                                # 异步原语（自研，零外部依赖）
│   │   │   ├── TGTask.cs / TGTaskBody.cs / TGTaskCompletionSource.cs
│   │   │   ├── AsyncTGTaskMethodBuilder.cs / TGTaskPool.cs
│   │   │   ├── ITGTaskScheduler.cs / TGTaskScheduler.cs
│   │   │   ├── TGTaskExpiredException.cs / TGTaskType.cs
│   │   │   └── TimerModuleAsyncExtensions.cs
│   │   ├── Common/                               # 基础服务
│   │   │   ├── IClock.cs (含 SystemClock)
│   │   │   ├── ILogger.cs / ConsoleLogger.cs / LogLevel.cs
│   │   │   ├── ITimerModule.cs / TimerModule.cs / TimerHandle.cs
│   │   │   ├── IPoolModule.cs / PoolModule.cs / IObjectPool.cs
│   │   │   ├── IProcedure.cs (+ ProcedureBase, OnPause/OnResume V2.0 新增)
│   │   │   ├── IAsyncProcedure.cs (+ AsyncProcedureBase)
│   │   │   ├── IProcedureModule.cs (+ Push/Pop/Replace/StackDepth V2.0 新增)
│   │   │   ├── ProcedureModule.cs (Stack 实现)
│   │   │   ├── ISerializer.cs
│   │   │   ├── IKVStore.cs / MemoryKVStore.cs
│   │   │   ├── IConfigSource.cs (+ ConfigNotFoundException)
│   │   │   ├── ConfigLoader.cs / MemoryConfigSource.cs
│   │   │   └── IAssetSource.cs / MemoryAssetSource.cs
│   │   └── Net/                                  # 客户端网络（仅契约）
│   │       ├── INetClient.cs (V2.0 简化版)
│   │       └── INetMessage.cs
│   └── Unity/                                    # Unity Adapter（V2.0 暂为空）
│       └── MyTryGetFramework.Unity.asmdef
└── Tests/
    └── EditMode/                                 # 纯 C# 测试 (noEngineReferences: true)
        └── MyTryGetFramework.Tests.asmdef
```

## V2.0 核心骨架

```
ModuleHost (框架根)
├── ILogger                — 日志
├── IClock                 — 时间（DeltaTime / ElapsedTime / FrameCount）
├── ITimerModule           — 定时器
├── IPoolModule            — 对象池
├── ITGTaskScheduler       — 异步调度
├── IEventBus              — 全局事件（泛型 struct，类型安全）
├── IProcedureModule       — 流程管理（Stack 模式 ★ V2.0 新增）
│   ├── Push / Pop / Replace / StackDepth
│   └── IProcedure: OnEnter / OnExit / OnPause / OnResume / OnUpdate
├── IConfigSource (+ ConfigLoader<T>)
├── IKVStore
├── IAssetSource
└── INetClient (业务层 Adapter 实现)

Source Generator:
├── [Module]               — 自动注册到 AssemblyManifestRegistry
└── [EventHandler]         — 自动订阅到 EventBus

Bootstrap:
└── CreateHost(options)    — 标准化启动 + 拓扑排序 OnInit
```

## Procedure Stack 设计（吸收 BigCat SceneMgr.stack）

```csharp
// 启动
proc.Start("MainMenu");      // 栈: [MainMenu]

// 替换栈顶
proc.Replace("Gameplay");    // 栈: [Gameplay]，MainMenu.OnExit + Gameplay.OnEnter

// 入栈（暂停下层，新栈顶进入）
proc.Push("PauseMenu");      // 栈: [Gameplay, PauseMenu]
                             // Gameplay.OnPause + PauseMenu.OnEnter

proc.Push("Settings");       // 栈: [Gameplay, PauseMenu, Settings]
                             // PauseMenu.OnPause + Settings.OnEnter

// 出栈（栈顶退出，恢复下层）
proc.Pop();                  // 栈: [Gameplay, PauseMenu]
                             // Settings.OnExit + PauseMenu.OnResume

proc.Pop();                  // 栈: [Gameplay]
                             // PauseMenu.OnExit + Gameplay.OnResume
```

| 操作 | 栈变化 | 生命周期 |
|------|--------|----------|
| `Start("A")` | [] → [A] | A.OnEnter |
| `Push("B")` | [A] → [A, B] | A.OnPause + B.OnEnter |
| `Pop()` | [A, B] → [A] | B.OnExit + A.OnResume |
| `Replace("C")` | [A, B] → [A, C] | B.OnExit + C.OnEnter |
| `Stop()` | [A, B, C] → [] | C/B/A.OnExit（逆序） |

## 双端门（V0.2 落地，V2.0 持续验证）

```
ServerProject/MyTryGetFramework.Core/
└── MyTryGetFramework.Core.csproj  # netstandard2.1，反向引用 Core/**/*.cs
```

`dotnet build` 持续验证 Core 不含 UnityEngine。Adapter 层（Runtime/Unity/）不参与跨端编译。

## 核心 ADR 索引

| ADR | 决策 | Status |
|-----|------|--------|
| 0001-0005 | V0.1 EC + Ownership + Aspect | **Superseded by ADR-0020**（V2.0 移除 ECS） |
| 0006 | SystemGroup 纯调度 | **Superseded by ADR-0020** |
| 0007 | Aspect 隔离纪律 | **Superseded by ADR-0020** |
| 0008 | V0.1 System 注册 | **Superseded by ADR-0020** |
| 0009 | Query All-of + None-of | **Superseded by ADR-0020** |
| 0010 | Entity-level + World-level Event | **Superseded by ADR-0020**（仅保留全局 IEventBus） |
| 0011 | ModuleHost + IModule 契约 | V0.2 落地，V2.0 继续有效 |
| 0012 | Shadow csproj 双端编译 | V0.2 落地，V2.0 继续有效 |
| 0013-0019 | 已被 ADR-0020 整体取代 | **Superseded by ADR-0020** |
| **0020** | **路线 C 重定向：纯客户端服务框架** | **V2.0 落地** |

## V2.0 ModuleHost 使用模式

```csharp
var host = Bootstrap.CreateHost(BootstrapOptions.Default);

// 业务 Module 注册
host.Register<ITimerModule>(new TimerModule());
host.Register<IProcedureModule>(new ProcedureModule());

// 业务 Procedure 注册
var proc = host.Get<IProcedureModule>();
proc.AddProcedure("MainMenu", new MainMenuProcedure());
proc.AddProcedure("Gameplay", new GameplayProcedure());
proc.AddProcedure("PauseMenu", new PauseMenuProcedure());

// 启动
host.Initialize();
proc.Start("MainMenu");

// 帧驱动（Unity 端由 MonoBehaviour 桥接）
host.Update(dt, unscaledDt);
host.LateUpdate(dt, unscaledDt);

// 关闭
host.Shutdown();
```

## V2.0 之后路线

| 版本 | 主题 |
|------|------|
| V2.1 | 事件系统升级（零 GC + Source Gen 事件接口） |
| V2.2 | ModuleHost 多阶段 Update（EarlyUpdate + FixedUpdate + EndOfFrame）★ 已随 V2.0 提前落地（ModuleHost 5 阶段派发 + 执行表分桶；FramePhase 枚举待 C3 决定去留） |
| V2.3 | UI 框架（IUIModule + UIWindow/UIWidget） |
| V2.4 | 资源管理（IAssetModule + YooAsset Adapter） |
| V2.5 | 场景管理（ISceneModule + Unity SceneManager） |
| V2.6 | 音频管理（IAudioModule） |
| V2.7 | 客户端网络 Adapter（TCP/KCP + 重连/心跳） |
| V2.8 | 热更新（HybridCLR 集成 + 程序集分离） |
| V3.0+ | 双端扩展（EC 模型 + INetServer + 跨端共享） |

## 设计参考

- TEngine（纯客户端 + 模块化 + 零 GC 事件）
- BigCat（Worker/Module 时序 + Scene Stack + 多阶段 Update）
- hsenl（IPlug 横切，V2.0 决定不引入）
- Fantasy（Source Generator 自动注册）

详见 `docs/design/V2.0-route-C-prd.md`（V2.0 PRD）和 `docs/adr/0020-route-c-pivot.md`（路线重定向决策）。
