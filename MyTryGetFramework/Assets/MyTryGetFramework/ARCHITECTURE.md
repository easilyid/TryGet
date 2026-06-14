# MyTryGetFramework V2.0 — 纯客户端服务框架 + Procedure Stack

> V2.0 路线 C 重定向（ADR-0020）：基于 TEngine/BigCat/hsenl/Fantasy 四框架对比分析，
> 从"双端框架"转向"纯客户端服务框架"。
> ECS/IPlugin/服务端契约全部移除。Core 文件数从 86 降到 55（精简 36%，含 V2.2 FrameLoop 提前落地的多阶段接口）。

## 程序集布局

**当前 V2.0 Route C 权威布局**：Core 按领域拆分为 Assembly/Event/Module/Async/Time/Logging/Timer/Pool/Procedure/Data/Net 等目录，Unity 依赖只允许在 `Runtime/Unity` Adapter 层出现。

```
Assets/MyTryGetFramework/
├── Runtime/
│   ├── Core/                                     # 纯 C# 核心 (noEngineReferences: true)
│   │   ├── MyTryGetFramework.Core.asmdef
│   │   ├── Assembly/                             # 程序集级配置
│   │   │   └── AssemblyInfo.cs                   # InternalsVisibleTo Tests
│   │   ├── Event/                                # 事件系统
│   │   │   ├── EventModule.cs                    # IEventModule 默认实现（V2.1 零 GC 派发）
│   │   │   ├── IEventModule.cs / IEventScope.cs / EventModuleScopeExtensions.cs
│   │   │   └── EventHandlerAttribute.cs / EventHandlerRegistry.cs
│   │   ├── Module/                               # 框架骨架 / 启动 / 模块注册
│   │   │   ├── IModule.cs / IUpdateModule.cs / ILateUpdateModule.cs
│   │   │   ├── IEarlyUpdateModule.cs / IFixedUpdateModule.cs / IEndOfFrameModule.cs / FramePhase.cs  # V2.2 多阶段 Update
│   │   │   ├── IModuleSystem.cs / ModuleSystem.cs
│   │   │   ├── GameLauncher.cs / LauncherOptions.cs
│   │   │   ├── ModuleAttribute.cs / ModuleRegistry.cs
│   │   │   └── ModuleExceptions.cs
│   │   ├── Async/                                # 异步原语 + 取消（自研，零外部依赖）
│   │   │   ├── TGTask.cs / TGTaskBody.cs / TGTaskCompletionSource.cs
│   │   │   ├── TGTask.Abort.cs / TGTask.Combinators.cs            # 句柄式 Abort + WhenAll/WhenAny（ADR-0021）
│   │   │   ├── AsyncTGTaskMethodBuilder.cs / TGTaskPool.cs
│   │   │   ├── ITGTaskScheduler.cs / TGTaskScheduler.cs
│   │   │   ├── TGCancellation.cs / TGTaskAbortException.cs        # 取消 token/source/registration（ADR-0021）
│   │   │   ├── TGTaskExpiredException.cs / TGTaskType.cs
│   │   │   └── TimerModuleAsyncExtensions.cs
│   │   ├── Time/                                 # 时间源
│   │   │   └── IClock.cs (含 SystemClock)
│   │   ├── Logging/                              # 日志
│   │   │   └── ILogger.cs / ConsoleLogger.cs / LogLevel.cs
│   │   ├── Timer/                                # 定时器
│   │   │   └── ITimerModule.cs / TimerModule.cs / TimerHandle.cs
│   │   ├── Pool/                                 # 对象池
│   │   │   └── IPoolModule.cs / PoolModule.cs / IObjectPool.cs
│   │   ├── Procedure/                            # 流程栈
│   │   │   ├── IProcedure.cs / IAsyncProcedure.cs
│   │   │   └── IProcedureModule.cs / ProcedureModule.cs
│   │   ├── Data/                                 # 数据源契约 + 内存实现
│   │   │   ├── ISerializer.cs
│   │   │   ├── IKVStore.cs / MemoryKVStore.cs
│   │   │   ├── IConfigSource.cs / ConfigLoader.cs / MemoryConfigSource.cs
│   │   │   └── IAssetSource.cs / MemoryAssetSource.cs
│   │   └── Net/                                  # 客户端网络（仅契约）
│   │       ├── INetClient.cs (V2.0 简化版)
│   │       └── INetMessage.cs
│   └── Unity/                                    # Unity Adapter（MonoBehaviour 桥接层）
│       ├── MyTryGetFramework.Unity.asmdef
│       └── TryGetMonoEntry.cs                    # Unity 启动入口模板（abstract MonoBehaviour）
└── Tests/
    └── EditMode/                                 # 纯 C# 测试 (noEngineReferences: true)
        └── MyTryGetFramework.Tests.asmdef
```

