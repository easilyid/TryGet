# MyTryGetFramework 当前代码质量与参考框架差距分析

日期：2026-06-14

范围：以当前工作树为准，审阅 `MyTryGetFramework/Assets/MyTryGetFramework`、`ServerProject`、`Samples/Net`、`docs` 和 `ReferenceFramework` 中的代表性实现。参考框架抽样覆盖 TEngine、BigCat、hsenl、AlicizaX、DGame、MyFramework。

## 1. 总体判断

MyTryGetFramework 当前不是空壳，已经具备一个方向清晰、可纯 .NET 验证的 Core：模块生命周期、五阶段调度、类型安全事件、TGTask、Timer、Pool、Procedure Stack、数据源契约和 Source Generator 都有实现与测试。它的代码质量在“轻量 Core 框架骨架”层面已经高于普通学习项目：依赖方向可验证，错误路径有类型化异常，核心模块普遍有单测。

但它距离参考框架里的“可落地商业 Unity 客户端框架”还有明显距离。核心问题不是 Core 写得太少，而是上层客户端能力基本还没有产品化：`Runtime/Unity` 目前没有 C# Adapter，UI/资源/场景/音频/网络/热更都停留在契约或路线图，`TryGetMonoEntry` 还在 Samples 下。换句话说，TryGet 当前强在“干净骨架”，弱在“完整客户端服务面”。

当前质量评级：

| 维度 | 评级 | 结论 |
|---|---:|---|
| Core 依赖方向 | A- | Core asmdef `noEngineReferences: true`，Shadow csproj 可编译 |
| 生命周期与模块调度 | B+ | 拓扑排序、回滚、五阶段分桶已落地；缺作用域和运行时替换能力 |
| 事件系统 | B+ | struct 事件、重入延迟增删、异常隔离已落地；缺 owner/scope 深度策略 |
| TGTask / async | B | 设计有深度，但复杂、缺公开 CancellationToken/Timeout/WhenAll 等成熟 API |
| Procedure Stack | B+ | 可 await 的 transition 是亮点；仍偏启动/状态流程，未联动 UI/Scene |
| Unity Adapter | D | `Runtime/Unity` 没有实际 C# 文件，入口仅 Sample 化 |
| UI/资源/场景等客户端服务 | D | 仅契约或内存实现，没有 Unity 真实 Adapter |
| 测试与验证 | B | .NET 侧通过；Unity EditMode 当前无法重跑，最近结果有 1 个失败 |

## 2. 当前规模与验证证据

### 2.1 代码规模

当前包内 C# 规模：

| 区域 | 文件数 | 行数 |
|---|---:|---:|
| 全部 C# | 99 | 12987 |
| Runtime/Core | 59 | 4658 |
| Tests/EditMode | 32 | 7305 |
| Source Generator 源码 | 6 | 847 |
| Runtime/Unity | 0 | 0 |
| Samples/Unity | 2 | 未单独计入核心 |

Core 内部最大复杂度集中在：

| Core 区域 | 文件数 | 行数 |
|---|---:|---:|
| Async | 11 | 1372 |
| Module | 15 | 919 |
| Event | 7 | 538 |
| Procedure | 4 | 546 |
| Data | 8 | 490 |
| Timer | 3 | 327 |

### 2.2 已跑验证

已在当前机器跑通：

```text
dotnet build ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj
结果：成功，0 warning，0 error

dotnet test MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.Tests/MyTryGetFramework.SourceGenerator.Tests.csproj
结果：14 passed，0 failed

dotnet run --project Samples/Net/TryGet.Samples.Net.csproj
结果：跑通 [Module] / [EventHandler] 自动注册、TickEvent handler、Boot -> Login -> InGame 异步 Procedure 流程
```

Unity EditMode：

- 本次尝试 batchmode 重跑被阻塞：当前项目已被另一个 Unity 实例打开。
- 最新可读结果 `MyTryGetFramework/TestLogResults/TestResults_20260614_123726.xml` 显示 412 total / 411 passed / 1 failed。
- 失败用例：`RegistryDiagnosticsTests.ModuleRegistry_Snapshot_MetadataPlusLegacyDelegate_ReportsBoth`，期望 3，实际 2，位置 `Tests/EditMode/RegistryDiagnosticsTests.cs:125`。
- 注意：当前工作树里 `ModuleRegistry.cs` 已有相关修正痕迹，但 Unity 测试未能在当前状态下重跑，因此该项应标记为“Unity 侧未重新验证”，不能宣称全绿。

