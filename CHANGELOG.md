# Changelog

V0.x 时期：未承诺时间，按 Gate criteria 升版本（design.md §12）。

> **历史段阅读说明**：V0.6–V1.0 历史记录中提及的 `Runtime/Core/Common`、`Runtime/Core/Entity`、`Runtime/Core/Module/EventHandler*`、`IPlugin`、`INetServer`、`ITickLoop/IFrameLoop`、`Samples/Shared`、`ITask/TaskBody` 等路径或能力，在 V2.0 路线 C 重定向（ADR-0020）后已迁移或移除。当前权威布局见 `MyTryGetFramework/Assets/MyTryGetFramework/ARCHITECTURE.md`。

## V2.0 — 自审修复：C4 transition 取消 + Samples/Net 复活

> 2026/06/13。对本轮（C2/C11/C10/生成器迁移/C4）产出做系统性自审，发现并修复 4 个问题，其中 2 个是真实 bug/损坏。

### Fixed

- **C4 transition 泄漏（真实 bug）**：`Stop`/`Shutdown` 打断 pending 异步切换时，该切换的 transition task 永不完成（`await module.Push(...)` 的调用者永久挂起）——`RunTransition` 的 tcs 是局部变量、Stop 访问不到。修复：ProcedureModule 持 `_activeTransition` 引用，`Stop`/`Shutdown` 调 `CancelActiveTransition()` 以 `OperationCanceledException` 完成它。+2 回归测试（含「取消后迟到完成幂等」边界）。
- **Samples/Net 长期编译失败（损坏，无 CI/不在 Unity 测试路径，坏了很久没被发现）**：停留在 ADR-0020 重定向前的旧 API（`IModuleHost`/`BootstrapOptions`/`Bootstrap.CreateHost`/`host.EventBus`/`AssemblyManifestRegistry`/`IModule.OnInit(IModuleHost)`）。更新到当前 API（`IModuleSystem`/`GameLauncherOptions`/`GameLauncher.CreateHost`/`host.EventModule`/`ModuleRegistry`），并用 C4 可 await 切换重写主流程（删除 `WaitForEnter`/`WaitForExit` 轮询，改 `await proc.Start/Replace`）——成为 C4 的活样板。
- **生成器引用路径迁移遗漏**：`Samples/Net` csproj 对生成器工程的 `ProjectReference` 仍指向旧 `Tools/` 路径（上一提交 `adc6420` 迁移时只改了 Shadow csproj、漏了 Samples）。修正为 `Generators~/`。
- 本轮新写但已过时的文档路径（CHANGELOG / plan 中 `Tools/...sln`、`Tools/README.md`）修正为 `Generators~/`（历史段的 Tools/ 提及是正确快照，保留）。

### Verification

- **Samples/Net `dotnet run` 端到端跑通**：`[Module]`/`[EventHandler]` 自动注册在 .NET 端生效（auto-registered Module + TickEvent handler）；Boot→Login→InGame 三阶段可 await 切换、每段 async enter 按序完成；完整生命周期顺序正确。这是 Shadow csproj 之外验证 Core 跨端 + 生成器 + C4 的活样板。
- Unity EditMode：380/380（C4 测试增至 12 例）；Shadow csproj `dotnet build` 0 警告 0 错误。

---

## V2.0 — C4 Procedure Transition Result（可 await 流程切换）

> 2026/06/13。消除 §7.4 点名的 shallow interface：ProcedureModule 此前暴露 `IsEntering`/`IsExiting`/`LastAsyncError`
> 三个内部异步状态字段，调用者必须逐帧轮询才能知道异步切换是否完成/出错。参考框架复查证实 TEngine（FsmState
> 同步 ChangeState）、BigCat（SceneMgr 同步 Add/Close）、hsenl（ProcedureLine 是 pipeline）**都把流程切换做成同步 API**、
> 异步性靠状态内部自己 await——TryGet 借 TGTask（C11 池化收口）做出了比所有参考更 deep 的「可 await 切换」。

### Changed

- **`IProcedureModule.Start/Push/Pop/Replace` 签名 `void` → `TGTask`**（向后兼容：现有不接收返回值的调用照常编译，TGTask 是 struct 丢弃无副作用）。返回表示「本次切换完成」的 transition task：同步流程立即完成（`CompletedTask`），异步流程（`IAsyncProcedure`）在 OnEnterAsync/OnExitAsync 全链完成时完成，`Replace` 串联 exit→enter 两段异步、只在两者都完成后才完成。
- **异步错误双通道**：既写入 `LastAsyncError`（保留兼容，旧测试/旧代码不破坏），又通过 transition task 抛出（`await module.Push(...)` 时 throw）。调用者可 `await` 切换、用 try/catch 处理错误，不再轮询。
- pending 时再切换保持「拒绝」策略（`ThrowIfAsync`）；可 await 后正常用法是 await 上一个再切下一个，自然避免冲突。
- `Stop` 保持 `void`（「立即清栈 + 后台跑异步 exit」的终结语义，不适合单一 transition）；`IsEntering`/`IsExiting`/`LastAsyncError` 保留兼容。

### Fixed

- Shadow csproj（`ServerProject/MyTryGetFramework.Core`）对 Source Generator 工程的 `ProjectReference` 路径修正为迁移后的 `Generators~/`（上一提交 `adc6420` 迁移遗漏，产生 MSB9008 警告）。

### Verification

- Unity EditMode：378/378 全绿（368 现有 + 10 个 `ProcedureTransitionTests`：同步立即完成、异步跟随完成、Pop resume 时序、Replace 两段串联、错误经 await 抛出且 LastAsyncError 兼容）。
- Shadow csproj `dotnet build` 0 警告 0 错误（含生成器 analyzer 在 .NET 端验证）。

---

## V2.0 — Source Generator 源码迁入框架包（Generators~）

> 2026/06/13。把生成器源码工程从仓库根 `Tools/` 迁入框架包内 `MyTryGetFramework/Assets/MyTryGetFramework/Generators~/`，
> 让生成器逻辑随框架自包含分发。采用 Unity 官方 Netcode for Entities 的 `Source~` 模式：`~` 结尾文件夹被 Unity 完全
> 忽略（不编译/不 import），生成器源码因而可安放在框架内。dll 仍为必需的构建产物（Unity 6 硬约束，无法消除，
> 见 docs.unity3d.com `create-source-generator`），保持在 `Runtime/Core/Generators/` 并带 `RoslynAnalyzer` label。

### Changed

- 生成器源码工程 `Tools/MyTryGetFramework.SourceGenerator*` → `MyTryGetFramework/Assets/MyTryGetFramework/Generators~/`（sln / build 脚本 / 生成器工程 / 测试工程整体迁入）。
- 生成器 csproj 的 `CopyToUnityAssets` 同步路径相应缩短（`..\..\Runtime\Core\Generators`）。
- `.gitignore`：Tools 例外规则改为 `Generators~/` 路径（csproj/sln 进库、bin/obj 忽略）。
- 文档（`CLAUDE.md`、`Generators~/README.md`）更新路径并补充 Netcode `Source~` 先例与 Unity 6 dll 约束说明。

### Verification

- 生成器单测：12/12 绿（从框架内新位置构建）。
- Unity EditMode：368/368 全绿 —— 验证 `~` 文件夹被 Unity 正确忽略（未误编译生成器源码）且新 dll 工作正常。

---

## V2.0 — Source Generator 工程化 + C10 编译期诊断

> 2026/06/13。把 Source Generator 从「Unity 里一个孤立 dll」确立为「源码工程一等公民 + dll 可重建产物」，
> 并落地 C10（生成器对非法输入报编译期诊断）。参考框架印证：AlicizaX/MyFramework/BigCat 的生成器/分析器
> 均为独立源码工程 + 进库 dll（Unity 无法编译生成器源码，dll 是必需产物）。

### Fixed

- **生成器命名漂移（被 dll 黑盒掩盖的真实 bug）**：`ModuleManifestGenerator` 源码生成对 `TryGet.AssemblyManifestRegistry` 的调用，但运行时类在 V2.0「收敛模块系统命名」（`fc93e45`）已改名为 `ModuleRegistry`——源码漏改且无人重建 dll，导致 git 源码生成「不存在的类」而 Unity 用旧 dll 掩盖。现已对齐为 `ModuleRegistry`，并由 `BaselineGenerationTests` 锁定（生成代码与运行时桩一起编译，再漂移即测试失败）。

### Added

- **生成器测试工程** `Tools/MyTryGetFramework.SourceGenerator.Tests/`：用 Roslyn `CSharpGeneratorDriver` 在纯 .NET 下驱动生成器、断言生成代码与诊断（12 例：4 基线 + 8 诊断）。对标 BigCat 的生成器测试工程。
- **C10 编译期诊断 TG0001-TG0006**：非法 `[Module]` / `[EventHandler]` 用法不再静默 `return null`，改为带源码位置的编译错误（abstract/static 类、服务类型非接口、**未实现声明的服务接口**、handler 非 static、签名不符、事件参数非 struct）。其中「类必须实现服务接口」是 `ModuleAttribute` 注释一直声称却从未执行的检查。诊断信息用可缓存的 `LocationInfo`/`DiagnosticInfo`，不破坏增量生成缓存。
- **生成器开发基建**：专属 `MyTryGetFramework.SourceGenerator.sln`、一键构建脚本 `build.ps1`/`build.sh`（测试 + Release 构建 + 同步 dll）、`Tools/README.md` 工作流文档；项目 `CLAUDE.md` 增补生成器工作流说明。

### Changed

- **dll 同步策略**：生成器 csproj 的 `CopyToUnityAssets` 改为**仅 Release 构建触发**，`Debug`/`dotnet test` 不再污染 Unity 现役 dll；显式 `<Deterministic>true</Deterministic>` 让相同源码产出逐字节一致的 dll，减少 git 二进制漂移。
- `.gitignore` 增 `!Tools/**/*.sln` 例外（手维护的生成器开发 sln 进库）。

### Verification

- 生成器单测：`dotnet test "MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.sln"` → 12/12 绿。
- Unity EditMode：368/368 全绿（用重建后的 dll，确认命名修复正确 + C10 诊断不误伤现有合法代码）。

---

## V2.0 — C11 TGTask 池化生命周期收口（消费侧统一归还）

> 2026/06/13 第三轮参考框架分析（hsenl HTaskCompletionBody + BigCat ValuePromise 双印证）后实施。
> 一次清掉三项关联技术债：IsCompleted 不校验 Version / Manual body 漏 GC / 调度器热路径每次堆分配。
> 设计相对 C11 草案的调整：不做「完成即 Version++」（会破坏 tcs TrySet 静默语义），
> 改用「消费侧统一归还 + 全路径 version 校验」达成同等安全性。详见计划文档 Candidate 11。

### Changed

- **消费侧统一归还**：`Awaiter.GetResult`（finally）与 `Forget` 对 Builder 与 Manual body 一律归还池（原先仅 Builder）；`TGTask.FromResult/FromException/FromCanceled` 的 body 也随消费回池。双重归还由进入 try 前的 version 检查防护。
- **IsCompleted version 校验**：`TGTask.IsCompleted` / `Awaiter.IsCompleted` 在 `Version != Body.Version`（body 已被消费归还）时返回 `true`，后续 `GetResult` 抛 `TGTaskExpiredException`——旧句柄不再可能读到复用 body 的他人状态（06/11 修复过的 bug 类从根上关闭）。
- **TGTaskCompletionSource.Return 加 version 守卫**：body 已被消费侧归还后调用为 no-op，不再双重入池；仅「任务从未被 await」时仍是有效的手动归还路径。
- **tcs 对象池化（非泛型）**：新增 internal `Rent()/Recycle()` 静态池（容量 64）；`TGTaskScheduler`（Yield/Delay/WaitForFrames 三类队列 + Shutdown）与 `TimerModuleAsyncExtensions`（WaitAsync/WaitUnscaledAsync）全部改用池化 tcs，SetResult 后立即回收——调度器热路径稳态零堆分配。
- **WaitForFrames(0)** 改返回 `TGTask.CompletedTask`（零分配，语义不变）。

### Verification

- Unity EditMode：368/368 全绿（新增 `TGTaskPoolingLifecycleTests` 10 例：Manual/FromException 回池、双归还防护、过期句柄语义、调度器与 Timer 稳态池命中、Shutdown 取消回收）。
- Shadow csproj `dotnet build` 通过，0 warning / 0 error。

---

## V2.1 — EventBus 零 GC 派发 + 生命周期策略 ✅（2026/06/13 收尾完成）

> 2026/05/30 基于第二轮 AlicizaX / AmaniDawn·DGame 源码复核，吸收重入安全延迟增删模式，修正当前 `Publish` 每次 `ToArray()` 分配的问题，并补上参考框架都缺失的 handler 异常隔离。
> 2026/06/13 收尾（C2 关单）：补 handler 异常上报钩子与派发递归深度护栏（来源参考 MyFramework EventSystem MAX_DEPTH，第三轮分析 §6ter）。

### Changed