## V2.0 核心骨架

```
ModuleSystem (框架根容器)
├── ILogger                — 日志
├── IClock                 — 时间（DeltaTime / ElapsedTime / FrameCount）
├── ITimerModule           — 定时器
├── IPoolModule            — 对象池
├── ITGTaskScheduler       — 异步调度
├── IEventModule           — 全局事件（泛型 struct，类型安全；V2.1 稳态 Publish 零 GC + 重入安全 + handler 异常隔离）
├── IProcedureModule       — 流程管理（Stack 模式 ★ V2.0 新增）
│   ├── Push / Pop / Replace / StackDepth
│   └── IProcedure: OnEnter / OnExit / OnPause / OnResume / OnUpdate
├── IConfigSource (+ ConfigLoader<T>)
├── IKVStore
├── IAssetSource
└── INetClient (业务层 Adapter 实现)

Source Generator:
├── [Module]               — 自动注册到 ModuleRegistry
└── [EventHandler]         — 自动订阅到 EventModule

GameLauncher:
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

## 双端门（ADR-0012 已落地）

```
ServerProject/
├── MyTryGetFramework.Core/
│   └── MyTryGetFramework.Core.csproj   # netstandard2.1，<Compile Include> 反向引用 Core/**/*.cs（不复制源码）
└── MyTryGetFramework.Tests/
    └── MyTryGetFramework.Tests.csproj  # net8.0 NUnit，反向引用 Tests/EditMode/*.cs，dotnet test 脱离 Unity 跑（ADR-0022）
```

**当前状态**：已落地手写影子 csproj。Core asmdef 设 `noEngineReferences: true`，在 Unity 端编译期物理禁止引用 UnityEngine；`ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj` 则在纯 .NET（netstandard2.1）下用 `<Compile Include>` 反向引用 `Runtime/Core/**/*.cs`（通配自动覆盖新增文件，不复制），并把 Source Generator 作为 Analyzer 引用。`dotnet build` 通过（0 警告 0 错误）即证明 Core 树跨端可编、无 UnityEngine 依赖——任何 `using UnityEngine` 渗入 Core 都会让此 csproj 编译失败，这就是"双端门"。

服务端业务代码（INetServer 等）按 ADR-0020 仍推迟到 V3.0+，本门当前仅用于验证 Core 的引擎无关性。

**测试影子（ADR-0022）**：`ServerProject/MyTryGetFramework.Tests/` 与 Core 影子对称——EditMode 测试 asmdef `noEngineReferences: true`、零 UnityEngine 依赖，故 `<Compile Include>` 反向引用后 `dotnet test` 可脱离 Unity 跑通全部 Core 测试（448 通过），补齐双端门只验「编译」、未验「测试运行」的缺口（形成「测试跨端门」）。

## 核心 ADR 索引

| ADR | 决策 | Status |
|-----|------|--------|
| 0001-0005 | V0.1 EC + Ownership + Aspect | **Superseded by ADR-0020**（V2.0 移除 ECS） |
| 0006 | SystemGroup 纯调度 | **Superseded by ADR-0020** |
| 0007 | Aspect 隔离纪律 | **Superseded by ADR-0020** |
| 0008 | V0.1 System 注册 | **Superseded by ADR-0020** |
| 0009 | Query All-of + None-of | **Superseded by ADR-0020** |
| 0010 | Entity-level + World-level Event | **Superseded by ADR-0020**（仅保留全局 IEventModule） |
| 0011 | ModuleSystem + IModule 契约 | V0.2 落地，V2.0 继续有效 |
| 0012 | Shadow csproj 双端编译 | **V2.0 落地**（Core noEngineReferences + 手写 netstandard2.1 csproj，`dotnet build` 0/0 通过） |
| 0013-0019 | 已被 ADR-0020 整体取代 | **Superseded by ADR-0020** |
| **0020** | **路线 C 重定向：纯客户端服务框架** | **V2.0 落地** |

## V2.0 ModuleSystem 使用模式

```csharp
var host = GameLauncher.CreateHost();

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
| V2.1 | 事件系统升级（稳态 Publish 零 GC + 派发中增删 next-publish-only + handler 异常隔离；Source Gen 事件接口后续继续）★ 代码实现完成；Shadow csproj 与生成测试 csproj build 通过；Unity EditMode 运行验证待办 |
| V2.2 | ModuleSystem 多阶段 Update（EarlyUpdate + FixedUpdate + EndOfFrame）★ 已随 V2.0 提前落地（ModuleSystem 5 阶段派发 + 执行表分桶；FramePhase 枚举待 C3 决定去留） |
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

详见 `.scratch/` 目录下的设计文档（V2.0 PRD 与路线重定向决策分析）。