## 3. 做得好的地方

### 3.1 依赖方向可验证

Core asmdef 明确无 Unity 引擎引用：`Runtime/Core/MyTryGetFramework.Core.asmdef` 的 `noEngineReferences: true`。Shadow csproj 反向包含 `Runtime/Core/**/*.cs`，并引用 Source Generator 作为 Analyzer，`dotnet build` 通过说明 Core 现阶段确实能离开 Unity 编译。

这点比 TEngine/DGame 常见的 Unity 强绑定 ModuleSystem 更干净。TEngine 的模块系统是全局静态，运行时模块直接处在 Unity 工程内；DGame 的 ModuleSystem 也是类似的静态服务定位器。TryGet 当前用实例化 `ModuleSystem` + Shadow csproj，依赖方向更容易测试和复用。

### 3.2 ModuleSystem 比参考框架更显式

`ModuleSystem` 要求 `Register<T>` 使用服务接口，禁止直接用 `IModule`、各类 Update 基接口、`IEventModule` 等框架基接口注册。初始化时做 DependsOn 拓扑排序，`OnInit` 中途失败会倒序 Shutdown 已初始化模块并回滚。五阶段 Update 则缓存执行表，避免每帧反射或 `OfType`。

证据：

- `ModuleSystem.Register<T>`：`MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Module/ModuleSystem.cs:51`
- `Initialize` 回滚：`ModuleSystem.cs:107`
- 五阶段接口：`ModuleSystem.cs:168`, `183`, `228`
- 拓扑排序：`ModuleSystem.cs:286`

TEngine/DGame 的模块系统更成熟在业务面，但创建依赖接口命名约定和静态全局状态。TryGet 的显式注册更适合做可测试框架核心。

### 3.3 EventModule 已越过“玩具事件总线”

当前事件系统不是简单 `Dictionary<Type, Delegate>`。它具备：

- struct 泛型事件，无弱类型 int/string id。
- `MaxPublishDepth = 32` 防止递归事件炸栈。
- 派发期间订阅/取消订阅写入 pending changes，下次发布生效。
- handler 异常隔离，且有 `HandlerException` 钩子。

证据：

- `EventModule.Publish<T>`：`Runtime/Core/Event/EventModule.cs:21`
- 深度上限：`EventModule.cs:13`
- `HandlerList<T>`：`EventModule.cs:119`
- 延迟增删：`EventModule.cs:161`, `198`
- 异常隔离：`EventModule.cs:149`

这吸收了 AlicizaX/DGame 的 pending-change 思路，同时补上了异常隔离。差距是生命周期 owner 还很轻，UI/Procedure/Scene 销毁时的批量解绑策略还没形成框架规范。

### 3.4 TGTask 有真实设计深度

TGTask 不是对 `Task` 的简单封装，而是完整自研 async 原语：

- `AsyncMethodBuilder` 接入 C# async。
- struct task + pooled body。
- version 防止旧句柄 await 已回收 body。
- `Forget()` 接全局 `UnobservedException`。
- Scheduler 支持 Yield / Delay / WaitForFrames 和 5 个 FramePhase。

证据：

- `TGTask` builder：`Runtime/Core/Async/TGTask.cs:32`
- `Forget`：`TGTask.cs:70`
- `GetResult` 回池：`TGTask.cs:140`
- `TGTaskScheduler`：`Runtime/Core/Async/TGTaskScheduler.cs:17`
- phase-aware 调度：`TGTaskScheduler.cs:248`

这部分已经接近参考框架 async 核心层的复杂度。但成熟度还不够：缺 `CancellationToken`、Timeout、WhenAll/WhenAny、进度/取消传播策略，Unity 对外使用时也缺产品级 API 文档。

### 3.5 Procedure Stack 的 transition 设计有亮点