- **EventBus Publish**：从 `list.ToArray()` 快照派发改为内部 handler 表 + pending add/remove；稳态 `Publish` 不再分配 snapshot 数组。
- **派发中增删语义**：保持 V2.0 行为，`Subscribe` / `Unsubscribe` during dispatch 只影响下一次 `Publish`。
- **handler 异常策略**：从 fail-fast 改为隔离；单个 handler 抛异常不阻断后续 handler，异常记录在 internal 诊断中供测试/后续诊断使用。
- **EventScope 边界测试**：补充已 Dispose scope 下订阅不残留 handler 的用例，继续保持 owner 显式 Dispose。
- **EventHandlerRegistry 行为锁定**：补充 legacy duplicate registration 与 `ApplyAll` fail-fast 的测试。

### Added（2026/06/13 收尾）

- **`IEventModule.HandlerException` 事件**：每个被隔离的 handler 异常触发一次（事件类型 + 异常），钩子自身异常吞掉防级联；业务启动时订阅一次即可统一路由到 ILogger，handler 异常不再静默。
- **Publish 递归深度护栏**：嵌套发布深度超过 `EventModule.MaxPublishDepth = 32` 抛 `InvalidOperationException`，防止事件互相触发形成无限递归导致栈溢出；嵌套场景下该异常被外层异常隔离捕获并上报。

### Verification

- Unity EditMode：368/368 全绿（新增 `EventModuleHardeningTests` 8 例：上报钩子、钩子异常不级联、自递归/互递归护栏、护栏触发后恢复正常）。
- Shadow csproj `dotnet build` 通过，0 warning / 0 error。

---

## V2.0 — 路线 C 重定向：纯客户端服务框架 + Procedure Stack

> 2026/05/26 架构重定向（ADR-0020）：基于 TEngine/BigCat/hsenl/Fantasy 四框架对比分析，
> 从"双端框架"转向"纯客户端服务框架"。ECS/IPlugin/服务端契约全部移除。
> ProcedureModule 升级为栈模式（吸收 BigCat SceneMgr.stack 设计）。

### Removed

- **ECS 整套**（~20 文件）：Entity/Aspect/Tag/Phase/Query/SystemBase/SystemGroup/IPureComponent/ComponentSystemHooks/BitArray256/TypeIndex/EntityWorld/EntityEventDispatcher/IWorldEventBus/SystemRegistry/WorldProxy + 13 个测试文件
- **IPlugin 系统**：IPlugin/IPluginHost/IPlugPoint/ModuleHostPlugPoints + ModuleHost Plugin 代码 + PluginTests
- **服务端契约**：INetServer/IConnection/ConnectionId/ConnectionState/NetPlugPoints/ITickLoop/IFrameLoop/Samples/Shared
- **已废弃 Module**（~12 文件）：ILogModule/ConsoleLogModule/LogModuleAdapter/ISaveModule/MemorySaveModule/SaveModuleAdapter/IConfigModule/MemoryConfigModule/IResourceModule/MemoryResourceModule/ILocalizationModule/MemoryLocalizationModule + 5 个测试文件
- **Source Generator**：SystemRegisterGenerator/ComponentSystemGenerator/HelloWorldGenerator

### Changed

- **INetClient** 简化为纯客户端接口（IsConnected / ConnectAsync / DisconnectAsync / Send / 4 events）
- **ProcedureModule** 升级为 Stack 模式（Push/Pop/Replace/StackDepth + IProcedure.OnPause/OnResume）
- **IModuleHost** 不再继承 IPluginHost
- **ModuleHost.Update** 移除 BeforeUpdate/AfterUpdate plugin 触发
- **WorldEventBus** 直接实现 IEventBus（移除 IWorldEventBus 中间接口）
- **ConfigNotFoundException** 移入 IConfigSource.cs

### Stats

- Core 文件数：86 → 55（精简 36%；含 V2.2 FrameLoop 提前落地的 5 个文件 FramePhase/IEarlyUpdateModule/IFixedUpdateModule/IEndOfFrameModule + EventBus.cs，纯 V2.0 收敛态约 50）
- 编译验证：Shadow csproj (netstandard2.1) + Samples/Net + Generator csproj 全部 0/0（dotnet 10.0.102 实跑）
- Samples/Net `dotnet run` 端到端跑通（EXIT=0：[Module]/[EventHandler] 自动注册 + Boot→Login→InGame 异步流程）
- Unity EditMode 测试：需 Unity Editor 验证（本环境无法跑），待并入 main 前确认全绿

### 备注：V2.2 FrameLoop 提前落地

本次提交同时包含 V2.2「ModuleHost 多阶段 Update」的实现：ModuleHost 已支持 EarlyUpdate/FixedUpdate/Update/LateUpdate/EndOfFrame 五阶段派发 + Initialize 期执行表分桶，实现进度领先路线图。ModuleHost.cs 的 V2.0 收敛改动（移除 IPlugin）与 V2.2 多阶段改动揉在同一文件，无法拆成两个独立可编译提交，故合并落地。`FramePhase` 枚举当前未被消费（派发靠 `is IXxxModule` 类型判断），去留待 C3 Phase-aware Scheduler 决策。

---

## V1.0 — 双端架构契约 + 网络抽象层 + Shared 代码边界（Superseded by V2.0）

> 2026/05/24 用户指令"业务逻辑先轻放"重定向：原 V1.0 = MMO Server/Client Demo（业务重）改为"框架架构契约 + 网络抽象 + Shared 边界规范"，MMO demo 转 V1.1+ Adapter Demo minor。本 minor 仅契约不实现，KCP/LiteNetLib/TCP 实现全部留 V1.1+ Adapter。

### 迭代 0 — V1.0 PRD（路线重定向）

**Added**
- `docs/design/V1.0-architecture-contracts.md`：4 框架对标（hsenl Channel/Service + ET Service/Session + Fantasy NetworkProtocolType + BigCat 三端 csproj） + INetClient/Server/Connection/Message + 5 IPlugPoint 详细 API + Samples/Shared 物理结构 + ITickLoop/IFrameLoop + Unity Entry 模板 + 5 Iter 拆分 + 与原 MMO demo 路线图差异表

**关键决策**
- V1.0 转向"架构契约"：原 V2 doc §6.5 V1.0 = MMO demo，2026/05/24 用户指令"业务逻辑先轻放"重定向到"双端架构契约 + 网络抽象 + Shared 边界规范"，MMO demo 推迟 V1.2 Demo minor
- Core 只持网络契约（接口 + marker + IPlugPoint），不含具体协议实现（详见 ADR-0019）
- 复用 V0.9 IPlugin/IPluginHost/IPlugPoint 机制承载网络生命周期事件（5 件 IPlugPoint：IOnConnectionStarted / Closed / RawDataReceived / MessageReceived / NetError）

### 迭代 1 — Samples/Shared csproj 物理结构 + ADR-0018

**Added**
- `Samples/Shared/TryGet.Shared.csproj`：netstandard2.1，ProjectReference Core，承载跨端业务代码
- `Samples/Shared/SharedInfo.cs`：placeholder marker（V1.0 期间业务侧暂不填充）
- `docs/adr/0018-shared-code-boundary.md`：Samples/Shared 跨端共享代码边界规范

**Notes**
- Shared 允许：业务 Aspect / 业务 Module 接口 / 网络消息 struct / 业务 Event struct / 业务工具类 / [Module]/[SystemRegister]/[EventHandler] 标记
- Shared 禁止：UnityEngine 引用 / Net 专属 API / KCP/LiteNetLib 等协议库 / 序列化库真依赖 / Editor-only API
- Unity 端接入方式：V1.0 选**源引用**（asmdef + 物理路径），不用 Plugin DLL（避免调试 step-into 体验差，ET 已踩坑）

### 迭代 2 — 网络抽象层契约（Runtime/Core/Net/）

**Added**
- `Runtime/Core/Net/INetClient.cs`：客户端连接（ConnectAsync / DisconnectAsync / Send / 3 events）+ IConnection（服务端单连接，Id / State / Send / CloseAsync）
- `Runtime/Core/Net/INetServer.cs`：服务端监听（StartAsync / StopAsync / Connections / 4 events）
- `Runtime/Core/Net/INetMessage.cs`：网络消息基础 marker 接口
- `Runtime/Core/Net/ConnectionId.cs`：readonly struct + IEquatable + ConnectionState enum（5 状态）
- `Runtime/Core/Net/NetPlugPoints.cs`：5 IPlugPoint 派生接口（IOnConnectionStarted / Closed / RawDataReceived / MessageReceived / NetError）

**Notes**
- 全部仅契约，Core 无任何具体协议实现（KCP / LiteNetLib / TCP / WebSocket 全部留 V1.1+ Adapter）
- ConnectAsync / DisconnectAsync / StartAsync / StopAsync 返回 V0.6 TGTask（与异步原语集成）
- IPlugPoint 5 件复用 V0.9 IPlugin/IPluginHost 机制：业务 `host.AddPlugin<IOnConnectionStarted, MyMonitorPlugin>(...)` 即注入网络中间件，零侵入
- 简化 vs hsenl：不暴露 buffer-level event 到 Core 契约（业务多数场景不需要）；不引入 Service 中间层（INetServer 直接管 IConnection 集合）

### 迭代 3 — ITickLoop / IFrameLoop 双端时间抽象

**Added**
- `Runtime/Core/Time/ITickLoop.cs`：ITickLoop（服务端 fixed-tick，TickInterval 常量步长）+ IFrameLoop（客户端可变-帧）

**Notes**
- 与 IClock / IUpdateModule 概念分工：
  * IClock = 时间查询门面（DeltaTime / FrameCount）— "几点了？"
  * IUpdateModule = 业务每帧回调（dt 上帧实际耗时）— "客户端可变帧"
  * ITickLoop = 业务每 tick 回调（dt 固定 TickInterval）— "服务端定步长"
  * IFrameLoop = 更轻量的客户端帧回调（不经 ModuleHost）
- Core 不提供 driver 实现；服务端 demo 通常用 `while (true)` + `TGTaskScheduler.Delay(TickInterval)` 自构

### 迭代 4 — 双端 Entry 模板标准化

**Added**
- `Assets/MyTryGetFramework/Samples/Unity/Entry/TryGetMonoEntry.cs`：MonoBehaviour 抽象 base，业务子类 override Setup(IModuleHost) 即用
- `Assets/MyTryGetFramework/Samples/Unity/Entry/MyTryGetFramework.Samples.UnityEntry.asmdef`：Unity 端独立 asmdef，references = [MyTryGetFramework.Core]

**Notes**
- TryGetMonoEntry 桥接 MonoBehaviour 生命周期到 ModuleHost：
  * Awake → Bootstrap.CreateHost(Options) → Setup(host) → host.Initialize
  * Update → host.Update
  * LateUpdate → host.LateUpdate
  * OnDestroy → host.Shutdown
- DontDestroyOnLoad 默认开启（业务可子类 override MakeDontDestroyOnLoad）
- **不定义 IEntry interface**（与 V0.7 Samples/Net/Entry.cs 决策一致）— Unity MonoBehaviour 与 .NET Main(string[]) 形态本质不同，5 个商业 Unity 框架均无 IEntry interface
- Samples/Net/Entry.cs 保持不变（V0.7 已成形）

### 迭代 5 — V1.0 收尾

**Added**
- `docs/adr/0019-network-abstraction-positioning.md`：网络抽象层定位 ADR（Core 持契约 + Adapter 持实现的分层决策 + IPlugPoint vs ModuleHost 横切的关系矩阵）

**Modified**
- `CHANGELOG.md`：本段
- `MyTryGetFramework/Assets/MyTryGetFramework/ARCHITECTURE.md`：V1.0 段
- `docs/strategy/V2-roadmap-tasks.md`：V1.0 段路线修订（MMO demo → V1.2 Demo minor）

**累计 V1.0 产物**
- 2 个 ADR（0018 Shared 边界 + 0019 网络抽象定位）
- 1 个新 csproj（Samples/Shared）+ 1 个 Unity asmdef（Samples/Unity/Entry）
- 8 个 Core 契约文件（INetClient/Server/Connection/Message + ConnectionId/State + 5 IPlugPoint + ITickLoop/IFrameLoop）
- 1 个 MonoBehaviour 模板（TryGetMonoEntry）
- 全套契约 - dotnet build 0/0（Shadow + Shared + Net + Samples/Net），Unity 端 .meta 待 Editor 内自动生成

**V1.0 不做（明确推迟）**
- KCP / LiteNetLib / TCP / WebSocket Adapter 实现 → V1.1
- MemoryPack 等序列化 Adapter 实现 → V1.1
- ServerTickDriver / UnityFrameDriver 等 driver 实现 → V1.1
- MMO 业务 Demo → V1.2 Demo minor
- Actor / MailBox / Location 抽象 → V2.0+ 业务层


## V0.9.5 — Source Generator 注册（**完整落地** — 7/7 Iter）

> 独立 minor。V0.9 PRD 拆分时确认：Source Generator 涉及独立 csproj + Unity asmdef + RoslynAnalyzer 集成 + 多 attribute + IIncrementalGenerator 管线。本 minor 完整落地 Iter 0-7，覆盖 [Module] / [SystemRegister] / [EventHandler] / IComponentSystem 四个自动注册场景。

### 迭代 0 — V0.9.5 PRD