参考框架里 TEngine/FSM 与 BigCat Scene Stack 多数是同步切换语义。TryGet 的 `Start/Push/Pop/Replace` 返回 `TGTask`，让调用者可以 await transition 完成，这是比简单 void API 更深的接口。

证据：

- `Start`：`Runtime/Core/Procedure/ProcedureModule.cs:73`
- `Push`：`ProcedureModule.cs:81`
- `Pop`：`ProcedureModule.cs:105`
- `Replace`：`ProcedureModule.cs:130`
- `RunTransition`：`ProcedureModule.cs:227`
- 主动取消 active transition：`ProcedureModule.cs:267`

当前差距是 Procedure 还没有和 UI/Scene/Resource Adapter 形成一个完整启动链，因此它只是“状态栈能力”，不是 TEngine/DGame 那种“启动/下载/资源/热更/进游戏”的完整流程。

### 3.6 Source Generator 有诊断意识

生成器使用 `IIncrementalGenerator`，合法输入生成 Module/EventHandler manifest，非法输入报告 TG0001-TG0006 诊断，而不是静默跳过。输出代码使用 `ModuleInitializer`，Unity 下再加 `RuntimeInitializeOnLoadMethod`。

证据：

- `ModuleManifestGenerator` 报诊断：`Generators~/.../ModuleManifestGenerator.cs:48`
- `ModuleDoesNotImplementService`：`Generators~/.../ModuleManifestGenerator.cs:85`
- Unity + ModuleInitializer 双触发：`ModuleManifestGenerator.cs:117`
- `EventHandlerGenerator` 签名诊断：`EventHandlerGenerator.cs:60`, `64`
- 诊断定义：`Generators~/.../GeneratorDiagnostics.cs:31`, `43`, `49`

这比“运行时扫描程序集”更适合 IL2CPP 和可测试性。

## 4. 当前问题与风险

### P1. TimerModule 周期 timer 自取消/自暂停会被覆盖

`TimerModule.Update` 中 repeating timer 到期后把 `_entries[i]` 读到局部 `entry`，执行 callback，然后用局部 `entry` 回写 `_entries[i] = entry`。如果 callback 内对自己的 handle 调 `Cancel` 或 `Pause`，`Cancel/Pause` 会先改 `_entries[i]`，但随后被局部 `entry` 覆盖，导致自取消/自暂停失效。

证据：

- `Cancel` 回写：`Runtime/Core/Timer/TimerModule.cs:86`, `96`
- `Pause` 回写：`TimerModule.cs:103`, `113`
- repeating callback 后回写：`TimerModule.cs:179`, `195`, `217`

影响：周期 timer 业务里很常见“触发到某条件后取消自己”。当前实现会让这类业务继续触发，属于真实行为 bug。

建议：repeating callback 后重新读取当前 slot，或在 Entry 内引入 generation/version，或者把 callback 触发与状态提交分成队列化命令，确保 callback 内状态修改不被旧快照覆盖。

### P1. Unity EditMode 当前不能证明全绿

已有 Unity 结果显示 412/411/1，失败点在 ModuleRegistry metadata + legacy 快照。当前代码看起来已经在 `ModuleRegistry` 加了 `_claimedMetadataCount/_legacyRegistrationCount` 修正，但由于 Unity 项目被打开，无法重跑 batchmode 验证。

证据：

- 最新保存结果：`MyTryGetFramework/TestLogResults/TestResults_20260614_123726.xml`
- 失败用例：`Tests/EditMode/RegistryDiagnosticsTests.cs:125`
- 当前修正逻辑：`Runtime/Core/Module/ModuleRegistry.cs:24`, `36`, `62`, `79`

建议：关闭当前 Unity Editor 或用干净 CI 环境重跑 EditMode。未重跑前，报告不能写“Unity 测试全绿”。

### P1. Runtime/Unity 还是空 Adapter

`Runtime/Unity` 只有 asmdef，没有 C# 文件。真正可用的 `TryGetMonoEntry` 在 `Samples/Unity/Entry`，这说明当前 Unity 集成仍是样例，不是框架公开 Adapter。

证据：