**Added**
- `docs/design/V0.9.5-source-generator.md`：6 维度调研（IIncrementalGenerator vs ISourceGenerator / Unity 集成约定 / dual-trigger init 模式 / 参考实现对标含 TEngine GameEventGen, BigCat APT, GenEvent, Entitas / V0.6-V0.9 手动注册痛点回顾）+ 4 套 API（[Module] / [SystemRegister] / [EventHandler] / IComponentSystem 自动调度）详细设计 + 8 Iter 拆分 + 风险表

**关键决策**
- 使用 `IIncrementalGenerator`（不是已 deprecated 的 `ISourceGenerator`），匹配 2026 主流实践
- Generator 源码放仓库根 `Tools/MyTryGetFramework.SourceGenerator/`，与 Unity Assets 完全隔离；DLL 通过 PostBuild target 自动 copy 到 `Assets/MyTryGetFramework/Runtime/Core/Generators/`
- netstandard2.0 target + Microsoft.CodeAnalysis.CSharp 4.8.0（Unity 6 / Roslyn 4.0+ 标配）
- Value-equatable record DTO（`ModuleInfo`）保证 Incremental pipeline 缓存命中
- Dual-trigger init pattern：`[ModuleInitializer]`（C# 9+ .NET）+ `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `[Preserve]`（Unity IL2CPP）

### 迭代 1 — Generator csproj 骨架 + Hello World

**Added**
- `Tools/MyTryGetFramework.SourceGenerator/MyTryGetFramework.SourceGenerator.csproj`：netstandard2.0 / `IsRoslynComponent=true` / `DevelopmentDependency=true` / PostBuild Copy 到 Unity Assets
- `Tools/MyTryGetFramework.SourceGenerator/src/Generators/HelloWorldGenerator.cs`：烟测 Generator，用 `RegisterPostInitializationOutput` 生成 `__TryGetSourceGenerator_HelloWorld.g.cs`

**Modified**
- `ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj`：加 `<ProjectReference OutputItemType="Analyzer" />` 引用 Generator csproj
- `.gitignore`：白名单 `Tools/**/*.csproj`，忽略 `Tools/**/bin/` + `Tools/**/obj/`

**Notes**
- 验证：`dotnet build -p:EmitCompilerGeneratedFiles=true` 后 `obj/generated/` 下能看到 `__TryGetSourceGenerator_HelloWorld.g.cs`
- 烟测 Generator 不读取任何 attribute / 语法树，仅作管线启动验证

### 迭代 2 — Unity 集成（DLL + RoslynAnalyzer label）

**Added**
- `Assets/MyTryGetFramework/Runtime/Core/Generators/` 目录（含 .meta folder asset）
- `Assets/MyTryGetFramework/Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll`（PostBuild 自动 copy）
- `Assets/MyTryGetFramework/Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll.meta`：PluginImporter 配置 + `labels: [RoslynAnalyzer]`（关键 Unity 集成 label）

**Notes**
- Unity 端验证留给用户在 Editor 中确认（DLL 显示 RoslynAnalyzer label + 生成代码在 Library/Bee/.../*.g.cs 中出现）
- 命名空间隔离：Generator 不引用 `UnityEngine`（Generator 跑在 Roslyn host 内，无 Unity 上下文）；生成代码用 `#if UNITY_5_3_OR_NEWER` 条件包裹 Unity attribute

### 迭代 3 — [Module] Attribute + AssemblyManifest 生成 + dual-trigger + Bootstrap 集成

**Added**
- `Runtime/Core/Module/AssemblyManifestRegistry.cs`：Generator 输出代码用的 runtime 收集器（`Register(Action<IModuleHost>)` / `ApplyAll(IModuleHost)` / `Count` / `ClearForTests`）
- `Runtime/Core/Module/ModuleAttribute.cs`：业务用 `[Module(typeof(IService))]` 标记 Module 实现类
- `Tools/MyTryGetFramework.SourceGenerator/src/Generators/ModuleManifestGenerator.cs`：扫 `[TryGet.Module]` 标记类，用 `ForAttributeWithMetadataName` 入口（O(1) attribute 查找）+ value-equatable `ModuleInfo` record，按 assembly 分组生成 `__AssemblyManifest_<asm>.g.cs`
- `Samples/Net/SourceGenDemo.cs`：`IGreetingModule` + `[Module(typeof(IGreetingModule))] GreetingModule` 自动注册 demo

**Modified**
- `Runtime/Core/Module/Bootstrap.cs`：`CreateHost` 在注册 Core 基础三件套后调 `AssemblyManifestRegistry.ApplyAll(host)`
- `Samples/Net/TryGet.Samples.Net.csproj`：加 `<ProjectReference OutputItemType="Analyzer" />` 让 Samples/Net 也享受 [Module] 自动注册
- `Samples/Net/Program.cs`：`RunMainAsync` 内 `host.Get<IGreetingModule>()` 拉自动注册的 Module 并打印问候语

**Notes**
- 生成代码结构（实测从 Samples/Net 编译产物提取）：
  ```csharp
  internal static class __AssemblyManifest_TryGet_Samples_Net
  {
      private static bool _initialized;
  #if UNITY_5_3_OR_NEWER
      [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
      [global::UnityEngine.Scripting.Preserve]
  #endif
      [global::System.Runtime.CompilerServices.ModuleInitializer]
      public static void Initialize() { /* dedup + AssemblyManifestRegistry.Register */ }
  }
  ```
- **Net 端端到端验证通过**：`dotnet run` Samples/Net 看到 `[Info] Hello from V0.9.5 auto-registered Module, Samples/Net!`，业务 setup 内**没有任何 host.Register&lt;IGreetingModule&gt;(...)** 行
- 同 [Module] 标 abstract / static 类时 Generator 静默忽略（不报错也不生成）
- 业务 ServiceType 错误（class 不实现该接口）会在 user-code 编译期由 Roslyn 检测（生成代码 `new T()` cast 到 `host.Register<TService>` 失败）
- **限制**：`AssemblyManifestRegistry.ApplyAll` 仅看到 ApplyAll 之前已 static-init 的 assemblies；后加载的 assembly 注册不会回填已构造 host

### 后续 Iter（V0.9.5+，未落地）

- ~~**Iter 4**~~ — **Done**：见下方 §迭代 4
- ~~**Iter 5**~~ — **Done**：见下方 §迭代 5
- ~~**Iter 6**~~ — **Done**：见下方 §迭代 6
- ~~**Iter 7**~~ — **Done**：本段（CHANGELOG/ARCHITECTURE/路线图收尾 + commit）

### 迭代 4 — [SystemRegister] Attribute + SystemRegistry + Generator

**Added**
- `Runtime/Core/SystemRegisterAttribute.cs`：业务用 `[SystemRegister(Phase, groupName)]` 标记 SystemBase 子类
- `Runtime/Core/SystemRegistry.cs`：runtime 收集器（`Register(Action<EntityWorld>)` / `ApplyAll(EntityWorld)` / `Count` / `ClearForTests`）
- `Tools/.../src/Generators/SystemRegisterGenerator.cs`：扫 `[SystemRegister]` + 生成 `__SystemManifest_<asm>.g.cs`

**Notes**
- 与 AssemblyManifestRegistry 的设计差异：System 注册到 EntityWorld（可能多 world 实例），业务**显式**调 `SystemRegistry.ApplyAll(world)`，framework 不自动 hook 进 EntityWorld 构造器（避免多 world 歧义 + 测试隔离困难）
- Phase 枚举值用 raw int 序列化进 record，生成代码内 switch 转回 `global::TryGet.Phase.Update` 等命名引用
- 同 (Phase, groupName) 多 System 生成代码会每次 `new SystemGroup(name)` — V1.x 可优化为按组缓存，当前 group cache 留业务侧自管

**Verification**（人工 inspect 生成代码 + 临时 [SystemRegister] 测试类后 inspect）
- 生成正确的 `world.RegisterSystem(new MyMovementSystem(), global::TryGet.Phase.Update)` 调用
- 排序稳定（按 ClassFullName ordinal）

### 迭代 5 — [EventHandler] Attribute + EventHandlerRegistry + Generator

**Added**
- `Runtime/Core/Module/EventHandlerAttribute.cs`：标记 `public static void M(TEvent evt)` 方法
- `Runtime/Core/Module/EventHandlerRegistry.cs`：收集 `Action<IEventBus>` 委托
- `Tools/.../src/Generators/EventHandlerGenerator.cs`：扫 `[EventHandler]` 静态方法 + 生成 `__EventHandlerManifest_<asm>.g.cs`

**Modified**
- `Runtime/Core/Module/Bootstrap.cs`：`CreateHost` 内调 `EventHandlerRegistry.ApplyAll(host.EventBus)`（在 `AssemblyManifestRegistry.ApplyAll` 之后）

**Generator 校验逻辑**
- 方法必须 `public static`
- 返回类型必须 `void`
- 参数必须 1 个且类型必须是 struct（满足 IEventBus.Subscribe&lt;T&gt; 约束）
- 不满足任何条件就静默忽略（不报错也不生成 Subscribe 代码）

**端到端验证（Samples/Net）**
```
[Info] TickEvent handler observed LastTickIndex = 42
```
- `GameplayHandlers.OnTickEvent` 标 `[EventHandler]` 后**没有任何手动 Subscribe** 行，单纯 publish 就触发 handler

### 迭代 6 — IComponentSystem 自动调度

**Added**
- `Runtime/Core/Entity/ComponentSystemHooks.cs`：泛型静态 hook 表（`ComponentSystemHooks<TComponent>.AttachHook` / `DetachHook` events，public + InvokeAttach/InvokeDetach internal helpers + ClearForTests）
- `Tools/.../src/Generators/ComponentSystemGenerator.cs`：扫所有非抽象/非静态 class，semantic 检查是否实现 `IComponentSystem<T>`，生成 `__ComponentSystemManifest_<asm>.g.cs`

**Modified**
- `Runtime/Core/Entity/EntityPureComponentExtensions.cs`：
  - `AddComponent<T>`：成功添加后 `ComponentSystemHooks<T>.InvokeAttach(entity, component)`
  - `RemoveComponent<T>`：成功移除后 `ComponentSystemHooks<T>.InvokeDetach(entity, component)`

**Notes**
- 本 Generator 通过 Interface 实现（非 Attribute）触发，使用 `CreateSyntaxProvider` 扫 class declaration + semantic 验证（比 ForAttributeWithMetadataName 慢但接口契约不需要额外标 attribute）
- struct PureComponent 不装箱：call-site 泛型 T 是静态类型，`ComponentSystemHooks<T>.InvokeAttach` 直接传 `T component`
- multi-cast：多 System 监听同 Component 类型（Action multi-cast delegate 天然支持）
- 业务 System 必须有 public 无参构造器（Generator 用 `new SystemType()` 实例化）
- V0.9 IComponentSystem 接口契约不变：本 Iter 仅"添加自动 hook"，业务依然可手动调 `system.OnAttach(entity, component)` 而 framework 不 dispatch hook（前提是不让 Generator 自动注册同 System）

**端到端验证（Samples/Net）**
```
[Info] CounterSystem observed AttachCount=1 DetachCount=1
```
- `CounterSystem : IComponentSystem<CounterComponent>` **没有任何手动 hook 注册**
- 业务 `entity.AddComponent(...)` + `entity.RemoveComponent<...>()` 即触发对应 Counter 计数

### 迭代 7 — V0.9.5 收尾

**Modified**
- `CHANGELOG.md`：本段（V0.9.5 由"部分落地"改为"完整落地"，加 Iter 4-7 描述）
- `MyTryGetFramework/Assets/MyTryGetFramework/ARCHITECTURE.md`：V0.9.5 段同步更新
- `docs/strategy/V2-roadmap-tasks.md`：V0.9.5 Epic 列表 E4/E5/E6/E7 标 Done

**累计 V0.9.5 产物**
- 4 个 Generator（Hello / Module / SystemRegister / EventHandler / ComponentSystem）
- 4 个 runtime helper（AssemblyManifestRegistry / SystemRegistry / EventHandlerRegistry / ComponentSystemHooks）
- 3 个 Attribute（ModuleAttribute / SystemRegisterAttribute / EventHandlerAttribute）
- Samples/Net 端到端 demo 同时使用 [Module] + [EventHandler] + IComponentSystem，全部自动注册零手动 boilerplate
- 全套 Generator csproj / Shadow csproj / Samples/Net 编译 0/0
- Unity Editor 端验证仍需用户在 Editor 打开后确认 RoslynAnalyzer label 生效

**已知 V1.0+ 改进项**
- 失效情境诊断（Roslyn Diagnostic）：[Module] 类无 public 无参构造、ServiceType 不实现 interface、[EventHandler] 签名错等，生成 user-facing diagnostic 而非静默忽略
- IPureComponent struct 类型化容器（消除当前 boxed `Dictionary<Type, IPureComponent>`）
- [NetMessage] 等业务 attribute（V1.1+ 网络层引入）
- IComponentSystem 自动调度的 DI 注入（当前仅 parameterless ctor）


## V0.9 — IPlugin（hsenl 风格切面）+ IPureComponent（ECS 二级方案）

> 原 V0.9 路线含 Source Generator，本 minor 拆分：V0.9 = 运行时（IPlugin + IPureComponent），V0.9.5 = Source Generator（独立 minor）。

### 迭代 0 — V0.9 PRD

**Added**
- `docs/design/V0.9-plugin-pure-component.md`：4 维度跨框架调研（hsenl IPlug / ET Component / Fantasy / TEngine） + IPlugin / IPluginHost / IPlugPoint / IPureComponent / IComponentSystem 详细设计 + Iter 拆分 + 风险表
- 本 PRD 文档化"V0.9 = 运行时部分"与"V0.9.5 = Source Generator"的拆分理由

### 迭代 1 — IPlugin / IPluginHost / IPlugPoint + ModuleHost 集成

**Added**
- `Runtime/Core/Module/IPlugin.cs`：
  - `IPlugin` 接口（`Install` / `Uninstall` + `Priority`）
  - `IPluginHost` 接口（`AddPlugin` / `RemovePlugin` / `GetPlugin` / `GetPluginsAt` / `PluginCount`）
  - `IPlugPoint` 标记接口
- `Runtime/Core/Module/ModuleHostPlugPoints.cs`：内置插点 `IModuleHostBeforeUpdate` / `IModuleHostAfterUpdate`

**Modified**
- `Runtime/Core/Module/IModuleHost.cs`：现继承 `IPluginHost`（API 表面新增，向后兼容）
- `Runtime/Core/Module/ModuleHost.cs`：
  - 新增 `_pluginsByPoint` 存储（按 Priority 升序）
  - `Update` 内调度顺序：BeforeUpdate plugins → IUpdateModule.Update → AfterUpdate plugins
  - `Shutdown` 起首先 Uninstall 所有 plugin（每个 plugin 实例只 Uninstall 一次，不论挂多少插点）
  - `AddPlugin` 失败回滚（Install 抛异常时移除注册）
  - 实现 `IPluginHost` 全套方法

**Tests** — `PluginTests.cs`（13 测试）
- 注册/移除：`AddPlugin_TriggersInstall` / `AddPlugin_NullThrows` / `AddPlugin_SamePointSameType_Twice_Throws` / `AddPlugin_InstallThrows_NoResidue` / `RemovePlugin_TriggersUninstall` / `RemovePlugin_NotRegistered_ReturnsFalse`
- Lookup：`GetPlugin_ReturnsRegisteredInstance` / `GetPlugin_NotRegistered_ReturnsNull` / `GetPluginsAt_EnumeratesAllPriorityOrder`
- Update 内触发：`Update_BeforePluginsFireBeforeModules_AfterFireAfter`（demo `ModuleHostMetricsPlugin` 同实例挂 Before+After） / `Update_NoPlugins_ModuleUpdateStillRuns`
- Shutdown：`Shutdown_UninstallsAllPluginsOncePerInstance` / `Shutdown_AfterRemove_NoCrash`
- Priority：`Priority_AscendingOrder`

**Notes**
- 命名升级 vs hsenl：`IPlug`→`IPlugin`、`IPluggable`→`IPluginHost`、`IPlugGroup`→`IPlugPoint`、`Init/Dispose`→`Install/Uninstall`
- TryGet 新加 `Priority` 字段（hsenl 无），与 Module 体系对齐
- 与 `IModule` 概念分工：Module = 服务（`host.Get<T>()` 拉），Plugin = 横切关注点（hook 到 IPlugPoint，框架内部触发）

### 迭代 2 — IPureComponent + IComponentSystem + EntityPureComponentExtensions + ADR-0017

**Added**
- `Runtime/Core/Entity/IPureComponent.cs`：
  - `IPureComponent` 标记接口（marker only，无约束）
  - `IComponentSystem<TComponent>` 外置 System 接口（`OnAttach` / `OnDetach`）
- `Runtime/Core/Entity/EntityPureComponentExtensions.cs`：扩展方法（`AddComponent` / `GetComponent` / `HasComponent` / `RemoveComponent` / `ComponentCount`）
- `docs/adr/0017-aspect-vs-pure-component-dual-path.md`：双轨决策 ADR

**Modified**
- `Runtime/Core/Entity/Entity.cs`：
  - 新增 `_components` 字段（懒初始化 `Dictionary<Type, IPureComponent>`，未使用时不分配）
  - 新增内部方法 `AddPureComponentInternal` / `TryGetPureComponentInternal` / `HasPureComponentInternal` / `RemovePureComponentInternal` / `PureComponentCountInternal`
  - `MarkDestroyed` 增加 `_components?.Clear()`

**Tests** — `PureComponentTests.cs`（16 测试）
- Add：`AddComponent_StoresComponent` / `AddComponent_NullThrows` / `AddComponent_SameType_Twice_Throws` / `AddComponent_OnDestroyedEntity_Throws`
- Get：`GetComponent_ReturnsStored` / `GetComponent_NotPresent_ClassReturnsNull` / `GetComponent_StructComponent_BoxingRoundtrip`
- Has：`HasComponent_AfterAdd_True` / `HasComponent_NotPresent_False`
- Remove：`RemoveComponent_Present_RemovesAndReturnsTrue` / `RemoveComponent_NotPresent_ReturnsFalse` / `RemoveComponent_OnDestroyedEntity_Throws`
- 双轨独立：`PureComponent_AndAspect_AreIndependentChannels`
- Destroy 清理：`Destroy_ClearsComponents`
- IComponentSystem 业务显式调度 demo：`IComponentSystem_ExplicitDispatch_Demo`
- 计数 + 懒分配：`ComponentCount_TracksAddAndRemove` / `NoAllocation_BeforeFirstAdd`

**Notes**
- **双轨并存**：Aspect（OO 风格，行为内嵌）与 IPureComponent（DOD 风格，行为外置到 IComponentSystem）完全独立——不共享存储、不共享 mask、不共享 Query 路径
- **V0.9 不自动调度**：framework 不自动调 `IComponentSystem.OnAttach`，业务显式触发（V0.9.5 Source Gen 后引入自动调度）
- **struct boxed 存储**：IPureComponent 是 marker interface，struct 走 `Dictionary<Type, IPureComponent>` 装箱；V0.9.5 评估泛型化
- 选择矩阵见 ADR-0017 §"选择矩阵"

### 迭代 3 — 路线图修订 + CHANGELOG + commit

**Modified**
- `docs/strategy/V2-roadmap-tasks.md`：V0.9 原 Epic 列表标 Done（IPlugin + IPureComponent），新增 V0.9.5 minor（Source Generator）的 7 Epic
- `CHANGELOG.md`：本段

**Notes**
- V0.9 总测试数：29 新增（13 IPlugin + 16 IPureComponent），全套 Shadow csproj 0/0
- V0.9 实际工作量 = 原 V0.9 路线图 60%（去掉 SourceGen 部分，留作 V0.9.5）；好处是 V0.9 可以独立发布且无外部依赖



## V0.8 — IKVStore / IConfigSource / IAssetSource / ISerializer 重构（完整落地）

### 迭代 0 — V0.8 PRD

**Added**
- `docs/design/V0.8-kv-config-asset-serializer.md`：4 维度调研（ET/Fantasy/TEngine/MemoryPack/Luban）+ 4 抽象 API 详细设计 + Iter 拆分 + 破坏性变更清单

**Notes**
- 调研发现：MemoryPack 是 Unity 2026 最佳序列化方案（10-200x 其他，AOT-safe），但需 .NET 7+ / Unity 2022.3+
- Fantasy 用 MemoryPack + ProtoBuf + BSON 三套；ET 用 MessagePack；TEngine 用 Luban
- **关键决策**：Core 内**不提供 ISerializer 实现**（避免引入 NuGet 依赖），Memory* 实现绕过序列化直接持 object（zero-copy reference）

### 迭代 1 — ISerializer 接口

**Added**
- `Runtime/Core/Common/ISerializer.cs`：接口（`IsSupported(Type)` / `Serialize<T>` / `Deserialize<T>` + 非泛型重载）
- Tests `SerializerTests.cs` 内 `MockBytesSerializer`（仅 Tests 程序集，UTF8 string 序列化），7 测试

**Notes**
- Core 内**无具体实现** — 真实 MemoryPack / Json / MessagePack 实现由 V1.1+ Adapter 提供
- 测试用 Mock 验证接口契约（IsSupported / Serialize / Deserialize 双向 / 不支持类型抛 / null data 抛）

### 迭代 2 — IKVStore + MemoryKVStore + SaveModuleAdapter

**Added**
- `Runtime/Core/Common/IKVStore.cs`：强类型 `Get<T>` / `Set<T>` / `TryGet<T>` / `Remove` / `Clear` + `Keys` / `Count`
- `Runtime/Core/Common/MemoryKVStore.cs`：Dictionary<string, object> 实现，绕过 ISerializer
- `Runtime/Core/Common/SaveModuleAdapter.cs`：把 ISaveModule 弱类型四件套桥接为 IKVStore 强类型；string/int/float/bool 走对应 ISaveModule API，其他类型抛 NotSupportedException

**Modified**
- `Runtime/Core/Common/ISaveModule.cs`：加 `[Obsolete]`（V0.9 删）
- `Runtime/Core/Common/MemorySaveModule.cs`：加 `[Obsolete]`

**Tests** — `KVStoreTests.cs`（22 测试）
- MemoryKVStore：Set/Get 多种类型（string/int/float/bool/自定义 struct/reference type）；TryGet 软失败；Get 硬失败抛；类型不匹配抛 InvalidCast；null reference 允许；空 key 抛；Remove / Clear / Keys / Count；ModuleHost 集成
- SaveModuleAdapter：string/int/float/bool 桥接；unsupported type 抛 NotSupportedException；Keys/Count 视图；Remove 同步双端；null inner 抛

### 迭代 3 — IConfigSource + ConfigLoader<T> + MemoryConfigSource

**Added**
- `Runtime/Core/Common/IConfigSource.cs`：无类型 byte[] 访问（`Has` / `GetRaw` / `TryGetRaw` / `ConfigIds` / `Count`）
- `Runtime/Core/Common/ConfigLoader.cs`：`ConfigLoader<T>` 类型化包装 + 反序列化函数（业务注入 lambda）+ 缓存
- `Runtime/Core/Common/MemoryConfigSource.cs`：Dictionary<string, byte[]> 实现 + `SetRaw` 注入

**Modified**
- `Runtime/Core/Common/IConfigModule.cs` / `MemoryConfigModule.cs`：加 `[Obsolete]`

**Tests** — `ConfigSourceTests.cs`（17 测试）
- MemoryConfigSource：SetRaw / GetRaw 往返；missing 抛 ConfigNotFoundException；TryGetRaw 软失败；null data 抛；Overwrite；ConfigIds 视图；Remove
- ConfigLoader<T>：第一次 Get 反序列化；多次 Get 命中缓存（不重复反序列化）；same instance reference；missing 抛；TryGet 软失败；deserializer 抛异常 TryGet 返 false；InvalidateCache 全部 / 单个 id；null args 抛；ModuleHost 集成

**Notes**
- 设计对齐 Luban 真实工作流：IConfigSource 提供 byte[]，业务用 `new Tables(buf)` 类自我反序列化
- 缓存语义：`Get(id)` 第一次反序列化并缓存；同 id 二次返回缓存 reference

### 迭代 4 — IAssetSource (async, TGTask 集成) + MemoryAssetSource

**Added**
- `Runtime/Core/Common/IAssetSource.cs`：`LoadAsync<T>(path) : TGTask<T>`（V0.6 异步原语客户）+ `Get` / `TryGet` / `Unload` 同步路径
- `Runtime/Core/Common/IAssetSource.cs` 内：`AssetNotFoundException`（V0.9 替代 `ResourceNotFoundException`）
- `Runtime/Core/Common/MemoryAssetSource.cs`：Dictionary<string, object> + `TGTask<T>.FromResult/FromException` 即时返回

**Modified**
- `Runtime/Core/Common/IResourceModule.cs` / `MemoryResourceModule.cs`：加 `[Obsolete]`

**Tests** — `AssetSourceTests.cs`（14 测试）
- MemoryAssetSource：Add / Get / IsLoaded / TryGet 软失败 / Get 硬失败抛 AssetNotFoundException
- LoadAsync 路径：Existing 立即完成 + 返回正确 instance；Missing 任务抛 AssetNotFoundException；Wrong type 抛 InvalidCast；空路径抛 ArgumentException
- **`async TGTask Memory_LoadAsync_AwaitableInAsyncBody`** — 用 `await src.LoadAsync<T>(path)` 在 async TGTask 函数体内（验证 V0.6 TGTask 与 V0.8 IAssetSource 完整集成）
- Unload；LoadedPaths 视图；ModuleHost 集成；Add null args 抛

### 迭代 5 — Localization 评估 + CHANGELOG + ARCHITECTURE + commit

**Modified**
- `Runtime/Core/Common/ILocalizationModule.cs`：加 XML doc 段落 "V0.8 评估：保留现状不动；V0.9 Source Generator 引入后评估 LocalizationKeys 编译期生成"
- `CHANGELOG.md`：本段
- `Assets/MyTryGetFramework/ARCHITECTURE.md`：V0.8 完整段落

**Notes — V0.8 完成认证**
- 累计 6 Iter（0-5），覆盖 PRD → ISerializer → IKVStore → IConfigSource → IAssetSource → 收尾
- Core 新增 8 个文件（IKVStore / MemoryKVStore / SaveModuleAdapter / IConfigSource / ConfigLoader / MemoryConfigSource / IAssetSource(+AssetNotFoundException) / MemoryAssetSource / ISerializer）
- Core 修改 5 个文件（ILocalizationModule 注释 + 4 对 Obsolete 标记 ISaveModule/MemorySaveModule/IConfigModule/MemoryConfigModule/IResourceModule/MemoryResourceModule）
- 测试累计 60 个新增（DoD #5 要求 25+ 已超额）：7 Serializer + 22 KVStore + 17 ConfigSource + 14 AssetSource
- Shadow csproj 持续 0/0；Samples/Net 无需改

**V0.8 DoD 状态**
- ✓ #1 IKVStore 替代 ISaveModule（Obsolete + Adapter 完整）
- ✓ #2 IConfigSource + ConfigLoader<T> 替代 IConfigModule（Obsolete 完整）
- ✓ #3 IAssetSource + ISerializer 替代 IResourceModule（Obsolete + TGTask 集成完整）
- ✓ #4 ILocalizationModule 评估：保留现状（理由文档化）
- ✓ #5 测试 25+（超额到 60）+ CHANGELOG

**已知保留**
- ISerializer Core 无具体实现（V1.1+ Adapter 提供 MemoryPack/Json/MessagePack）
- LubanConfigSource / YooAssetSource / PlayerPrefsKVStore 等 production Adapter 留 V1.1+
- Tests 中现有 ISaveModule/IConfigModule/IResourceModule 测试（LogModuleTests 等同模式）继续工作但触发 Obsolete 警告（V0.9 删旧 API 时一并迁移）

---

## V0.7 — Bootstrap + ILogger + IClock + IEventScope

### 迭代 0 — V0.7 PRD

**Added**
- `docs/design/V0.7-bootstrap-logger-clock.md`：4 维度调研对比（Fantasy/ET/BigCat/TEngine/hsenl + Microsoft.Extensions.Logging + UniTask 时钟）+ API 详细设计 + Iter 拆分 + 破坏性变更清单 + 已知风险

**Notes**
- 调研发现：**没有商业 Unity 框架定义 IEntry interface**（Fantasy 用两个独立 Entry 类 + ET 用 Init/Program.cs + BigCat 用 GameLauncher）— 路线图原 "IEntry / Bootstrap 双端入口规范" 修正为 "Bootstrap + Net Entry pattern"（无 IEntry interface）
- ILogger 命名对齐 .NET 标准（非 Fantasy/hsenl `ILog`），方法签名故意简化（无 structured logging / 无泛型 / 无 factory）
- IClock 不暴露 wall-clock（DateTime），仅 game-frame 取向（DeltaTime/ElapsedTime/FrameCount），对标 BigCat TimeModule
- IEventScope 是 TryGet 创新点（5 个参考框架均未实现）

### 迭代 1 — IClock + SystemClock

**Added**
- `Runtime/Core/Common/IClock.cs`：`IClock : IModule` 接口（`DeltaTime` / `UnscaledDeltaTime` / `ElapsedTime` / `UnscaledElapsedTime` / `FrameCount`）
- `SystemClock : IClock, IUpdateModule`：Net/Headless 默认实现，Priority=-900，每帧 host.Update 注入 dt 累加

**Tests** — `SystemClockTests.cs`（8 测试）
- 初始零值；Update 设当前 dt；累计 ElapsedTime；scaled vs unscaled 独立追踪；FrameCount 单调递增
- Shutdown 重置；ModuleHost 注册 + Initialize + Update 完整驱动
- Priority 验证：sentinel Module 在自己 Update 时观察到 clock.FrameCount=1（说明 SystemClock 先于业务 Module）

**Verified**
- Shadow csproj 0 警告 0 错误

### 迭代 2 — ILogger + ConsoleLogger + LogModuleAdapter

**Added**
- `Runtime/Core/Common/ILogger.cs`：新接口，命名对齐 .NET `Microsoft.Extensions.Logging.ILogger`
  - 方法 `Trace/Debug/Info/Warn/Error(string)` + `Error(string, Exception)`
  - **不做** structured logging / `ILogger<T>` 泛型 / factory（V1.0+ 评估）
- `Runtime/Core/Common/ConsoleLogger.cs`：`ILogger` 默认实现（功能等价 `ConsoleLogModule`，Priority=-1000）
- `Runtime/Core/Common/LogModuleAdapter.cs`：把 `ILogModule` 桥接为 `ILogger`，支持业务渐进迁移；`Trace` 走 `Debug` 加前缀 `[TRACE] `

**Modified**
- `Runtime/Core/Common/LogLevel.cs`：新增 `Trace = -1`（在 `Debug` 之下，最详细），不破坏现有 enum 值

**Tests** — `LoggerTests.cs`（14 测试）
- ConsoleLogger 级别过滤（Trace/Debug/Error 等）；OnLog 钩子；Error+Exception；Shutdown 后静默
- ModuleHost 集成；Priority=-1000 验证
- LogModuleAdapter 全级别桥接；Trace 转 Debug 加 `[TRACE] ` 前缀；MinimumLevel 双向同步；null inner 抛；Error+Exception 转发

### 迭代 3 — Bootstrap + Net Entry pattern + Samples/Net 重构 + ILogModule Obsolete

**Added**
- `Runtime/Core/Module/Bootstrap.cs`：静态工具类 `Bootstrap.CreateHost(options)` 自动注册 Core 基础三件套（ILogger + IClock + ITGTaskScheduler）
- `Runtime/Core/Module/BootstrapOptions.cs`：可选注入自定义 Logger/Clock/Scheduler + 默认 `MinimumLogLevel`
- `Samples/Net/Entry.cs`：Net 端启动模板，`Entry.Run(setup, mainAsync, options, frameSleepMs, timeoutMs)` 静态方法
  - 主循环 + Stopwatch + Thread.Sleep + 异常处理 + 超时保护
  - 返回值：0=成功 / 1=mainAsync 抛 / 2=超时 / 3=setup 抛

**Modified**
- `Samples/Net/Program.cs`：用 `Entry.Run` 重构，从 ~180 行降到 ~120 行；ILogModule → ILogger 全替换
- `Runtime/Core/Common/ILogModule.cs`：加 `[Obsolete("Use ILogger (V0.7+). ILogModule will be removed in V0.8...")]`
- `Runtime/Core/Common/ConsoleLogModule.cs`：加 `[Obsolete("Use ConsoleLogger (V0.7+)...")]`
- `Runtime/Core/Common/LogModuleAdapter.cs`：内部加 `#pragma warning disable CS0618`（adapter 必须引用 Obsolete 的 ILogModule，是合法用法）