- `Runtime/Unity` C# 文件数：0。
- `TryGetMonoEntry` 在 Samples：`Samples/Unity/Entry/TryGetMonoEntry.cs:50`
- `Awake` 创建 host：`TryGetMonoEntry.cs:64`
- EndOfFrame coroutine：`TryGetMonoEntry.cs:96`

影响：作为 UPM 包安装后，用户会疑惑“应该引用 Samples 还是 Runtime/Unity”。这会弱化框架边界。

建议：把最小 Unity host 移入 `Runtime/Unity`，Samples 只保留演示子类。提供 `TryGetUnityHost`/`TryGetMonoEntry` 作为正式 Adapter，并明确是否 auto-referenced。

### P1. Data/Asset/Net 仍偏契约或内存实现

Core 有 `IAssetSource`、`IConfigSource`、`IKVStore`、`INetClient`，但真实 Unity 资源、YooAsset/Addressables、配置生成、网络连接都未落地。相比 TEngine/DGame/hsenl，TryGet 还缺真正的客户端服务闭环。

参考对比：

- TEngine `ProcedureLoadAssembly` 通过资源模块加载热更 DLL，再反射调用 `GameApp.Entrance`：`ReferenceFramework/TEngine/.../ProcedureLoadAssembly.cs:50`, `92`, `142`
- hsenl UI 直接基于 YooAsset 加载 UI：`ReferenceFramework/hsenl/.../UIManager.cs:197`, `201`
- hsenl 网络有纯 C# `TcpClient`，含 socket、Channel、KeepAlive、Update 驱动：`ReferenceFramework/hsenl/.../TcpClient.cs:41`, `82`, `177`
- DGame 有 GameObjectPool async spawn/recycle 和取消令牌：`ReferenceFramework/DGame/.../GameObjectPoolModule.cs:50`, `69`, `93`, `135`

建议：V2.3/V2.4 优先把 UI + Resource Adapter 做成最小可运行闭环，否则 Core 会越来越“正确但不可用”。

### P2. TGTask 缺成熟 async API 与取消模型

当前 `TGTaskScheduler` Shutdown 会取消内部 pending tcs，但对用户侧没有通用 `CancellationToken`、Timeout、WhenAll/WhenAny、owner-scope 取消。`Forget()` 需要用户显式调用，否则忽略返回值的 TGTask 没有强约束。

证据：

- `UnobservedException` 只接 `Forget` 路径：`TGTaskScheduler.cs:50`, `TGTask.cs:70`
- Scheduler API 只覆盖 Yield/Delay/WaitForFrames：`TGTaskScheduler.cs:147`, `160`, `194`

建议：先补 owner-scope cancellation 和 Timeout，再补 WhenAll/WhenAny。不要直接引入多线程模型，保持单线程主循环边界。

### P2. PoolModule DEBUG 重复归还检测应使用引用相等

DEBUG 下 `_activeSet` 用 `HashSet<T>` 默认 equality。如果池对象重写 `Equals/GetHashCode` 为值相等，不同对象可能被误认为同一个 active 对象，导致双释放检测误报或漏报。

证据：

- `_activeSet`：`Runtime/Core/Pool/PoolModule.cs:59`
- Rent 加入：`PoolModule.cs:94`
- Return 移除：`PoolModule.cs:100`, `106`

建议：改为 reference equality comparer，或引入类似 AlicizaX generation-token 的句柄式归还校验。

### P2. 注册表快照逻辑仍是全局静态状态

`ModuleRegistry` / `EventHandlerRegistry` 解决了 Source Generator 自动注册，但它们是全局静态收集器，多 host、多热更程序集、测试隔离都依赖 `ClearForTests` 和应用时机。文档中也承认“后加载 assembly 注册不会回填到已构造 host”。

证据：

- `ModuleRegistry.ApplyAll`：`Runtime/Core/Module/ModuleRegistry.cs:62`
- `EventHandlerRegistry.ApplyAll`：`Runtime/Core/Event/EventHandlerRegistry.cs:54`

建议：V2.8 热更前必须定义“后加载程序集如何应用到现有 host”的显式 API，否则热更层生成注册会有时序坑。

## 5. 与参考框架的主要差距

### 5.1 对 TEngine / DGame：缺完整客户端业务服务层