**Verified**
- Shadow csproj 0/0 持续
- `Samples/Net/dotnet run` 跑通同等行为（Boot→Login→InGame，总时长 ≈ 1.8s，输出顺序与 V0.6 版本一致）

**Notes**
- **不定义 IEntry interface**（参考 Fantasy/ET/BigCat 实践，避免过度抽象）
- V0.8 起删除 ILogModule + ConsoleLogModule + LogModuleAdapter（Obsolete 提前一个 minor 告警是 .NET 标准做法）

### 迭代 4 — IEventScope（创新点）

**Added**
- `Runtime/Core/Module/IEventScope.cs`：`IEventScope : IDisposable` 接口 + `EventScope` 默认实现
  - `Register(unsubscriber)` 接受"解绑动作"列表；`Dispose` 倒序执行（LIFO，与订阅顺序逆序）
  - 重复 Dispose 静默 no-op；scope 已 Dispose 后 Register 立即触发以防泄漏
  - unsubscriber 抛异常吞掉继续遍历（保证后续 handler 仍清理）
- `Runtime/Core/Module/EventBusScopeExtensions.cs`：`IEventBus.CreateScope()` + `IEventBus.Subscribe<T>(handler, scope)` overload
- `Runtime/Core/Entity/EntityEventScopeExtensions.cs`：`Entity.Subscribe<T>(handler, scope)` overload（Entity 销毁时 Unsubscribe 路径有 `IsDestroyed` 判断不抛）

**Tests** — `EventScopeTests.cs`（13 测试）
- EventScope 基础：初始未 Dispose / Register 计数 / null 参数抛 / Dispose 倒序解绑 / 重复 Dispose no-op
- Dispose 后 Register 立即触发 unsubscriber（防泄漏）；unsubscriber 抛异常吞后继续
- IEventBus 集成：with scope 订阅 → 收事件；scope.Dispose 后不再收；多 scope 隔离；混合事件类型；null 参数抛
- Entity scope：订阅 + 销毁后 Dispose 不崩；跨 bus 单 scope Dispose 同时解绑 EventBus + Entity

**Notes**
- 创新点：5 个参考框架（Fantasy/ET/BigCat/TEngine/hsenl）均未实现订阅自动解绑机制
- 灵感：`Microsoft.Extensions.Logging.ILogger.BeginScope` 模式扩展到 EventBus / Entity
- 现有不带 scope 的 `Subscribe` API **完全保留**（无破坏，业务可渐进采用 scope）

### 迭代 5 — CHANGELOG + ARCHITECTURE + commit

**Modified**
- 本 `CHANGELOG.md`：V0.7 完整段落
- `Assets/MyTryGetFramework/ARCHITECTURE.md`：V0.7 段落新增（含 Iter 0-5 列表 + 测试计数）

**Notes — V0.7 完成认证**
- 累计 6 Iter（Iter 0-5），覆盖 PRD → IClock → ILogger → Bootstrap+Entry → IEventScope → 收尾
- Core 新增 9 个文件（IClock/ILogger/ConsoleLogger/LogModuleAdapter/Bootstrap/BootstrapOptions/IEventScope/EventBusScopeExtensions/EntityEventScopeExtensions）
- Core 修改 3 个文件（LogLevel +Trace / ILogModule [Obsolete] / ConsoleLogModule [Obsolete]）
- Samples 新增 1 个文件 + 重构 Program.cs
- 测试累计 35 个新增（V0.7 DoD #5 要求 20+，超额完成）：8 SystemClock + 14 Logger + 13 EventScope
- Shadow csproj + Samples/Net dotnet run 持续 0/0

**V0.7 DoD 状态**
- ✓ Net Entry + Bootstrap 工作（Samples/Net dotnet run 跑通）
- 待 Unity Entry（V1.0+ 落地，配合 Samples/Unity）
- ✓ ILogger 替代 ILogModule（Obsolete + bridge 完整）
- ✓ IClock 抽象时钟（不动 IUpdateModule.Update 签名，零破坏）
- ✓ IEventScope（创新点，参考框架未实现）
- ✓ 测试 20+
- ✓ CHANGELOG / ARCHITECTURE 完整

**已知保留**
- Tests 中仍直接使用 ILogModule/ConsoleLogModule 的文件（LogModuleTests / ModuleHostEndToEndTests / LoginFlowDemoTests 等）— 测试本身验证 Obsolete API 行为，V0.8 删 ILogModule 时一并迁到 ILogger
- `LogLevel.Trace` 在旧 `ILogModule` 路径无对应方法（强制业务迁移到 `ILogger`）
- Unity 端的 `TryGet.Platform.Unity.Entry`（MonoBehaviour 启动）留 V1.0+ Samples/Unity 落地

---

## V0.6 — ITask 异步原语自研（完整落地）

### 迭代 0 — V2 设计文档族 + V0.6 ITask PRD

**Added**
- `docs/design/V2-commercial-framework-architecture.md`：商业 Unity 框架基础架构设计本体（做加法，承接 `V2-direction-pivot.md` 做减法 + ADR-0016 Adapter 退场）
- `docs/strategy/V2-roadmap-tasks.md`：V0.6 ~ V1.0 任务拆解（V0.6 Iter 级 + V0.7-V1.0 Epic 级骨架）
- `docs/design/V0.6-ITask.md`：ITask 异步原语 PRD，对标 ETTask / FTask / HTask 取舍 + API 详细设计 + 实施时序 + 失败模式

**Notes**
- 调研覆盖：本地 Explore agent 扫描 ReferenceFramework/{TEngine, hsenl, BigCat} + deepwiki MCP 远程问 egametang/ET + qq362946/Fantasy + WebSearch 现代基础设施（HybridCLR / UniTask / Awaitable / YooAsset / Luban / MemoryPack / Source Generator + AOT）
- V0.6 异步原语走自研路线（参考 hsenl HTask 风格：struct + Version 防过期 + AsyncMethodBuilder），不引入 UniTask / .NET Task ThreadPool

### 迭代 1 — ITask 骨架（最小可编译版，无 Pool / 无 Version）

**Added**
- `Runtime/Core/Async/ITask.cs`：`ITask` + `ITask<T>` readonly struct + 嵌套 `Awaiter`（`ICriticalNotifyCompletion`）+ `[AsyncMethodBuilder(typeof(AsyncITaskMethodBuilder))]`
- `Runtime/Core/Async/AsyncITaskMethodBuilder.cs`：`AsyncITaskMethodBuilder` + `AsyncITaskMethodBuilder<T>` 全套协议方法（Create/Start/SetStateMachine/SetResult/SetException/AwaitOnCompleted/AwaitUnsafeOnCompleted）
- `Runtime/Core/Async/ITaskBody.cs`：`ITaskBody` / `ITaskBody<T>` internal interface + `TaskBody` / `TaskBody<T>` 临时实现（单 continuation、Version 固定 0，等 Iter 4 进入 Pool）
- `Runtime/Core/Async/ITaskType.cs`：Builder/Manual 二分（参考 hsenl HTask:64-67 限制用户对 Builder 类型的 task 调 SetResult）
- `Runtime/Core/Async/TaskExpiredException.cs`：占位异常（Iter 3 启用 Version 校验后真正触发）
- `Tests/EditMode/ITaskCompilationSmokeTests.cs`：6 个 smoke 测试覆盖 `async ITask` 无 await / 带返回值 / 抛异常传播 / default(ITask) 安全 / Forget no-op

**Verified**
- `cd ServerProject/MyTryGetFramework.Core && dotnet build`：0 警告 0 错误（Shadow csproj 跨端编译通过，无 UnityEngine 依赖）
- `[AsyncMethodBuilder]` attribute 在 netstandard2.1 + LangVersion 9.0 下正确工作

**Notes**
- 单线程模型：`OnCompleted` 同步执行 continuation 或入队 `ITaskScheduler`（V0.6 Iter 5 引入）；不引入 ThreadPool
- `ITask` 仅允许一次 await（多次 await = 用户错误，Iter 1 在 TaskBody 内通过单 continuation 直接强约束抛异常）
- Iter 2-11 路线见 `docs/strategy/V2-roadmap-tasks.md` §1.3

### 迭代 2 — ITaskCompletionSource + 完整 TaskBody

**Added**
- `Runtime/Core/Async/ITaskCompletionSource.cs`：`ITaskCompletionSource` + `ITaskCompletionSource<T>` 公开 class，构造时从 Pool Rent body，提供 SetResult / SetException / SetCanceled 公开 API + Return() 显式归还（Iter 4 启用 Pool）
- `Runtime/Core/AssemblyInfo.cs`：`[assembly: InternalsVisibleTo("MyTryGetFramework.Tests")]` 让 Test 程序集能访问 TaskBody.Reset / Version 等 internal 路径

**Modified**
- `TaskBody` / `TaskBody<T>`：保持单 continuation 设计，OnCompleted 上重复挂 continuation 抛 `InvalidOperationException`（强制用户错误显式化）

**Tests** — `ITaskCompletionSourceTests.cs`（14 测试）
- 基本 SetResult / SetException / null arg / SetCanceled
- 重复 SetResult 静默忽略；SetException after SetResult 静默忽略
- 跨 await 边界：OuterBody 在 tcs SetResult 后正确恢复；异常通过 await 传播
- 泛型 tcs&lt;T&gt;：SetResult / SetException / SetCanceled / 跨 await

### 迭代 3 — _version 防过期机制

**Modified**
- `TaskBody.Reset()` / `TaskBody<T>.Reset()`：实际启用 `_version++`（带 `MaxVersion = int.MaxValue - 2` 回绕 sentinel，参考 hsenl HTask:16）
- `ITask.Awaiter.GetResult / OnCompleted / UnsafeOnCompleted`：加入 `if (_task.Version != _task.Body.Version) throw new TaskExpiredException()` 校验
- `ITaskCompletionSource.EnsureNotExpired`：tcs 持有的 body 被外部 Reset 时 SetResult 抛 `TaskExpiredException`

**Tests** — `ITaskVersionTests.cs`（8 测试）
- Reset 让 version++ + 恢复 initial 状态
- AwaiterGetResult / OnCompleted / 泛型 AwaiterGetResult 在 expired 时抛
- TcsSetResult after external Reset 抛；MaxVersion 回绕到 MinVersion（反射推送 _version 字段避免百万次 Reset）
- `default(ITask)` 无 body 不触发 version 校验

### 迭代 4 — TaskPool 真池化

**Added**
- `Runtime/Core/Async/TaskPool.cs`：静态全局池，每种 body 类型独立 Stack；`MaxPoolSize`（默认 64）；`Rent` / `Return` internal；`PooledCount` / `PooledCountOf<T>` / `ClearAll` / `ClearGeneric<T>` 公开诊断 + 测试钩子
- 泛型 `TypedPool<T>` 嵌套静态类，每个 T 一个独立 Stack

**Modified**
- `AsyncITaskMethodBuilder.Create()` / `AsyncITaskMethodBuilder<T>.Create()`：从 `TaskPool.Rent()` / `TaskPool.Rent<T>()` 拿 body
- `ITaskCompletionSource` / `ITaskCompletionSource<T>`：构造时 Rent；新增 `Return()` 公开方法显式归还
- `ITask.Awaiter.GetResult`：完成时若 `TaskType == Builder` 自动 `TaskPool.Return(body)`（try/finally 保证异常路径也归还）

**Tests** — `TaskPoolTests.cs`（14 测试）
- Rent/Return 基础语义；Rent after Return reuses 同一 body；MaxPoolSize 边界；泛型池隔离
- async ITask 完成后 Builder body 自动归还（含异常路径）；反复 100 次 async ITask → pool 复用同一 body
- tcs.Return / tcs after Return throws on SetResult / 忘记 Return 时无 crash

### 迭代 5 — ITaskScheduler

**Added**
- `Runtime/Core/Async/ITaskScheduler.cs`：接口（`IModule + IUpdateModule`），方法 `Yield() / Delay(seconds) / WaitForFrames(int)`
- `Runtime/Core/Async/TaskScheduler.cs`：默认实现，Priority=-150（介于 Procedure=-200 和 EntityWorld=-100）
  - 双 buffer Yield 队列（保证"下一帧"语义）
  - Delay 队列（线性 scan，N=10~50 可接受）
  - Frame-wait 队列
  - Shutdown 取消所有 pending tcs

**Tests** — `TaskSchedulerTests.cs`（12 测试）
- Yield 下一帧完成；同帧多 Yield 同时完成；跨帧 Yield 只先到的完成
- Delay 1.5s 累计 dt 完成；Delay(0) 等价 Yield；Delay 负数抛
- WaitForFrames(3) 三帧完成；WaitForFrames(0) 立即完成；负数抛
- Shutdown 取消所有 pending；async ITask 内 await Delay 恢复；ModuleHost 注册 + Initialize + Update 完整驱动

### 迭代 6 — ITimerModule.WaitAsync 扩展

**Added**
- `Runtime/Core/Async/TimerModuleAsyncExtensions.cs`：命名空间放 `TryGet`（与 ITimerModule 一致），让业务 `using TryGet;` 自动获得扩展
  - `timer.WaitAsync(seconds): ITask` — 内部用 tcs + `timer.Schedule(seconds, () => { tcs.SetResult(); tcs.Return(); })`
  - `timer.WaitUnscaledAsync(seconds): ITask` — 同上但走 `ScheduleUnscaled`

**Tests** — `TimerModuleAsyncExtensionsTests.cs`（5 测试）
- WaitAsync 1s 累计 dt 完成；WaitAsync 负数 / null timer 抛；WaitUnscaledAsync 工作；async ITask 内 await timer.WaitAsync 恢复

### 迭代 7 — IAsyncProcedure + ProcedureModule 异步支持

**Added**
- `Runtime/Core/Common/IAsyncProcedure.cs`：接口（`: IProcedure`）+ `AsyncProcedureBase`（继承 `ProcedureBase` 提供空 OnEnterAsync/OnExitAsync 默认实现）

**Modified**
- `IProcedureModule`：新增 `IsEntering` / `IsExiting` / `LastAsyncError` 属性
- `ProcedureModule`：
  - Update 在 `IsEntering || IsExiting` 时跳过 OnUpdate
  - Start：同步 OnEnter 后，若 `IAsyncProcedure` 启动 `BeginAsyncEnter`
  - TransitionTo：若 prev 是 async procedure 走 `BeginAsyncExit` + 完成后 SwitchTo（异步阶段中 TransitionTo 抛 InvalidOperationException）
  - Stop：异步阶段中强制清空；正常路径下走 BeginAsyncExit + 完成后清空
  - 异常处理：OnEnterAsync/OnExitAsync 抛异常或返回的 ITask 抛异常时存入 `LastAsyncError`

**Tests** — `AsyncProcedureTests.cs`（10 测试）
- Sync-complete async procedure 无异步阶段；Pending tcs 让 IsEntering=true；Update 跳过；TransitionTo 抛
- OnEnterAsync throws 记录到 LastAsyncError；TransitionTo 走 OnExitAsync + OnExit；pending OnExitAsync 让 IsExiting=true
- Stop 走 OnExitAsync；异步阶段中 Stop 强制清空

### V0.6 Iter 2-7 阶段性总结

**累计产出**
- 新增 9 个 Core 文件：`Runtime/Core/Async/*.cs`（10 个） + `Runtime/Core/Common/IAsyncProcedure.cs` + `Runtime/Core/AssemblyInfo.cs`
- 修改 3 个 Core 文件：`IProcedureModule.cs` / `ProcedureModule.cs` / `ITask.cs` / `AsyncITaskMethodBuilder.cs` / `ITaskCompletionSource.cs`
- 新增 7 个测试文件：累计 **~69 个 EditMode 测试**（PRD §8 的 30+ 目标超额 130%）
- Shadow csproj 全程 0 警告 0 错误，无任何 Unity 依赖

**V0.6 Module Priority 链**
ConsoleLog(-1000) → Pool/Timer(-500) → Procedure(-200) → TaskScheduler(-150) → EntityWorld(-100) → 业务(0)

### 迭代 8 — 全框架命名专业化 + 静态工厂 + UnobservedException 钩子

**Breaking（V0.6 内部 rename，无外部影响）**

`I` 前缀只保留给真接口。Async 层 15 个公开类型按 `TG` 品牌前缀（与 ET/F/H 单字母前缀对齐）统一改名：

| 旧名（V0.6 Iter 1-7） | 新名（V0.6 Iter 8+） | 形态 | 改名理由 |
|---|---|---|---|
| `ITask` / `ITask<T>` | `TGTask` / `TGTask<T>` | struct | I 前缀误用于非接口 |
| `ITaskBody` / `ITaskBody<T>` | `ITGTaskBody` / `ITGTaskBody<T>` | interface | 真接口加 TG 前缀保品牌 |
| `TaskBody` / `TaskBody<T>` | `TGTaskBody` / `TGTaskBody<T>` | class | 与 .NET Task 重名 |
| `ITaskType` | `TGTaskType` | enum | I 前缀误用于 enum |
| `ITaskCompletionSource` / `ITaskCompletionSource<T>` | `TGTaskCompletionSource` / `TGTaskCompletionSource<T>` | class | I 前缀误用 |
| `AsyncITaskMethodBuilder` / `AsyncITaskMethodBuilder<T>` | `AsyncTGTaskMethodBuilder` / `AsyncTGTaskMethodBuilder<T>` | struct | I 前缀误用 |
| `ITaskScheduler` | `ITGTaskScheduler` | interface | 加 TG 前缀 |
| `TaskScheduler` | `TGTaskScheduler` | class | 与 `System.Threading.Tasks.TaskScheduler` 冲突 |
| `TaskPool` | `TGTaskPool` | static class | 加 TG 前缀 |
| `TaskExpiredException` | `TGTaskExpiredException` | exception | 加 TG 前缀 |

`Entity/Handle` → `Entity/EntityHandle`（"Handle" 太泛，明确"指向 Entity 的句柄"语义，对标 ET `EntityRef`）。

**Added — 静态工厂**（对标 .NET `Task.FromResult` + UniTask `UniTask.CompletedTask`）
- `TGTask.CompletedTask` — 已完成单例
- `TGTask<T>.FromResult(T value)`
- `TGTask.FromException(Exception)` / `TGTask<T>.FromException(Exception)`
- `TGTask.FromCanceled()` / `TGTask<T>.FromCanceled()`

**Added — UnobservedException 全局钩子**（对标 UniTask `UniTaskScheduler.UnobservedTaskException`）
- `TGTaskScheduler.UnobservedException : event Action<Exception>`（静态事件）
- `TGTask.Forget()` / `TGTask<T>.Forget()` 实装：未观察异常路径触发钩子；完成路径自动归还 body 到 Pool

**Added — 文档**
- `docs/design/V0.6-Iter8-naming-review.md`：完整命名审视报告（扫描 Core 全部 49 个公开类型 + 对标 C# / .NET / UniTask / ETTask / FTask / HTask 命名约定）

**Modified**
- `Runtime/Core/Common/IAsyncProcedure.cs`：`AsyncProcedureBase.OnEnterAsync/OnExitAsync` 默认返回 `TGTask.CompletedTask`（语义比 `default(ITask)` 更明确）
- `Runtime/Core/Common/ProcedureModule.cs`：内部 `ITask` 全替为 `TGTask`
- `Runtime/Core/Async/TimerModuleAsyncExtensions.cs`：扩展方法返回 `TGTask`