TEngine 和 DGame 的优势不是 ModuleSystem 本身，而是围绕 Unity 客户端已经打通：

- GameEntry/Procedure 启动链。
- ResourceModule + YooAsset。
- UI 模块、窗口栈、层级深度、错误日志。
- HybridCLR / 热更 DLL 加载。
- 配置、Luban、生成器、调试器。

TryGet 已吸收的是“模块优先级、执行表、Procedure、Timer catch-up 思路”，但还没有实现真正的 UI/Resource/Scene/Audio/Hotfix。当前差距最大就在这里。

结论：不要再扩 Core 抽象，下一步应该做 Unity 端最小 UI + Resource Adapter 闭环。

### 5.2 对 BigCat：缺 Scene/Worker 级运行容器，但这是有意识取舍

BigCat 的强项是 Node/Worker/EventLoop/SceneMgr/RPC 的大型运行容器。`Node.OnStart` 先启动模块、导出服务、启动子 Worker，再导出全部服务；关闭时先停 Worker，再停模块。这类顺序纪律很强。

证据：

- BigCat Node 启动顺序：`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/Node.cs:157`
- BigCat SceneMgr 持有 scene 字典、active scene、closed scene、GTime、CoroutineMgr：`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Gameplay/SceneMgr.cs:68`, `82`, `93`, `97`

TryGet 没有 Worker/RPC，这是正确的 Route C 取舍。但 TryGet 仍可吸收两点：

- 更显式的 frame boundary，不靠 phase 顺序启发式推断新帧。
- Scene/Coroutine 与 Procedure 的协作模型，V2.5 做 SceneModule 时参考。

### 5.3 对 hsenl：缺 Unity Proxy 产品化、UI 和网络 Adapter

hsenl 的 `FrameworkProxy` 是一个清晰 Unity 入口：Awake 初始化并 Start，Update/LateUpdate 转发，Quit Destroy。虽然它把 Editor 类放进同文件不是好模式，但入口对 Unity 用户很直接。

证据：

- hsenl `FrameworkProxy.Awake/Update/LateUpdate`：`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/Framework/FrameworkProxy.cs:15`, `25`, `29`
- hsenl UIManager 单实例/多实例 UI、YooAsset 加载：`ReferenceFramework/hsenl/.../UIManager.cs:56`, `112`, `197`
- hsenl TCP 客户端纯 C# 网络边界：`ReferenceFramework/hsenl/.../TcpClient.cs:24`, `41`, `177`

TryGet 的 `TryGetMonoEntry` 还在 Samples，是主要差距。网络也只有 `INetClient` 契约，没有真实 TCP/KCP/WebSocket Adapter。

### 5.4 对 AlicizaX：缺服务作用域、UI operation/cancellation 和对象池工程化

AlicizaX 的 `ServiceWorld` 有 App/Scene/Gameplay 三层 scope，支持就近查找、Scope tick、逆序 Dispose。这正好对应 TryGet 未来 UI/Scene/Gameplay 生命周期问题。

证据：

- `ServiceWorld` App/Scene/Gameplay scope：`ReferenceFramework/AlicizaX/.../ServiceWorld.cs:19`, `50`, `61`
- `TryGet` 服务查找可参考：`ServiceWorld.cs:72`, `90`
- Scope tick 与 dispose：`ServiceWorld.cs:98`, `122`

AlicizaX UIService 也比 TryGet 当前目标更成熟：open layer 数据、operation version、防并发过期、CancellationToken、可见性和深度排序。

证据：

- UI async open operation version：`ReferenceFramework/AlicizaX/.../UIService.Open.cs:65`, `74`, `76`
- UI stack/layer push/pop：`UIService.Open.cs:200`, `221`
- 可见性/深度排序：`UIService.Open.cs:293`, `320`

结论：TryGet 不应照搬其静态 AppServices 和 UniTask 依赖，但应该在 V2.3/V2.5 前做“服务作用域是否进入 Core”的 ADR。

### 5.5 对 DGame：缺 GameObjectPool、资源/热更/红点等商业工程能力

DGame 和 TEngine 类似，是完整 Unity 客户端项目。TryGet 已吸收 DGame timer catch-up 的思想，并修正为每 timer maxCatchUp，而不是 DGame 的全局预算。但 DGame 仍有一些 TryGet 缺失的工程化能力：

- GameObjectPool async 创建、spawn、recycle、取消。
- MemoryCollector 诊断。
- RedDot 代码生成和编辑器工具。
- YooAsset/HybridCLR/Luban 项目链路。

证据：

- DGame Timer 全局 bad frame 预算：`ReferenceFramework/DGame/.../GameTimerModule.cs:16`, `112`
- DGame MemoryCollector 统计：`ReferenceFramework/DGame/.../MemoryCollector.cs:13`, `25`
- DGame GameObjectPool async：`ReferenceFramework/DGame/.../GameObjectPoolModule.cs:50`, `69`, `135`
- DGame UI 查找 UIRoot 并注册 controller：`ReferenceFramework/DGame/.../UIModule.cs:51`, `74`

结论：TryGet V2.x 不需要复制 DGame 的项目链路，但 V2.4 资源、V2.8 热更做起来时不能只停留在接口。

### 5.6 对 MyFramework：运行时方向不宜吸收，Analyzer 和契约执法值得参考

MyFramework 的运行时偏命令系统、静态全局、弱类型事件 ID、多线程池和大量 Unity 工具函数，这与 TryGet 当前“显式 host + typed event + 单线程 Core”方向相反。

但它的 AnalyzerUnity 有价值：`RESET001` 检查池化对象 reset，`BASE001` 检查 override 调 base。这类契约执法比人工 review 稳定。

证据：

- Analyzer 入口：`ReferenceFramework/MyFramework/ToolProject/AnalyzerUnity/AnalyzerUnity/AnalyzerUnityAnalyzer.cs`
- `RESET001`：`ReferenceFramework/MyFramework/ToolProject/AnalyzerUnity/AnalyzerUnity/AnalyzerResetProperty.cs`
- `BASE001`：`ReferenceFramework/MyFramework/ToolProject/AnalyzerUnity/AnalyzerUnity/AnalyzerCallBase.cs`

结论：不吸收其运行时模式；可在 Pool/TGTask/Procedure 成熟后考虑小型 Analyzer，但不是当前最高优先级。

## 6. 建议路线

### 近期必须修

1. 修复 `TimerModule` repeating timer callback 自取消/自暂停被覆盖的问题，并补 EditMode 测试。
2. 关闭 Unity Editor 后重跑 EditMode，确认 412/412 或记录新失败。
3. 把正式 Unity host 从 Samples 迁入 `Runtime/Unity`，Samples 只继承/演示。

### V2.3 最小可用闭环

1. `IUIModule` + `UIWindow` 生命周期最小契约。
2. `IUIResourceLoader` 先用 Resources 或 Addressables/YooAsset 单 Adapter，不做多套。
3. UI open/close 返回 `TGTask`，支持取消/operation version，避免并发打开关闭错序。
4. 用 Procedure Stack 演示 MainMenu -> Gameplay -> PauseMenu。

### V2.4/V2.5 之前的架构门

1. 是否引入 App/Scene/Gameplay service scope。
2. `TGTask` 是否补 `CancellationToken` 兼容层，还是保留自定义 owner-scope cancellation。
3. SourceGen 对热更程序集的后加载注册策略。
4. Resource handle 是否采用引用计数 + awaitable handle。

## 7. 最终结论

MyTryGetFramework 当前代码质量的核心矛盾是：Core 很干净，但客户端框架价值还没有闭环。它已经成功避开了参考框架里常见的静态全局、Unity 强绑定、过早服务端/ECS/RPC 复杂度；但也因此还没有 TEngine/DGame/hsenl/AlicizaX 那种“装进 Unity 项目就能管 UI、资源、场景、热更、网络”的完整能力。

下一阶段不建议继续扩展抽象清单。最有效的推进顺序是：先修正 Timer/Unity 测试证据，再把 Unity Adapter 产品化，然后围绕 UI + Resource 做一个真实可运行的客户端闭环。只有闭环出现后，ServiceScope、AssetHandle、SceneModule、Hotfix 这些参考框架能力才有明确落点。