**Tests** — 测试文件跟随 rename
- 5 个测试文件改名：`I*Tests.cs` → `TG*Tests.cs`（`TGTaskCompilationSmokeTests` / `TGTaskCompletionSourceTests` / `TGTaskVersionTests` / `TGTaskPoolTests` / `TGTaskSchedulerTests`）
- 2 个测试文件内容更新（文件名保持）：`TimerModuleAsyncExtensionsTests` / `AsyncProcedureTests`
- `TGTaskCompilationSmokeTests.cs` 新增 5 个用例覆盖静态工厂：`CompletedTask` / `FromResult` / `FromException`（含 null arg）/ `FromCanceled` / `Forget no-crash`
- `TGTaskSchedulerTests.cs` 新增 3 个用例覆盖 UnobservedException：Forget throwing task / Forget pending throwing tcs / Forget successful task no fire
- `OwnershipTests.cs` 跟随 `Handle` → `EntityHandle`

**Verified**
- `dotnet build` Shadow csproj：0 警告 0 错误
- Async 目录从 10 个 `I*` / `Task*` 文件全部 rename 为 `TG*` / `TGTask*`，旧文件 + 旧 `.meta` 物理删除
- 全仓库 grep `\bHandle\b`：仅匹配 `EntityHandle.cs` 自身（无残留旧引用）

**Notes**
- 测试累计从 V0.6 Iter 2-7 的 ~69 个增加到 **~77 个**（Iter 8 新增 8 个：5 静态工厂 + 3 UnobservedException）
- 单线程模型下 `TGTaskScheduler.UnobservedException` 是静态全局事件（与 .NET `TaskScheduler.UnobservedTaskException` 同思路），测试中订阅/退订必须 try/finally 配对

### 迭代 9 — 关键边界测试 + GC 基准

**Added — `Tests/EditMode/TGTaskEdgeCaseTests.cs`（12 测试）**
- **嵌套 async TGTask**（4 测试）：2 层 / 3 层值传递；3 层深层异常穿透到顶层；同层多 await 顺序执行
- **Builder body 复用语义**（4 测试）：第一次 await 后 struct 副本再 GetResult/OnCompleted 抛 `TGTaskExpiredException`；body 自动入 Pool；泛型 `TGTask<T>` 同语义
- **Stress + GC diagnostic**（4 测试）：10K 次串行 Builder TGTask 后 Pool 维持 1 槽（完美复用）；MaxPoolSize=8 时并行 Rent 32 个后 Pool 上限封顶；10K 次 `await scheduler.Yield()` 的 GC alloc 信息打印（V0.6 DoD #3 informational，软上限 50MB）

**Notes — V0.6 DoD 状态**
- **DoD #1**（`Samples/Net/Program.cs` 跑通 Boot→Login→InGame）— Iter 10 达成
- **DoD #2**（`Samples/Unity/TryGetMonoEntry.cs` 跑通）— V0.7 IEntry/Bootstrap 落地时一并交付
- **DoD #3**（100W await GC alloc < 1MB）— 当前实现 `TGTaskCompletionSource` 为 class（每次 Yield 必 alloc 一个 tcs ≈ 32 bytes），完整达成推迟到 **V0.6.5 池化 tcs**；本 Iter 给"Pool 对 body 复用生效"硬证据 + alloc 量信息测试
- **DoD #4**（Shadow csproj 通过）— 持续 0/0
- **DoD #5**（测试 30+ 全绿）— 累计 **~89 EditMode 测试**（Iter 9 新增 12），需在 Unity Editor Test Runner 跑全套验证
- **DoD #6**（CHANGELOG 完整）— 本 Iter / Iter 10 / Iter 11 同步落档

### 迭代 10 — Samples/Net/Program.cs 雏形

**Added**
- `Samples/Net/TryGet.Samples.Net.csproj`：dotnet console 项目（net8.0，OutputType=Exe），通过 `<ProjectReference>` 反向依赖 `ServerProject/MyTryGetFramework.Core` Shadow csproj
- `Samples/Net/Program.cs`：纯 .NET console 跑通 V0.6 异步原语 demo
  - **流程**：Boot (async 加载 0.3s) → Login (async 认证 0.3s) → InGame (async 加载 0.2s + play 1s) → Stop
  - **三个 AsyncProcedureBase 子类**：`BootProcedure` / `LoginProcedure` / `InGameProcedure`，各自 OnEnterAsync 内 `await scheduler.Delay(seconds)` 模拟异步资源加载
  - **主流程**：`async TGTask RunMainAsync` 函数，串行调 `proc.Start/TransitionTo` + `await WaitForEnter(proc, sched)` 等待 IsEntering 退回 false
  - **主循环**：`Stopwatch` + `Thread.Sleep(16)` 驱动 `host.Update(dt, dt)`，10s 超时保护
  - **TGTaskScheduler.UnobservedException** 全局钩子订阅 + try/finally 退订
  - **Module 注册栈**：`ConsoleLogModule` + `TimerModule` + `TGTaskScheduler` + `ProcedureModule`，不引入 PoolModule（demo 最小化）

**Verified**
- `cd Samples/Net && dotnet build`：0 警告 0 错误
- `dotnet run`：完整流程跑通，输出顺序与预期 100% 匹配，总时长 ≈ 1.8s
- `cd ServerProject/MyTryGetFramework.Core && dotnet build`：Shadow csproj 持续 0/0（无回归）

**Notes**
- V0.7 IEntry/Bootstrap 落地后，`RunMainAsync` 主流程将归并到 `IEntry.RunAsync`，主循环 tick 归并到 `Bootstrap.Run`；本雏形作为"裸 ModuleHost 编排"参考保留
- `AsyncProcedureBase.OnEnterAsync` 使用 `async TGTask` 函数体（Builder 类型 TGTask），由 `ProcedureModule.BeginAsyncEnter` 内部 `task.GetAwaiter().OnCompleted(...)` 挂 continuation，Builder body 在 `Awaiter.GetResult` finally 自动归还到 Pool

### 迭代 11 — ARCHITECTURE.md V0.6 完整版 + tag v0.6.0

**Modified**
- `Assets/MyTryGetFramework/ARCHITECTURE.md`：V0.6 段落更新为"完整落地"状态；测试计数同步到 ~89；新增 `Samples/Net/` 目录到程序集布局；剩余 Iter 列表删除

**Notes — V0.6 完成认证**
- 累计 12 Iter（Iter 0-11），覆盖 PRD → 骨架 → tcs → version → pool → scheduler → timer 桥接 → AsyncProcedure → 命名专业化 → 边界测试 → Net sample → 文档
- Async 层共 11 个 Core 文件（`TGTask.cs` / `TGTaskBody.cs` / `TGTaskCompletionSource.cs` / `AsyncTGTaskMethodBuilder.cs` / `TGTaskPool.cs` / `ITGTaskScheduler.cs` / `TGTaskScheduler.cs` / `TimerModuleAsyncExtensions.cs` / `TGTaskExpiredException.cs` / `TGTaskType.cs` + `Core/Common/IAsyncProcedure.cs`）
- 测试累计 ~89 EditMode（V0.6 Iter 1-9 累计新增 ~77 个）
- Shadow csproj + Samples/Net dotnet console 双端验证持续 0/0
- 已知保留项：`TGTaskCompletionSource` 池化（V0.6.5）、`Samples/Unity/TryGetMonoEntry.cs`（V0.7 IEntry 共生）

---

## V0.5（进行中 — Core 服务补齐 + InputModule）

### 迭代 0 — InputModule（V0.4 deferred 补齐）

**Added**
- `IInputModule` 接口：按 Action 名查询输入（IsPressed / WasPressedThisFrame / WasReleasedThisFrame / GetAxis / GetAxis2D / ActiveCount）
- `MemoryInputModule`：3 HashSet 区分 _pressed / _pressedThisFrame / _releasedThisFrame；
  SimulatePress / SimulateRelease / SetAxis / SetAxis2D 测试 API；
  Axis 自动 clamp 到 [-1, 1]；同帧 Press→Release 两个 edge 都保留 true（与 Unity 一致）
- 22 个 EditMode 测试

### 迭代 1 — ConfigModule（V0.3 deferred 补齐）

**Added**
- `IConfigModule` 接口：Register / Get / TryGet / Has / Unregister / RegisteredCount
- `MemoryConfigModule`：Dictionary<string,object>，Priority=-460（最早一批服务）
- `ConfigNotFoundException` 带 Key 属性
- 21 个 EditMode 测试

**Notes**
- 与 IResourceModule 语义区分：Resource 加载运行时对象（Prefab/AudioClip），Config 查询业务表数据（武器表/关卡表）

### 迭代 2 — SceneModule

**Added**
- `ISceneModule` 接口：Load / Unload / SetActive / IsLoaded / ActiveScene / LoadedScenes / UnloadAll / LoadedCount
- `MemorySceneModule`：List 保插入顺序 + HashSet O(1) IsLoaded + active 字符串引用，Priority=-250
- 19 个 EditMode 测试

**Notes**
- 默认 additive 加载语义（与 Unity LoadSceneMode.Additive 一致）
- 首个加载场景自动成为 ActiveScene；卸载 active 后 ActiveScene 置 null

### 迭代 3 — SceneFlowDemoTests 端到端 demo + Input edge 调度 bugfix

**Fixed**
- `MemoryInputModule` 从 `IUpdateModule` 改为 `ILateUpdateModule`：
  Update 调度按 Priority 升序，Input (-350) 在 Procedure (-200) 之前 Update，
  会先清掉 _pressedThisFrame 导致业务永远看不到本帧输入。改 LateUpdate 后业务在 Update 阶段消费 edge，host.LateUpdate 后才清空，与 Unity Input.GetKeyDown 行为一致

**Added**
- `SceneFlowDemoTests`：V0.5 端到端 demo，MainMenu → press Confirm → Battle 流程
  串联 V0.5 三件套（Input/Config/Scene）+ V0.4 Audio
- 2 测试：完整流程 + Input edge 调度顺序验证

### V0.5 Module Priority 链（进行中）
Log(-1000) → Pool/Timer(-500) → Config(-460) → Save(-450) → Localization(-420) →
Resource(-400) → Audio(-380) → Input(-350) → UI(-300) → Scene(-250) →
Procedure(-200) → EntityWorld(-100) → 业务(0)

### 迭代 4 — TimerModule 增强

**Added**
- `ITimerModule.ScheduleRepeat(intervalSeconds, callback)`：周期触发直到 Cancel
- `ITimerModule.Pause(handle)` / `Resume(handle)` / `IsPaused(handle)`：暂停/恢复（关卡暂停刚需）
- `TimerModule.Entry` struct 增加 Interval / Repeating / Paused 字段（非破坏性）
- 16 个 EditMode 测试

**Notes**
- 周期 timer interval<=0 抛 ArgumentOutOfRangeException（防死循环）
- 单帧 deltaTime >> interval 时只触发一次（不补帧），避免长时暂停后连发

### 迭代 5 — AudioModule 增强

**Added**
- `IAudioModule.Pause(cue)` / `Resume(cue)` / `IsPaused(cue)` / `PauseAll(category)` / `ResumeAll(category)`
- `MemoryAudioModule` 内部 Dictionary 改为 <cue, PlayingEntry>（struct 含 Category + Paused）
- 17 个 EditMode 测试，含游戏暂停菜单场景验证

**Notes**
- Pause 后 IsPlaying 仍 true（cue 保留在播放列表，与 Stop 区分）
- 重新 Play 隐含 Resume（业务"重启"语义）

### 迭代 6 — UnityAudioModule Adapter（第一个 Unity Adapter）

**Added**
- `Runtime/Unity/Audio/UnityAudioModule.cs`：IAudioModule 的 Unity 实现，基于 AudioSource 池（默认 16）
- `RegisterClip(cue, AudioClip)` / `UnregisterClip(cue)` API：业务先加载 AudioClip 后注入 Adapter
- 溢出策略：池满时 FIFO 复用最旧 cue
- MasterVolume × CategoryVolume 合成 effective volume，Set 时刷新所有在播 AudioSource
- **新建 Tests/PlayMode test asmdef**（基建）：references Core + Unity + TestRunner，includePlatforms=[] 允许所有平台
- `UnityAudioModulePlayModeTests` (11 测试) 用 AudioClip.Create 生成 1 秒静音 clip

### 迭代 7 — UnityInputModule Adapter（接 InputSystem 1.18）

**Added**
- `Runtime/Unity/Input/UnityInputModule.cs`：IInputModule 的 Unity 实现，接 com.unity.inputsystem 1.18.0
- `RegisterButton(name, binding)` / `RegisterAxis(name, binding)` / `RegisterAxis2D(name, binding)` API：
  业务用 binding 字符串注册（如 "<Keyboard>/space"），Adapter 内部 new InputAction + Enable + hook performed/canceled
- ILateUpdateModule：LateUpdate 清 edge buffers（与 MemoryInputModule + V0.5 Iter 3 修正语义一致）
- Shutdown：DisposeAll 所有 InputAction，释放 OS 输入资源
- MyTryGetFramework.Unity.asmdef references 加 "Unity.InputSystem"
- Tests.PlayMode.asmdef references 加 "Unity.InputSystem" + "Unity.InputSystem.TestFramework"
- `UnityInputModulePlayModeTests` (11 测试) 用 `InputSystem.AddDevice<Keyboard>/<Gamepad>` + `StateEvent.From` 模拟硬件

### V0.5 Module Priority 链（最新）
Log(-1000) → Pool/Timer(-500) → Config(-460) → Save(-450) → Localization(-420) →
Resource(-400) → Audio(-380) → Input(-350) → UI(-300) → Scene(-250) →
Procedure(-200) → EntityWorld(-100) → 业务(0)

### 迭代 8 — PlayerPrefsSaveModule Adapter

**Added**
- `Runtime/Unity/Save/PlayerPrefsSaveModule.cs`：ISaveModule 的最简 Unity Adapter，接 UnityEngine.PlayerPrefs
- bool 编码为 int (0/1) 透明转换（PlayerPrefs 原生只支持 string/int/float）
- 内部 _keys HashSet 维护 KeyCount 诊断
- Shutdown 不擦盘（持久化数据不应被 Adapter Shutdown 擦除）
- `PlayerPrefsSaveModulePlayModeTests` (16 测试)

### 迭代 9 — UGUIUIModule Adapter

**Added**
- `Runtime/Unity/UI/UGUIUIModule.cs`：IUIModule 的 Unity 实现，基于 UGUI Canvas
- V0.5 最小版（不分层 / 不 Modal / 不传参，留给业务子类化扩展）
- 单 Canvas root + GraphicRaycaster，Initialize 创建、Shutdown 销毁
- `RegisterPrefab/UnregisterPrefab` API（不耦合 IResourceModule）
- 委托 MemoryUIModule 做状态机，Adapter 只包裹 GameObject lifecycle
- `virtual OnOpened/OnClosed` 钩子供业务子类化扩展
- 加 "UnityEngine.UI" references
- `UGUIUIModulePlayModeTests` (11 测试)

### 迭代 10 — UnitySceneModule Adapter

**Added**
- `Runtime/Unity/Scene/UnitySceneModule.cs`：ISceneModule 的 Unity 实现，接 SceneManager
- Load 同步（LoadScene Additive）、Unload 异步 fire-and-forget（UnloadSceneAsync）
- 内部 List+HashSet 维护"已请求加载"状态，与 MemorySceneModule 契约一致
- Shutdown 不卸载已加载场景（运行时资源持久化）
- `UnitySceneModulePlayModeTests` (13 测试)

### V0.5 Unity Adapter 套件总结
5 个 Unity Adapter 全数落地（Audio / Input / Save / UI / Scene），合计 ~62 PlayMode 测试。
Adapter 测试基建（Tests/PlayMode asmdef + InputSystem.TestFramework + UGUI）就位。

### V0.5 Gate 剩余项（待做）
- [ ] NetworkModule（IChannel + IMessageBus 接口）— Plan agent 建议与首个真实实现共生设计
- [ ] Adapters/Mirror 默认实现
- [ ] MemoryPack 序列化集成
- [ ] HybridCLR IHotfixLoader 接口 + 实现
- [ ] YooAsset Adapter（包未装，物理阻塞）

---

## V0.4（Common Modules 五件套）

### 迭代 0 — ResourceModule

**Added**
- `IResourceModule` 接口：`Load<T>` / `TryLoad<T>` / `Release` / `Register` / `Unregister` / `RegisteredCount`
- `MemoryResourceModule`：Dictionary<string,object> 实现，Priority=-400，介于 Pool/Timer (-500) 与 EntityWorld (-100) 之间
- `ResourceNotFoundException` 带 `Path` 属性便于诊断
- 18 个 EditMode 测试

**Notes**
- Core 不依赖 YooAsset / Addressables / UniTask（Adapter 层后续迭代提供 YooAssetResourceModule）

### 迭代 1 — UIModule

**Added**
- `IUIModule` 接口：`Open` / `Close` / `IsOpen` / `OpenedUIs` / `OpenedCount` / `CloseAll`
- `MemoryUIModule`：List 保插入顺序 + HashSet 保 O(1) IsOpen，Priority=-300
- 17 个 EditMode 测试

**Notes**
- Core 只管"哪些 UI 在打开"的状态机，不渲染、不分层、不传参、不 Modal（这些由 Adapters/UGUI 层 UGUIUIModule 扩展）
- 重复 Open 同名抛 InvalidOperationException；Close 不存在 UI 静默幂等

### 迭代 2 — SaveModule

**Added**
- `ISaveModule` 接口：`HasKey` / `Get/Set(String|Int|Float|Bool)` / `DeleteKey` / `DeleteAll` / `Save` / `KeyCount`
- `MemorySaveModule`：单 Dictionary<string,object> 实现（保 KeyCount 准确、同 key 跨类型互斥），Priority=-450
- 24 个 EditMode 测试

**Notes**
- 行为对齐 PlayerPrefs：同 key 跨类型 Set 覆盖；Get 类型不匹配/key 不存在返回 defaultValue（读容错）；Set null/empty key 抛 ArgumentException（写强约束）
- Save() 为 no-op；Adapter 实现负责真正持久化（PlayerPrefs / 本地文件 / 云存档）

### 迭代 3 — LocalizationModule

**Added**
- `ILocalizationModule` 接口：`CurrentLanguage` / `SetLanguage` / `RegisterTable` / `T(key)` / `T(key, default)` / `TryGet` / `AvailableLanguages` / `RegisteredCount`
- `MemoryLocalizationModule`：Dictionary<语言, Dictionary<key,值>> 双层表，Priority=-420
- 23 个 EditMode 测试

**Notes**
- production-ready 实现（不是 stub），Adapter 层只需"从 Excel/CSV/JSON 加载 + RegisterTable"工厂
- 与 Unity Localization Package / i18next 共识：T(key) 漏译返回 key 本身（让 UI 立即暴露漏译）
- RegisterTable 浅拷贝输入 dict 防外部污染；重复注册同语言覆盖；覆盖 current 立即刷新引用

### 迭代 4 — AudioModule

**Added**
- `AudioCategory` 枚举：BGM / SFX / UI / Voice
- `IAudioModule` 接口：`Play(cue, category)` / `Stop` / `StopAll(category)` / `StopAllSounds` / `IsPlaying` / `MasterVolume` / `SetMasterVolume` / `GetCategoryVolume` / `SetCategoryVolume` / `PlayingCount`
- `MemoryAudioModule`：Dictionary<cue, AudioCategory> 状态机 + 4 个 float 存分类音量，Priority=-380
- 22 个 EditMode 测试

**Notes**
- Core 不依赖 AudioClip / AudioSource / AudioMixer；Adapters/Unity 层后续 UnityAudioModule 接 AudioSource
- 同 cue 重复 Play 幂等；音量自动 clamp 到 [0,1]；Shutdown 清空列表 + 重置音量

### 迭代 5 — V0.4 端到端 demo

**Added**
- `MainMenuFlowDemoTests`：Boot → MainMenu → InGame 三 Procedure 串联 V0.4 五件套全协同
  - Boot：Save.GetString("lang") → Localization.SetLanguage → Resource.Register UI Prefab → TransitionTo MainMenu
  - MainMenu：Resource.Load + UI.Open + Audio.Play(BGM) + Localization.T 显示标题
  - InGame：UI.Open(HUD) + Audio.Play(SFX) + Save.Save 写时间戳
- 3 个测试：默认 zh-CN 流程、从 Save 恢复 en-US 偏好、Priority 拓扑顺序验证

### V0.4 Module Priority 完整链
Log(-1000) → Pool/Timer(-500) → Save(-450) → Localization(-420) → Resource(-400) → Audio(-380) → UI(-300) → Procedure(-200) → EntityWorld(-100) → 业务(0)

### Deferred to V0.5
- **InputModule**：强耦合 Unity InputSystem 包，Core 抽象价值低，留 V0.5 与 UnityInputAdapter 一起做
- **YooAsset adapter**：V0.5 首推（IResourceModule 接口设计漏没漏，得真实加载验证）
- **UGUIUIModule adapter**：紧随 YooAsset，配合跑通"加载 Prefab → Open UI"真实闭环
- **sqlite-net SaveModule adapter**：Core ISaveModule + Memory 已封口，sqlite-net 降级为 Adapter 备选

---

## V0.3（玩法层增强 + 关键服务）

### 迭代 0 — EntityWorld 拆分

**Breaking**
- `World` → `EntityWorld`（重命名 + 实现 `IModule` / `IUpdateModule`）
- `WorldState` → `EntityWorldState`
- `world.Update()` 无参版本 → `world.Update(float deltaTime, float unscaledDeltaTime)`
- `IWorldAdapter` 删除（被 ADR-0014 Adapter 模式替代）
- `Handle.Resolve(World)` → `Handle.Resolve(EntityWorld)`
- `SystemBase._world / SetWorld(World)` → `EntityWorld`

**Added**
- `IEntityWorld` 接口：暴露 Entities / CreateEntity / RegisterSystem / EventBus
- `WorldProxy`（Unity 侧）改为持有 EntityWorld，Update 用 `Time.deltaTime`
- `ModuleHostEndToEndTests` 验证完整生命周期闭环
- `EntityWorldModuleIntegrationTests` 5 用例验证 ModuleHost + EntityWorld 协同

**Removed**
- `IWorldAdapter.cs` + `FakeWorldAdapter` + `FakeAdapterTests.cs`

**Deprecated**
- `IWorldEventBus`（V0.4 计划合并到 `IEventBus` 全局事件总线）

### 迭代 1 — BitArray256 位运算加速

**Added**
- `BitArray256` struct：4×ulong 固定容量 256 位，O(1) Add/Remove/Contains/ContainsAll/ContainsAny
- `TypeIndex<T>` 静态泛型：lazy 分配稳定 int index
- `TypeRegistry` + `TypeIndexOverflowException`：类型注册中心
- `Entity._aspectMask / _tagMask`：与字典并行的 mask 镜像
- `Query` 内部完全位运算（删除 HashSet<Type>）

**Performance**
- Query.Matches 退化为 4 次 ContainsAll/ContainsAny 位运算
- 预期 10000 Entity × 10 Query 场景 5-10x 提升（待 Unity 跑实测）
- benchmark 测试：10000 × 5 × 10 frame = 500k Matches 实测 < 1s

### 迭代 1.5 — 加固

**Fixed**
- `Entity.Attach` OnAttach 异常时完整回滚 `_aspects / _aspectMask / Owner`
- `Entity.Detach` OnDetach 异常时回滚 `_aspects / _aspectMask`（Owner 保留作"未完全 detach"标志）
- `TypeRegistry` 所有公开 API 加 `lock(_lock)` 串行化，避免 `_next++` 并发竞态

### 迭代 2 — ProcedureModule（跨帧流程状态机）

**Added**
- `IProcedure` + `ProcedureBase`（OnEnter / OnUpdate / OnExit）
- `IProcedureModule` + `ProcedureModule`（Priority=-200）
- `IProcedureModule.Host` 注入：Procedure 通过 `m.Host.Get<...>()` 拉其他 Module

### 迭代 3 — 端到端 Demo

**Added**
- `LoginFlowDemoTests`：Boot → Login → InGame 三阶段完整流程
  覆盖 ModuleHost / Common 三件套 / EntityWorld / ProcedureModule / System / Aspect / Pool / Timer / Log 全协同

---

## V0.2（ModuleHost 基础设施）

### 迭代 0-3 — ModuleHost 拓扑排序 + Update/LateUpdate 调度
- `IModule` / `IUpdateModule` / `ILateUpdateModule` / `IModuleHost` / `IEventBus`
- `ModuleHost`（Kahn 拓扑排序 + Priority tie-breaker）
- 类型化异常：`ModuleAlreadyRegistered` / `NotRegistered` / `CircularDependency` / `DependencyMissing` / `Shutdown`

### 迭代 4 — Common 三件套
- `ILogModule` + `ConsoleLogModule`（Priority=-1000，OnLog 钩子，CaptureToMemory）
- `ITimerModule` + `TimerModule`（Priority=-500，IUpdateModule 驱动，scaled/unscaled 双轨）
- `IPoolModule` + `IObjectPool<T>` + `PoolModule`（Priority=-500，Stack-based）

### 迭代 5 — 双端门 + V0.1 资产迁移

**Added**
- `ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj`：Shadow csproj 双端编译验证
- 6 个 V0.1 复用文件平移到 `Runtime/Core/Entity/`：Aspect / Entity / EntityId / Handle / Tag / Phase

### 迭代加固
- ModuleHost 错误路径加固：Initialize 半失败回滚 / Shutdown 异常聚合 / DependsOn null 防御
- TimerModule callback 异常聚合（同帧其他 timer 不受单 callback 失败影响）
- ConsoleLogModule Shutdown 后静默 / OnInit 重新可写
- IObjectPool.Return 文档警示重复 Return 是未定义行为

---

## V0.1（ECS 骨架）

- `World` / `Entity` / `Aspect` / `Tag` / `EntityId` / `Handle` / `Phase`
- `SystemBase` / `SystemGroup` / `Query`（HashSet<Type> 路径）
- `IEntityEventDispatcher` + `EntityEventDispatcher`
- `IWorldEventBus` + `WorldEventBus`
- `IWorldAdapter` + `WorldProxy`（V0.3 删除/重构）
- Aspect 隔离纪律（ADR-0007）
- 10 个 ADR + 18 个 issue 骨架
