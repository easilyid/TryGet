# V2 参考框架吸收计划 — 底层架构设计迭代

> **撰写日期**：2026/05/26  
> **状态**：Plan / Analysis，不代表已进入实现  
> **输入来源**：`ReferenceFramework/TEngine`、`ReferenceFramework/hsenl`、`ReferenceFramework/BigCat`、`AmaniDawn/DGame` 源码级只读分析  
> **适用范围**：MyTryGetFramework V2.x 底层 Core 设计演进  
> **当前约束**：Unity 运行时接入暂缓；不做 ECS / 服务端 / IPlugin；Core 继续保持纯 C#。

---

## 0. 目的

本文件用于沉淀参考框架源码分析后的底层架构计划，目标不是照搬任一框架，而是：

1. 从成熟框架中识别可复用的设计思想。
2. 对照 MyTryGetFramework 当前 V2 路线，判断哪些思想值得吸收。
3. 把可吸收内容拆成后续可独立实现、可验证、可回滚的任务。
4. 为未来新增其他参考框架提供统一分析模板。

本文件只描述计划和必要性，不进行源码实现。

---

## 1. 当前架构边界

MyTryGetFramework V2.0 已通过 ADR-0020 从旧的“双端 / ECS / Plugin”路线转向：

**纯客户端服务框架 + 轻量 Procedure Stack + 可验证 Core 基础设施。**

当前保留：

- `ModuleHost` / `IModule`
- `IEventBus` / `IEventScope`
- `TGTask` / `ITGTaskScheduler`
- `IProcedureModule` / `IProcedure`
- `ILogger` / `IClock` / `ITimerModule` / `IPoolModule`
- `IKVStore` / `IConfigSource` / `IAssetSource` / `ISerializer`
- `[Module]` / `[EventHandler]` Source Generator
- 简化 `INetClient`

当前明确不做：

- 不恢复 ECS / EC 模型。
- 不恢复服务端抽象。
- 不恢复 `IPlugin` / `IPlugPoint`。
- 不为未来双端预留复杂系统。
- 不让 `Runtime/Core` 引用 UnityEngine。
- Unity 运行时接入本轮暂缓。

---

## 2. 参考框架分析原则

### 2.1 不照搬原则

TEngine、hsenl、BigCat、DGame 的定位不同：

| 框架 | 主要定位 | 可学习点 | 高风险点 |
|---|---|---|---|
| TEngine | 纯 Unity 客户端商业框架 | Module、Procedure、事件生命周期、资源/热更/工具链 | 静态全局 ModuleSystem、完整 Unity 工具链过早绑定 |
| hsenl | 全栈 / 双端 / 组件化框架 | Attribute 注册、ProcedureLine、UI 面板池、网络 Plug 思想 | Entity/Component 全量引入、双端网络、热重载复杂度 |
| BigCat | 双端 / Worker / EventLoop / Scene Stack | 外部驱动 EventLoop、多阶段 Update、Timer/Coroutine phase 思想 | Node/Worker/RPC/服务端模型 |
| DGame | TEngine 商业客户端扩展 / Unity 工具链 | MonoDriver 边界、GameTimer 坏帧追赶、Pool 诊断、客户端数据域样板 | Unity Runtime/UI/Hotfix/Editor 工具链 |

本项目只吸收能增强 V2 Core 的设计，不引入与 ADR-0020 冲突的系统。

### 2.2 采用标准

一个参考设计只有满足以下条件才进入候选：

1. 能让某个 Module 的 Interface 更 deep，而不是只增加方法数量。
2. 能提高调用者 leverage。
3. 能把复杂性集中到 Implementation，提升 locality。
4. 能通过纯 C# EditMode 测试验证。
5. 不要求 Unity 运行时立即接入。
6. 不恢复 ECS / 服务端 / Plugin。

---

## 3. TEngine 分析结论

### 3.1 观察到的设计

TEngine 的核心路线是：

**Unity Entry → ModuleSystem → Procedure/FSM → Resource/YooAsset → HotFix/HybridCLR → UI → Editor/Build Tools**

源码级观察：

- `GameEntry` 在 Awake 中显式拉起核心 Module，并启动 Procedure。
- `ModuleSystem` 是静态模块容器，按 Priority 排序，缓存 Update 执行表，关闭时反向 Shutdown。
- `ProcedureModule` 本质依托 FSM。
- `GameEventMgr` 记录对象级事件订阅，支持 Clear 自动反注册。
- `ResourceModule` 深度绑定 YooAsset，处理 PlayMode、版本、下载、卸载、加密服务。
- UI 模块管理 UIRoot、Camera、窗口栈、资源加载器和异步打开。
- HybridCLR、Luban、BuildPipelineWindow 组成完整商业工具链。

### 3.2 可吸收点

#### T1. Update 执行表缓存

当前 `ModuleHost` 已经缓存 `IUpdateModule` / `ILateUpdateModule`，TEngine 的价值在于证明：

- 初始化阶段构建执行表。
- 运行阶段只遍历执行表。
- Shutdown 反向执行。
- Priority 顺序必须稳定。

这个思想适合继续深化到 FrameLoop。

#### T2. Procedure 作为主流程驱动

TEngine 用 Procedure 串起初始化、资源、热更、进入游戏。TryGet 当前已有 Procedure Stack，适合保留 Stack 语义，但需要进一步让 transition 结果更明确。

可吸收思想：

- Procedure 是客户端主流程入口。
- 流程切换不应让调用者到处管理内部状态。
- 启动链应有可测试的流程样板。

#### T3. Owner / Scope 事件生命周期

TEngine 事件系统的重要优点不是派发本身，而是按对象记录订阅、集中清理。

可吸收思想：

- EventScope 应成为事件系统一等能力。
- 订阅生命周期必须由框架集中管理。
- UI / Procedure / Module 后续都应能自然使用 scope。

### 3.3 暂不吸收点

- 不引入静态全局 `ModuleSystem`。
- 不引入完整 YooAsset 下载/加密/多包更新链。
- 不引入 HybridCLR/Obfuz 热更链。
- 不引入 UI 代码生成器。
- 不引入 Editor/Build 全家桶。

这些能力应等 Core seam 稳定、Unity Adapter 明确后再分包设计。

---

## 4. hsenl 分析结论

### 4.1 观察到的设计

hsenl 的核心路线是：

**Entity/Component 树 + EventSystem 扫描注册 + ProcedureLine 流水线 + Network Service/Channel/Plug + YooAssets + HybridCLR 工具**

源码级观察：

- Entity 管父子、Scene、激活态、组件生命周期。
- Component 缓存 Require、生命周期、类型索引等运行期元数据。
- EventSystem 扫描程序集、缓存 Attribute、驱动生命周期和事件调用。
- ProcedureLine 把业务流程拆成 item + handler + worker。
- Network 分 Service、Channel、Packet、Plug、Opcode、MessageDispatcher。
- UIManager 有层级、单例/多实例、面板池、异步加载。
- HybridCLR 工具负责编译并复制热更 DLL / AOT metadata。

### 4.2 可吸收点

#### H1. 注册目录诊断能力

TryGet 已有 Source Generator 注册：

- `[Module]`
- `[EventHandler]`
- `AssemblyManifestRegistry`
- `EventHandlerRegistry`

hsenl 的 EventSystem 证明注册目录应该具备诊断能力，而不只是静态列表。

可吸收思想：

- 注册表可查询当前注册项。
- 重复注册策略明确。
- 生成结果可测试。
- 启动失败时能输出清晰诊断。

#### H2. ProcedureLine / Pipeline 思想

Procedure Stack 适合游戏状态和叠层流程，但不适合所有线性业务。

hsenl 的 ProcedureLine 说明商业项目常有另一类流程：

- 启动链。
- 登录链。
- 资源初始化链。
- 战斗结算链。
- Buff / 词条 / 规则流水线。

可吸收思想：

- 在 Procedure Stack 之外，设计轻量 Pipeline Module。
- Pipeline 只做有序步骤和上下文传递，不引入 ECS。
- 每个 step 可测试、可插拔、可失败短路。

#### H3. 网络 Plug 思想转化为客户端拦截器

ADR-0020 不允许恢复 IPlugin，但网络内部的拦截链思想有价值。

未来 V2.7 可作为 `INetClient` Adapter 的内部 Implementation：

- 日志。
- 加密。
- 压缩。
- 重连。
- 流量统计。
- 错误恢复。

当前不进入 Core，只记录为未来网络 Adapter 设计参考。

### 4.3 暂不吸收点

- 不引入完整 Entity / Component。
- 不引入组件位图、类型索引和 Scene/Transform 映射。
- 不引入双端网络 Service / Channel / RPC 全套。
- 不引入运行时热重载程序集替换。
- 不引入 HybridCLR 工具链。

这些能力与当前纯客户端 Core 路线冲突或过早。

---

## 5. BigCat 分析结论

### 5.1 观察到的设计

BigCat 的核心路线是：

**Node / Worker / EventLoop / MainModule / SceneMgr / GameUnit / RPC**

源码级观察：

- Node 是 EventLoop，持有多个 Worker，负责启动与关闭顺序。
- Worker 是串行逻辑执行单元。
- UnityWorker 无自有线程，由 Unity 外部驱动。
- UnityEventLoop 每帧处理定时任务、队列事件、模块更新。
- SceneMgr 管 active / closed 列表，并按 Begin / Early / Fixed / Update / Late / EndOfFrame 分相驱动。
- Timer / Coroutine 绑定 GameLoopPhase。
- RPC / S2S / Worker 路由服务端味道很重。

### 5.2 可吸收点

#### B1. 外部驱动 EventLoop

这是 BigCat 最适合 TryGet 的思想。

当前 TryGet 只有：

- `ModuleHost.Update`
- `ModuleHost.LateUpdate`

商业客户端底层更合理的模型是：

1. 处理主线程队列。
2. 处理 Timer / Await continuation。
3. 分阶段 Update Module。
4. 执行 EndOfFrame 清理。

这可以让 FrameLoop 成为 deep Module。

#### B2. 多阶段 Update

BigCat 的多阶段为后续 UI、场景、音频、资源调度提供基础。

适合 TryGet 的最小阶段：

- `EarlyUpdate`
- `FixedUpdate`
- `Update`
- `LateUpdate`
- `EndOfFrame`

暂不引入 Begin，也不引入 Worker。

#### B3. Phase-aware Timer / Scheduler

BigCat 的 Timer / Coroutine 绑定 phase，这能启发 TGTaskScheduler 演进。

未来可增加：

- `Yield(FramePhase phase)`
- `Delay(seconds, TimeMode.Scaled / TimeMode.Unscaled)`
- `WaitForFrames(count, FramePhase phase)`

### 5.3 暂不吸收点

- 不引入 Node / Worker。
- 不引入 Disruptor。
- 不引入 RPC / S2S。
- 不引入服务发现。
- 不引入 GameUnit / Scene ECS 容器。
- 不引入服务端 Session。

BigCat 的双端能力只作为未来 V3.0+ 参考。

---

## 6. DGame 分析结论

### 6.1 观察到的设计

DGame 的核心路线是：

**TEngine 客户端底座 / Unity MonoDriver / YooAsset / HybridCLR / 商业项目常用模块扩展**

源码级观察：

- `GameEntry` 在 Unity `Awake` 中显式拉起 `IMonoDriver`、`IResourceModule`、`IFsmModule`，再启动 Procedure。
- `LauncherMgr` 通过 `GameObject.Find`、`Resources.Load`、`Instantiate` 管理启动更新界面。
- `ModuleSystem` 延续 TEngine 静态全局模块容器，按 `Priority` 插入模块和 Update 模块，销毁时反向清理。
- `MonoDriver` 将 Unity `Update` / `FixedUpdate` / `LateUpdate` 转为框架监听器。
- `EventMgr` 保存跨模块接口包装，`EventDispatcher` 使用 `int eventId -> Delegate` 和 0-6 参数重载派发。
- `GameTimer` 支持 scaled / unscaled、暂停、循环，并对坏帧追赶设置最大补偿次数。
- `MemoryCollector` / `ObjectPool` 暴露 unused / using / capacity，并提供重复释放检查、容量、过期、自动释放、优先级策略。
- `LocalizationModule`、`DataCenterModule`、`ClientSaveDataMgr`、`RedDotModule` 是商业客户端常见业务服务样板。
- UI / Input / Anim / Resource / Hotfix / Editor 模块强绑定 Unity、YooAsset、HybridCLR、Luban 和热更业务程序集。

### 6.2 可吸收点

#### D1. Unity Driver 与 Core Tick 边界

DGame 的 `MonoDriver` 证明商业客户端需要明确的 Unity Driver，但 TryGet 当前 Core 不应直接引入 `MonoBehaviour`。

适合 TryGet 的吸收方式是：

- Core 只定义 FrameLoop / Phase / Tick 契约。
- Unity Adapter 未来负责把 Unity 生命周期转发给 Core。
- 不让 `Runtime/Core` 引用 `UnityEngine`。

这进一步支撑 Candidate 1。

#### D2. GameTimer 的坏帧追赶上限

DGame 的 Timer 在长帧或卡顿后不会无限补偿循环回调，而是设置最大追赶次数。

TryGet 当前 `TimerModule` 已有 scaled / unscaled、pause、repeat、异常聚合，但缺少 catch-up policy。这个能力适合并入后续 Scheduler / Timer 迭代：

- repeating timer 明确是否补偿错过 tick。
- catch-up 次数有上限。
- 默认策略应保守，避免单帧执行过多回调。

#### D3. Pool 诊断与严格回收策略

DGame 的 Pool 不只是 spawn / release，还提供容量、使用中、未使用、重复释放检查、过期与自动释放。

TryGet 如果后续扩展 `IPoolModule`，应优先吸收：

- 诊断快照。
- 重复释放检测。
- 容量限制。
- 可选自动清理。

不需要把 Unity GameObjectPool 放入 Core。

#### D4. DataCenter / ClientSaveData 的客户端数据域思想

DGame 把运行期数据中心和本地存档管理分离，且存档 key 结合角色维度。

TryGet 当前已有 `IKVStore` / `IConfigSource` seam，但仍偏 hypothetical。只有当出现真实客户端账号、角色、存档需求时，才应引入更 deep 的数据域接口。

#### D5. RedDot 作为可选纯模型服务

DGame 的 RedDot 树是 UI 常见需求，但核心逻辑可以纯 C# 表达。

它不应进入 Core 必备底座，但可作为未来 Samples 或 Adapter 上层服务，用于验证 EventBus、DataStore、UI binding 的组合能力。

### 6.3 暂不吸收点

- 不吸收静态全局 `ModuleSystem` 与接口命名反射创建。
- 不把 `MonoDriver` / `GameObject` / `Resources` / Playable / InputSystem 放入 Core。
- 不采用 `int eventId` + 0-6 参数重载的弱类型事件派发作为 Core EventBus。
- 不吸收 UI / Input / Anim / GMPanel / SuperScrollView 的 Unity 实现。
- 不吸收 YooAsset / HybridCLR / Luban / Editor 工具链。
- 不吸收业务单例系统和 HotFix 层业务目录结构。

DGame 对 TryGet 的价值主要是商业客户端模块样板，而不是 Core 架构形态。

---

## 6bis. 第二轮参考框架分析（AlicizaX / AmaniDawn·DGame 源码复核，2026/05/30）

> 本轮按 §11 模板，对 `ReferenceFramework/AlicizaX/`（UPM 包体系）与 `ReferenceFramework/DGame/`（AmaniDawn/DGame，对 §6 旧结论做源码级复核）做只读分析。
> 澄清：用户给的 `github.com/Dgame/Dgame` 经核实是 D 语言 2D 图形框架（SDL/OpenGL，2019 归档，非 Unity/非 C#），与 TryGet 无关，已排除；§6 与本节的 Unity「DGame」均指 `AmaniDawn/DGame`。

### 6bis.1 基本信息

| 项 | AlicizaX | AmaniDawn/DGame |
|---|---|---|
| 框架名称 | Aliciza X Framework（com.alicizax.unity.*） | DGame |
| 源码路径 | `ReferenceFramework/AlicizaX/com.alicizax.unity.framework`（286 cs，核心）+ `SourceGenerateTools` + 7 个扩展包 | `ReferenceFramework/DGame/GameUnity/Assets/DGame/Runtime`（734 cs） |
| 定位 | 纯 Unity 客户端框架（UPM 包形态） | 基于 TEngine 的纯 Unity 客户端扩展框架 |
| 是否依赖 Unity | 是（Mono\* 驱动层 + Cysharp.Text/UniTask 依赖） | 是（TEngine 系，强绑 YooAsset/HybridCLR） |
| ECS / Entity | 无 | 无 |
| 服务端 / 网络 | 无 | 无 |
| 热更 / 资源工具链 | UI/资源 SourceGen | YooAsset / HybridCLR / Luban |

### 6bis.2 AlicizaX 关键源码证据与设计思想

AlicizaX 核心是 `Runtime/ABase/Service/Core/` 的一套 **三层作用域 Service Locator**：

- **作用域分层 + 就近遮蔽**：`ServiceWorld` 固定 3 槽 `App(-10000)/Scene(-5000)/Gameplay(0)`（`AppScope.cs:30-35`、`ServiceWorld.cs:9-11`）；同一契约可在不同 scope 注册不同实现，`ContractBindings.TryGetBest` 解析时按 `Gameplay→Scene→App` 返回最具体绑定（`ServiceWorld.cs:236-258`）；`Scene/Gameplay` 可 `Ensure` 懒创建、可整体 `Dispose`（活动 scope 逆序销毁，`ServiceWorld.cs:122-132`、`ServiceScope.cs:152-176`）。
- **重入安全延迟增删**：tick 迭代期间置 `_isIterating`，期间 Register/Unregister 入 `_pendingChanges` 队列，迭代结束 `FlushPendingChanges` 统一应用（`ServiceScope.cs:78-90,116-150,403-429`）——稳态零分配。
- **索引化 swap-remove**：每服务缓存其在各相 tick 列表中的下标，注销时末尾搬移 + 回写索引，O(1) 注销、稳态零 GC（`ServiceScope.cs:296-401,498-518`）。
- **四相 tick 能力接口** `IServiceTickable/LateTickable/FixedTickable/GizmoDrawable`（`IServiceTicks.cs`）+ `IServiceOrder` 手动 int 排序，由 `AppServiceRoot`(MonoBehaviour) 驱动。
- **SourceGen 诊断友好**：`UIMetaSourceGenerator` 对未具体化的开放泛型 `ReportDiagnostic(UI002,Error,带 Location)`（`UIMetaSourceGenerator.cs:44-58`）；`UIResRegistryGenerator` 生成 UI 资源注册表。`EventSourceGenerator` 实为 `[Prewarm(N)]` 容量预热表（非 handler 订阅）。
- **GameObjectPool 诊断**：`GameObjectPoolSnapshot` 含 acquire/release/hit/miss/peak/容量 + `generation-token` 三重校验（handle 引用 + generation + state）杜绝重复释放/陈旧句柄（`RuntimePoolModels.cs:438-579,890-927`）。

**无 DependsOn 拓扑排序**：AlicizaX Init 在 Register 时立即触发、依赖靠 `Require<T>` 惰性解析——这点 **TryGet ModuleHost 的拓扑序 + 循环检测 + 失败回滚反而更强，吸收时不得回退**。

### 6bis.3 DGame 复核（修正 §6 旧结论）

- **修正 D1**：模块 tick 真实走 `RootModule(MonoBehaviour).Update → ModuleSystem.Update`（单相位，`RootModule.cs:248-262`）；`MonoDriver` 不是模块驱动器，只是给任意代码挂 Unity 生命周期/协程的事件中枢（`MonoDriver.cs:124-177`）。§6 把两者混为一谈，特此更正。
- **细化 D2**：`GameTimer` 坏帧追赶上限 `m_maxBadFrameCheckCnt=10` 是**所有 loop timer 共享的全局节点访问预算**（`GameTimerModule.cs:16,60-160`），粗粒度且不可配——TryGet 应改为每定时器可配 `maxCatchUp`，不照搬全局预算。catch-up 逻辑是纯 C#、可 dotnet 验证（`GameTimerModule.cs:1-2`，无 UnityEngine）。
- **坐实红线**：`ModuleSystem` 是 `public static class` + 反射按命名实例化（`ModuleSystem.cs:9,71-73`）；`GameEvent` 是 `StringId.StringToHash → int eventId` 弱类型派发（`GameEvent.cs:159-166`、`StringId.cs:17-28`）。
- **次级发现**：`EventDelegateData` 用 `m_isExecute + addList/removeList` 延迟增删实现重入安全、无 ToArray 拷贝（`EventDelegateData.cs:20-57,82-97`），但 **handler 抛异常会中断循环 → CheckDataDirty 不执行 → 延迟队列永不 flush**。与 AlicizaX 的 PendingChange 模式**互相印证**同一思路，但两者都缺异常隔离。

### 6bis.4 可吸收点（汇入候选）

| 设计点 | 来源（双印证标注） | 符合 ADR-0020 | 映射候选 |
|---|---|---|---|
| EventBus 重入安全延迟增删 + 零 GC 派发 | AlicizaX PendingChange + DGame EventDelegateData（双印证） | ✓ 纯 C# 可验证 | **C2**（强化） |
| SourceGen 诊断友好（ReportDiagnostic 替代静默 return null） | AlicizaX UIMetaSourceGenerator | ✓ tooling 可验证 | **C5**（强化） |
| Timer 有界坏帧追赶（每定时器可配 maxCatchUp） | DGame GameTimer（改进其全局预算缺陷） | ✓ 纯 C# 可验证 | **C7（新增）** |
| 服务作用域分层（App/Scene/Gameplay + 就近遮蔽 + scope 级 Dispose） | AlicizaX ServiceWorld | ✓ 主体纯 C# | **C8（新增，架构级，需先决策）** |
| 对象池诊断快照 + generation-token 重复释放检测 | AlicizaX + DGame（双印证） | ✓ 纯 C# 可验证 | **C9（新增，低优先）** |
| 索引化 swap-remove（O(1) 注销，稳态零 GC） | AlicizaX ServiceScope | ✓ 纯 C# 可验证 | C2/C3 实现技巧 |

### 6bis.5 不吸收点（红线）

- AlicizaX：`RootModule/AppServiceRoot/MonoServiceBehaviour`（MonoBehaviour）、`AppServices` 静态全局单例、`Cysharp.Text`(ZString) 依赖、GameObjectPool 强绑 Unity——只取纯 C# 的 `ServiceScope/ServiceWorld` 机制，Mono 驱动与第三方依赖挡在 Adapter 层外。
- AlicizaX `Register<TContract,TExtraContract>` 多契约绑定——超出 ADR-0020 最小化范围，不引入。
- DGame：静态全局 `ModuleSystem` + 反射按命名注册、弱类型 `int eventId` 派发、`Fsm` 强绑 `Animator/Playable`、`CreateSystemTimer` 用 `System.Timers.Timer` 线程池回调（非帧安全）——全部红线。

---

## 6ter. 第三轮参考框架分析（MyFramework 首析 + TryGet 自审 + 五框架定向复查，2026/06/13）

> 本轮按 §11 模板对 `ReferenceFramework/MyFramework/`（ZHOURUIH/MyFramework）做首次源码级分析；同时对 TryGet Core 做实现状态自审（修正候选矩阵的陈旧标记），并带着「TGTask 池化技术债」「V2.3+ 契约设计」两个问题定向复查 hsenl/BigCat/TEngine。

### 6ter.1 基本信息（MyFramework）

| 项 | 内容 |
|---|---|
| 框架名称 | MyFramework（ZHOURUIH） |
| 源码路径 | `ReferenceFramework/MyFramework/`（1016 cs；框架主体 `Assets/Scripts/Frame_HotFix/` 858 cs） |
| 定位 | 纯客户端 Unity 商业级框架 + 工具链（MMORPG/传奇类商业项目验证） |
| 是否依赖 Unity | 是（管理器层直接 using UnityEngine，无纯 C# 内核分层） |
| ECS / Entity | 无（Character/MovableObject 是传统对象模型） |
| 服务端 / 网络 | 仅客户端网络（NetPacketFactory/NetPacketTypeManager） |
| 热更 / 资源工具链 | HybridCLR + Obfuz 热更分层；外置 C++ CodeGenerator 文件模板生成（UI/配置表/协议）；**Roslyn Analyzer 工程检查**（`Analyzers/AnalyzerUnity.dll`，源码在 `ToolProject/AnalyzerUnity/`） |

### 6ter.2 MyFramework 关键源码证据与设计思想

**架构总览**：五层装配体 `Frame_Base`（非热更基础）→ `Frame_HotFix`（热更框架层，主体）→ `HotFix`（热更业务）；`Frame_Game`→`Game`（非热更侧）。根容器 `GameFrameworkHotFix` 显式注册 50+ 个 `FrameSystem` 管理器（`Frame_HotFix/GameFramework/GameFrameworkHotFix.cs:377-434`），update/fixedUpdate/lateUpdate 三相派发、每管理器 `ProfilerScope` 包裹 + 初始化异常逐个隔离（`GameFrameworkHotFix.cs:330-345`）。

逐设计点：

1. **Roslyn Analyzer 框架契约检查（本轮最大发现）**：`ToolProject/AnalyzerUnity/AnalyzerUnity/AnalyzerResetProperty.cs:10-16` 定义 `RESET001`（**Error 级**）——任何实现 `resetProperty()` 的池化类，方法体内必须重置**全部非 static 实例字段**，漏一个字段直接编译失败；`AnalyzerCallBase.cs:9-15` 定义 `BASE001`——override 指定生命周期方法必须调 `base.X()`。这是「强生命周期控制」的编译期执法：池化对象状态残留这类最难排查的 bug 被消灭在编译期。**BigCat 仓库同样自带 `csharp/Analyzers/` 目录，双印证「框架自带 Analyzer」路线**。TryGet 已有 SourceGenerator 管线，补一个同管线的 Analyzer 成本低。→ **新增 Candidate 12**。
2. **三条独立顺序轴 + 四段式初始化**：`registeFrameSystem<T>(callback, initOrder, updateOrder, destroyOrder)`（`GameFrameworkHotFix.cs:242-258`）允许 init/update/destroy 顺序各自独立——真实案例：ResourceManager 最先 init、最后 destroy（3000）；CommandSystem 在大部分管理器销毁后才销毁（2001）；GameSceneManager 最先 destroy（0）（`GameFrameworkHotFix.cs:379-433` 注释）。生命周期为 `preInitAsync→initAsync→init→lateInit`+`willDestroy→destroy` 两段销毁 +`resourceAvailable` 资源就绪钩子（`FrameSystem.cs:20-46`）。**观察**：TryGet「Shutdown=OnInit 逆序」在 95% 场景正确，但「日志/命令系统需要比逆序更晚销毁」是真实需求；暂不立候选，留作 ModuleSystem 未来压力测试用例。异步初始化段在 V2.4 资源 Adapter 落地时会成为真实需求（Module 启动期异步加载资产），届时再评估。
3. **CommandSystem（招牌设计，确认不吸收，但有一个可剥离思想）**：池化 `Command : ClassObject`（`CommandSystem/Command.cs:5`）+ 延迟命令队列每帧扣减 delayTime（`CommandSystem.cs:23-56`）+ 跨线程输入缓冲（input/process 双列表 + 锁同步，`CommandSystem.cs:273-283`）。整套「方法调用对象化」的间接层与 TryGet 直接接口调用 + struct 事件方向相反，**不吸收**。可剥离的思想是**宿主销毁联动取消**：`notifyReceiverDestroied(receiver)` 把该接收者所有挂起延迟命令清掉（`CommandSystem.cs:243-271`）、`DelayCmdWatcher` 让发起者销毁时中断自己发出的命令——对应 TryGet 的真实空白：EventScope 管了事件订阅，但 **Timer/TGTask 没有 owner-scope 批量取消**（宿主 Procedure 退出后 pending Delay/Timer 仍会触发）。记入 §10 之外的观察项，待 V2.1/V2.3 出现真实泄漏案例再立候选。
4. **EventSystem**：`TypeID<T>.ID` 静态泛型缓存做 type→int 零字典查找（`EventSystem/EventSystem.cs:95`）；**派发递归深度护栏** `MAX_DEPTH=20`，超限 logError 拒发（`EventSystem.cs:14,77-81,130-134`）——事件 A 的 handler 又发事件 B 形成环时不会栈溢出；per-handler try/catch 异常隔离（`EventSystem.cs:109-119`）；`unlistenEvent(listener)` 监听者批量解绑（`EventSystem.cs:193-209`，同 TEngine owner-clear，三印证）；事件参数对象池化复用（`ClassScope<T>`，`EventSystem.cs:62-66`）。**递归深度护栏是 TryGet EventModule 的真实缺口**（见 6ter.3），并入 C2 收尾。
5. **ClassPool 泄漏检测**：编辑器模式维护 inuse/persistent-inuse 双列表 + 分配点堆栈快照（F4 开关，`Pool/ClassPool/ClassPool.cs:13-16,104`）；「帧临时对象」（onlyOnce）若下一帧仍在使用，update 里直接 logError 并打印**分配时堆栈**（`ClassPool.cs:30-53`）；每次分配发新 `assignID` 世代号（`ClassPool.cs:92`，`ClassObject.cs:8-11`）。「租借类别（帧临时/持久）+ 分配堆栈 + 世代号」三件套给 **C9** 提供了比快照计数更可执行的实现蓝本。
6. **Scope 系列 RAII 借还**：`using var a = new ListScope<int>(out var list)`（`Scope/` 18 个文件）把池借还绑到 C# using 作用域，杜绝忘还——纯语法层技巧，TryGet PoolModule 成熟后可作为扩展 API 形态参考（C9 实现技巧，不单列候选）。
7. **SceneProcedure 树形流程**：父子流程树，进子流程时父 `onExitToChild` 不销毁、子回父走 `onInitFromChild`（`GameScene/SceneProcedure.cs:8-52`）。与 TryGet 栈式 Procedure 是同问题的不同解；树形表达「共享父状态的兄弟流程」更强但复杂度高，**确认不吸收**（ADR 已选栈式，Push/Pop 可覆盖主要场景）。
8. **DoubleBuffer / 各类 Thread 池**：多线程写单线程读的双缓冲（`DoubleBuffer/DoubleBuffer.cs:7-18`）——TryGet 单线程模型（ADR），**不吸收**；若 V2.7 网络 Adapter 要跨线程收包，这是 Adapter 层（非 Core）的现成参考。
9. **代码生成工具链**：外置 C++ `ToolProject/CodeGenerator` 做文件模板生成（UI/配置表/协议），生成时机靠人工跑工具——比 TryGet 的 Roslyn 增量 SourceGen 落后一代，**机制不吸收**；其「生成物 + 注册逻辑一起生成」的完整性可在 V2.3 UI 代码生成时参考清单。

### 6ter.3 TryGet Core 现状自审（修正候选矩阵陈旧标记）

- **C2 实际已基本完成，矩阵标记过时**：`Runtime/Core/Event/EventModule.cs` 已实现重入安全延迟增删（`_pendingChanges`+`_dispatchDepth`，`EventModule.cs:120-174`）、per-handler 异常隔离（`EventModule.cs:100-110`）、零 ToArray 派发（索引循环）。**剩余缺口**：(a) handler 异常仅存入 `_lastPublishExceptions`（internal，`EventModule.cs:66-72`），生产路径上**静默吞掉**，无日志/钩子上报策略；(b) 无派发递归深度护栏（`_dispatchDepth` 只增减不设限）；(c) 文档/CHANGELOG 未声明完成。→ C2 收尾 = 异常上报策略 + 深度护栏 + 关单。
- **C5 维持半成判断**：`ModuleRegistry` 仅 `Count`（`ModuleRegistry.cs:50`），`EventHandlerRegistry` 有 `Count+Snapshot`（`EventHandlerRegistry.cs:41-47`）；无重复注册检测、无可读 manifest 导出。
- **TGTask 技术债定位坐实**：
  - `TGTask.IsCompleted` 不校验 Version（`TGTask.cs:50-54`、Awaiter 同 `TGTask.cs:120-124`）——body 复用后旧句柄读到他人状态；
  - Builder 型 body 已有消费侧归还（`Awaiter.GetResult` finally + `Forget`，`TGTask.cs:127-145,61-89`），**Manual 型（TGTaskCompletionSource）依赖显式 `Return()`，不调则 GC 兜底**（`TGTaskCompletionSource.cs:57-66`）；
  - **调度器热路径双重逃逸**：`TGTaskScheduler` 每次 Yield/Delay/WaitForFrames `new TGTaskCompletionSource()`（tcs 本身堆分配，`TGTaskScheduler.cs:146,160,180`），SetResult 后因「外部还持 TGTask」不能立即 Return（`TGTaskScheduler.cs:260-267,281,297` 注释），body 全部漏给 GC——池化在最热路径完全失效；
  - 帧边界启发式 `phase <= _lastProcessedPhase` 即新帧（`TGTaskScheduler.cs:226-237`）——真实 Unity 一帧多次 FixedUpdate 会误增 frameCount。

### 6ter.4 定向复查发现（hsenl / BigCat / TEngine）

- **hsenl HTask 的池化生命周期答案（TGTask 技术债的直接解法）**：`HTaskCompletionBody.SetResult/SetException` 在完成瞬间就 `IncrementVersion()`（`hsenl/.../HTask/HTaskBodys/HTaskCompletionBody.cs:62-83`）——**完成即失效**，所有旧句柄立刻过期；body 归还发生在**消费侧** `GetResult()` 的 try/finally（`HTaskCompletionBody.cs:39-60`），Manual（completion）型与 Builder 型同享此路径（`HTaskBody.cs:49,104`）；无人 await 且发生异常时，body 立即归还并原地 rethrow，不吞异常（`HTaskCompletionBody.cs:103-118`）。TryGet V0.6 只学了 HTask 的 struct+version 外形，没学完成即失效 + 消费侧归还的生命周期闭环。
- **BigCat ValuePromise 印证**：`TimerMgr.Schedule` 用 `ValuePromise<T>.Acquire(out int rid, eventLoop)` 取池化 promise + 回收令牌 rid，任务对象自身也从 `taskPool.Acquire()`（`BigCat/csharp/Wjybxx.BigCat.Core/src/Co/TimerMgr.cs:44-56`）——「池化 promise + 令牌校验」与 hsenl 双印证。→ 合并立 **Candidate 11**。
- **TEngine UI 契约层（V2.3 预研素材，非本期候选）**：`UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/` 的 `UIBase/UIWindow/UIWidget + WindowAttribute + IUIResourceLoader`（UIWindow.cs 523 行）给出了 UIWindow 生命周期、层级/全屏遮挡、资源加载 seam 的完整样板；强绑 Unity，届时只剥契约。

### 6ter.5 可吸收点（汇入候选）

| 设计点 | 来源（印证标注） | 符合 ADR-0020 | 映射候选 |
|---|---|---|---|
| TGTask 完成即失效（Version++ on complete）+ 消费侧统一归还 + tcs 池化 | hsenl HTaskCompletionBody + BigCat ValuePromise（双印证） | ✓ 纯 C# 可验证 | **C11（新增，P1）** |
| Roslyn Analyzer 框架契约编译期执法（Reset 完整性/必须调 base 等） | MyFramework AnalyzerUnity + BigCat Analyzers（双印证） | ✓ tooling 可验证 | **C12（新增，P2）** |
| EventBus 派发递归深度护栏 | MyFramework EventSystem MAX_DEPTH | ✓ 纯 C# 可验证 | **C2 收尾项** |
| handler 异常上报策略（替代静默吞掉） | MyFramework logException / TGTask UnobservedException 同型 | ✓ 纯 C# 可验证 | **C2 收尾项** |
| 池租借类别（帧临时/持久）+ 分配堆栈 + 世代号泄漏检测 | MyFramework ClassPool（强化 AlicizaX/DGame 双印证的 C9） | ✓ 纯 C#（堆栈捕获仅 DEBUG） | **C9（强化）** |
| 宿主销毁联动取消（owner-scope 批量取消 pending 异步/定时器） | MyFramework DelayCmdWatcher/notifyReceiverDestroied | ✓ 需设计 | 观察项（待真实泄漏案例） |
| init/destroy 顺序解耦的真实案例集 | MyFramework 三顺序轴 | — | 观察项（ModuleSystem 压力测试用例） |

### 6ter.6 不吸收点（红线/不符）

- MyFramework：CommandSystem 整套「调用对象化」间接层（与直接接口+struct 事件方向相反）、静态全局 `FrameBaseHotFix` 单例族 + 管理器直接 using UnityEngine（无纯 C# 内核）、`TypeID` 弱类型 int 事件 ID（TryGet 用 struct 类型本身）、树形 SceneProcedure（已选栈式）、DoubleBuffer/Thread 池（单线程模型红线）、C++ 文件模板代码生成（Roslyn SourceGen 更先进）、HybridCLR/Obfuz 热更绑定（V2.8 前不碰）。
- 复查确认无新增吸收：TEngine/DGame 的 UI/资源/热更运行时（强绑 Unity，留作 V2.3-V2.8 Adapter 期参考）；hsenl HTaskCompletionBody 的 lock/ManualResetEventSlim 线程同步部分（TryGet 单线程模型，只取 version/归还语义，不取线程安全机制）。

---

## 7. 当前 TryGet Core 摩擦

### 7.1 FrameLoop Interface 还不够 deep

当前 `ModuleHost` 只有 Update / LateUpdate。随着 UI、资源、场景、音频增加，时序复杂度会扩散到调用者。

需要把时序复杂度集中到 Framework Host / FrameLoop 的 Implementation。

### 7.2 EventBus 还偏 shallow

当前事件 Interface 主要是：

- Publish
- Subscribe
- Unsubscribe

缺少明确策略：

- 重复订阅。
- 派发中订阅 / 退订。
- handler 异常。
- scope / owner 生命周期。
- 诊断信息。

### 7.3 TGTaskScheduler 缺少时间策略

当前 Scheduler 只表达：

- Yield
- Delay
- WaitForFrames

缺少：

- scaled / unscaled。
- phase-aware wait。
- cancellation。
- timeout。
- shutdown 行为。

### 7.4 ProcedureModule 暴露内部异步状态

当前 Procedure 对外暴露：

- IsEntering
- IsExiting
- LastAsyncError

这说明调用者需要理解 Implementation 状态。更 deep 的 Interface 应该提供 transition result 或 awaitable transition。

### 7.5 Config / Asset / KV / Serializer seam 仍偏 hypothetical

当前主要只有 Memory Adapter。根据 “one adapter = hypothetical seam, two adapters = real seam”，这些接口不宜过早复杂化。

### 7.6 Source Generator Registry 是 pass-through

当前注册表主要是静态列表 + ApplyAll。未来需要：

- 诊断。
- 可查询 manifest。
- 重复注册策略。
- 测试覆盖。

---

## 8. 候选架构任务

### Candidate 1 — FrameLoop Module

**来源参考**：BigCat EventLoop / SceneMgr phase，TEngine Update 执行表，DGame MonoDriver 边界。

**必要性**：

后续 UI、资源、场景、音频、异步调度都会依赖稳定帧时序。如果不先集中时序，后续各 Module 会各自发明 Update 顺序，导致 locality 变差。

**范围**：

- 定义 `FramePhase`。
- 增加 EarlyUpdate / FixedUpdate / EndOfFrame Module 契约。
- `ModuleHost` 初始化后构建各阶段执行表。
- 保持现有 Update / LateUpdate 行为。
- 不接 Unity Runtime。
- 不引入 Worker / Node。

**预期收益**：

- 提升 ModuleHost Interface depth。
- 让后续 Scheduler / Timer / UI / Scene 都有统一 phase 语言。
- 可纯 C# 测试。

**验证标准**：

- 阶段顺序可测。
- Priority 与 DependsOn 顺序稳定。
- Shutdown 仍逆序。
- 未初始化调用各阶段应抛出一致异常。
- Core 不引用 UnityEngine。

**优先级**：P0。

---

### Candidate 2 — EventBus Lifecycle Policy ✅ **已完成（2026/06/13 关单）**

**设计文档**：`docs/design/V2.1-eventbus-zero-gc-lifecycle.md`（核心最小闭环 2026/05/30 落地；2026/06/13 收尾补 `IEventModule.HandlerException` 异常上报钩子 + `MaxPublishDepth=32` 递归深度护栏，Unity EditMode 368/368 全绿验证，见 CHANGELOG V2.1 段）。

**来源参考**：TEngine GameEvent owner-clear，hsenl EventSystem 注册目录；第二轮强化吸收 AlicizaX PendingChange / DGame EventDelegateData 的重入安全延迟增删，并补上两者缺失的 handler 异常隔离。

**必要性**：

事件系统后续会被 UI、Procedure、Module 大量使用。如果订阅生命周期不进入事件 Interface，业务侧会出现泄漏和重复解绑问题。

**范围**：

- 明确重复订阅策略。
- 明确派发中 Subscribe / Unsubscribe 行为。
- 明确 handler 异常策略。
- 将 EventScope / owner 订阅提升为一等能力。
- EventHandlerRegistry 增加诊断查询。

**预期收益**：

- 提高 EventBus Interface depth。
- 减少 UI / Procedure 订阅泄漏。
- 后续 Source Generator 事件升级有基础。

**验证标准**：

- 重复订阅测试。
- 派发中退订测试。
- handler 异常测试。
- scope dispose 自动退订测试。
- registry 诊断测试。

**优先级**：P1。

---

### Candidate 3 — Phase-aware TGTaskScheduler

**来源参考**：BigCat phase timer / coroutine，TEngine UniTask 异步启动链，DGame GameTimer catch-up policy。

**必要性**：

当前 TGTaskScheduler 只能表示普通 Yield/Delay/WaitForFrames。随着 FrameLoop 增加，业务需要等待 fixed、late、end-of-frame、unscaled delay 等语义。

**范围**：

- 等 Candidate 1 的 `FramePhase` 稳定后再做。
- 增加 `Yield(FramePhase phase)`。
- 增加 `Delay(seconds, TimeMode)`。
- 明确 repeating timer 的 catch-up policy 与最大补偿次数。
- 增加 `WaitForFrames(count, FramePhase)`。
- 评估 cancellation token，但不一定第一版引入。

**预期收益**：

- Scheduler Interface 更 deep。
- Timer / await / Procedure 异步语义集中。
- 避免业务自己组合 Timer + TCS。

**验证标准**：

- Update / Late / Fixed phase wait 测试。
- scaled / unscaled delay 测试。
- repeating timer catch-up 上限测试。
- shutdown 后 pending task 行为测试。
- Core 不引用 UnityEngine。

**优先级**：P1，依赖 Candidate 1。

---

### Candidate 4 — Procedure Transition Result ✅ **已完成（2026/06/13）**

**来源参考**：TEngine FSM transition 封装，hsenl ProcedureLine 的流程结果思想。

> 实施记录：`Start/Push/Pop/Replace` 签名 `void` → `TGTask`（向后兼容），返回可 await 的 transition；异步错误双通道（transition task 抛出 + LastAsyncError 兼容保留）；Replace 串联 exit→enter 两段异步。第三轮复查证实 TEngine/BigCat/hsenl 的流程切换全是同步 API，TryGet 借 TGTask 做出更 deep 的设计。Unity EditMode 378/378（新增 ProcedureTransitionTests 10 例）。详见 CHANGELOG「V2.0 — C4」段与 `.scratch/c4-procedure-transition/`。Stop 保持 void（范围收敛，见 issue）。

**必要性**：

当前 ProcedureModule 已有 Stack，但调用者仍要通过 `IsEntering` / `IsExiting` / `LastAsyncError` 理解内部异步状态。这降低 Interface depth。

**范围**：

- 保留现有同步 API。
- 设计 transition result 或 async API。
- 让 Start / Push / Pop / Replace / Stop 可以被 await。
- 明确过渡中再次切换的策略：拒绝、排队、覆盖，三选一。
- 暂不引入完整 FSM 或 ProcedureLine。

**预期收益**：

- 调用者不再轮询内部状态。
- 异步切换错误集中处理。
- Samples/Net 主流程更清晰。

**验证标准**：

- async enter/exit 完成后 transition task 完成。
- pending 时再次切换策略明确且可测。
- Stop 打断 pending transition 可测。
- LastAsyncError 是否仍需要保留需明确。

**优先级**：P2。

---

### Candidate 5 — Generated Registry Diagnostics

**来源参考**：hsenl Attribute registry，Fantasy Source Generator 自动注册思想。

**必要性**：

当前 Source Generator 注册表只是 ApplyAll，出错时不易定位生成了什么、重复注册了什么、是否漏注册。

**范围**：

- `AssemblyManifestRegistry` 增加只读诊断快照。
- `EventHandlerRegistry` 增加 handler 诊断快照。
- 明确重复注册策略。
- 增加 registry 单元测试。
- 不引入运行时反射扫描。

**预期收益**：

- 提高 Source Generator seam 的可观察性。
- 方便未来热更程序集 / 多程序集注册分析。
- 减少自动注册“黑盒感”。

**验证标准**：

- 注册项数量可查询。
- 重复注册行为可测。
- ApplyAll 后不会改变诊断一致性。

**优先级**：P2。

---

### Candidate 6 — Lightweight Pipeline Module

**来源参考**：hsenl ProcedureLine。

**必要性**：

Procedure Stack 适合游戏状态和 UI 叠层，但启动链、登录链、资源初始化链更像有序 pipeline。现在如果全部用 Procedure，会让 Procedure 承担过多职责。

**范围**：

- 只做 Core 纯 C# pipeline。
- Step 顺序执行。
- Context 传递。
- 支持失败短路。
- 支持 TGTask 异步 Step。
- 不引入 worker / handler 复杂体系。

**预期收益**：

- 主流程和业务流水线有独立 seam。
- Procedure Stack 保持状态栈职责。
- 后续启动流程更清晰。

**验证标准**：

- Step 顺序测试。
- 异步 Step 测试。
- 失败短路测试。
- Context 传递测试。

**优先级**：P3。

---

### Candidate 7 — Timer Catch-up Policy（坏帧有界追赶）

**状态**：✅ **已实现**（2026/06/08）

**来源参考**：DGame GameTimer 坏帧追赶（修正其全局预算缺陷）。

**实现摘要**：

- `ITimerModule.ScheduleRepeat` 新增 `maxCatchUp` 参数（默认 0，向后兼容）
- `maxCatchUp = 0`：保持当前行为，单帧只触发一次
- `maxCatchUp = N`：最多补偿 N 次丢失触发
- `maxCatchUp = int.MaxValue`：完全补偿所有丢失触发
- 每个 timer 独立配置，无全局预算限制
- 纯 C# 实现，Shadow csproj 验证通过
- 测试：`Tests/EditMode/TimerModuleCatchUpTests.cs`（6 个新测试）

**解决问题**：技能 CD、buff tick、动画节拍在长帧后不再漂移。

---

### Candidate 7 — Timer Catch-up Policy（坏帧有界追赶）[已归档]

**来源参考**：DGame GameTimer 坏帧追赶（修正其全局预算缺陷）。

**必要性**：

TryGet 当前 `TimerModule` 的 repeating timer 在长帧后**直接合并丢弃漏拍**（`TimerModule.cs:179-196`：到期后 `RemainingSeconds=Interval` 重置只 Invoke 一次）。对依赖固定节拍的逻辑（技能 CD、buff tick、动画节拍）会丢拍。DGame 提供了另一极端（有界补偿），但其上限是所有 loop timer **共享的全局节点访问预算（硬编码 10）**，定时器多时几乎分不到补偿次数，行为难以推理。

**范围**：

- 给 repeating timer 增加 `catch-up` 策略：`Coalesce`（默认，当前行为：合并丢拍）vs `CatchUp`（补偿漏拍）二选一。
- `CatchUp` 模式下每定时器可配 `maxCatchUp` 次数上限（**非** DGame 的全局共享预算），超限丢弃剩余并可选 Warning。
- 纯 Core 算术（基于注入的 dt / IClock），不引入 `System.Timers.Timer` 线程池旁路。
- 评估是否与 Candidate 3 Phase-aware Scheduler 的 `Delay(scaled/unscaled)` 协同。

**预期收益**：

- 固定节拍逻辑不丢拍，行为可预测（每定时器独立预算）。
- 修正了 DGame 全局预算的设计缺陷。

**验证标准**：

- 长帧（dt 远大于 interval）下 `CatchUp` 触发预期次数、`Coalesce` 只触发一次。
- `maxCatchUp` 上限生效、超限不无限补偿。
- Core 不引用 UnityEngine。

**优先级**：P2（可与 C3 一并做，或并入 TimerModule 增强）。

---

### Candidate 8 — Service Scope Layering（服务作用域分层）

**来源参考**：AlicizaX ServiceWorld（App/Scene/Gameplay 三层 + 就近遮蔽 + scope 级 Dispose）。

> ⚠️ **架构级候选，需先决策**：本候选与 ADR-0020「ModuleHost 单一全局容器」定位存在张力。引入前必须先确认是否允许子容器/分层生命周期，否则属架构扩张而非最小改动。建议**等 V2.3 UI / V2.5 场景真实需求出现后**再评估，而非现在投机引入。

**必要性**：

TryGet 当前 `ModuleHost` 是单一全局容器，所有 Module 同生命周期。但 UI（V2.3）/ 场景（V2.5）有真实的**作用域生命周期**需求：进入某场景时注册一批场景级服务，离开时整批释放；切换场景时这些服务应自动 Dispose 而非常驻。AlicizaX 用 App/Scene/Gameplay 三层 scope + 就近遮蔽（窄作用域覆盖宽作用域）+ scope 级逆序 Dispose 干净地表达了这一需求。

**范围**（若决策通过）：

- 在 ModuleHost 之上或之内引入有限层级的 scope（如 App 常驻 + Scene 可创建销毁），**不做任意嵌套**。
- 服务解析就近遮蔽：窄 scope 命中优先。
- scope 级生命周期：scope Dispose 时逆序 Shutdown 其内服务。
- 保留现有 DependsOn 拓扑排序（**不得削弱**，AlicizaX 无拓扑是其短板）。
- 不引入 AlicizaX 的静态全局 `AppServices` 单例与 Mono 驱动，保持显式 host 注入。

**预期收益**：

- 场景/UI 服务生命周期清晰，切场景自动释放。
- ModuleHost Interface 更 deep（承载分层而非让业务自管）。

**验证标准**：

- scope 内注册/解析/就近遮蔽测试。
- scope Dispose 逆序销毁测试。
- 拓扑排序与循环检测在分层下仍生效。
- Core 不引用 UnityEngine。

**优先级**：P2-P3（**前置决策门**：先定 ADR 是否允许分层容器；不通过则不做）。

---

### Candidate 9 — Pool / Timer Diagnostics（池与定时器诊断快照）

**来源参考**：AlicizaX GameObjectPool 诊断 + generation-token；DGame MemoryCollector / GameObjectPool（双印证）。

**必要性**：

TryGet `IObjectPool` 仅暴露 `IdleCount`（`IObjectPool.cs:22-25`），重复 Return 是未定义行为（注释把双释放检测推迟到 V0.3+ 可选 DEBUG）。出现真实泄漏/性能诊断需求时缺可观测性。AlicizaX 与 DGame 都提供了成体系的诊断快照（命中/未命中/峰值/容量）+ 重复释放检测；AlicizaX 的 `generation-token`（回收时三重校验 token+state）是比 HashSet Contains 更规范的双释放检测方案。

**范围**：

- `IObjectPool`/`IPoolModule` 增加只读诊断快照（acquire/release/hit/miss/peak/容量/idle），调用方传数组零分配填充。
- 可选 generation-token 重复释放检测（纯整数 generation 计数，无 Unity 依赖）。
- 不引入 AlicizaX 的分页 slot / maintenance 堆 / Unity GameObjectPool（强绑 Unity，属 Adapter）。
- 注意：DGame `MemoryCollector.ReleaseCount--` 疑似符号 bug，勿照抄。

**预期收益**：

- 池可观测，便于定位泄漏与命中率问题。
- 重复释放从「未定义行为」变为「可检测」。

**验证标准**：

- 诊断计数快照准确（spawn/release/hit/miss）。
- 重复 Return 被 token 检测拒绝。
- Core 不引用 UnityEngine。

**优先级**：P3（仅在真实诊断需求出现后引入）。

---

### Candidate 10 — SourceGen Diagnostics（生成器诊断友好）✅ **已完成（2026/06/13）**

**来源参考**：AlicizaX UIMetaSourceGenerator 的 `ReportDiagnostic`。

> 实施记录：诊断 TG0001-TG0006 落地于 `Tools/MyTryGetFramework.SourceGenerator/src/Diagnostics/GeneratorDiagnostics.cs`；同迭代把生成器从「孤立 dll」确立为「源码工程一等公民」（专属 sln + GeneratorDriver 测试工程 + 一键构建脚本 + 文档），并修复了被 dll 黑盒掩盖的 `AssemblyManifestRegistry`→`ModuleRegistry` 命名漂移 bug。生成器单测 12/12、Unity EditMode 368/368 全绿。详见 CHANGELOG「V2.0 — Source Generator 工程化 + C10」段与 `Tools/README.md`。C12（框架契约 Analyzer）可在此基建上续作。

**必要性**：

TryGet 的 `EventHandlerGenerator`/`ModuleManifestGenerator` 对非法输入（非 static、签名不符、未实现接口）一律 `return null` **静默丢弃**（`EventHandlerGenerator.cs:43-49`），开发者写错 `[Module]`/`[EventHandler]` 时**无任何编译期反馈**，只能在运行时发现「没注册上」。AlicizaX 对开放泛型等非法输入 `ReportDiagnostic(带 Location)`，IDE 直接红线提示。

**范围**：

- 给两个 Generator 对非法输入 `ReportDiagnostic`（带 Location，分配诊断 ID 如 TG001/TG002）替代静默 return null。
- 覆盖：`[Module]` 标在非 IModule 类、`[EventHandler]` 方法签名不符、标在非 static 等。
- 纯 tooling（Roslyn GeneratorDriver + Diagnostics 断言可验证），不进 Core。

**预期收益**：

- 自动注册不再是黑盒，写错即时报错。
- 降低「以为注册了其实没注册」的排查成本。

**验证标准**：

- 非法输入触发预期诊断 ID + Location。
- 合法输入无误报。
- GeneratorDriver 单测覆盖。

**优先级**：P2（投入小、收益直接）。

---

### Candidate 11 — TGTask Pooling Lifecycle Closure（池化生命周期收口：完成即失效 + 消费侧归还）✅ **已完成（2026/06/13）**

> **实施时设计调整**：草案中的「SetResult/SetException 时 Version++（完成即失效）」评审后未采用——它会破坏已钉住的 tcs TrySet 静默语义并引入 version+1 算术脆弱性。实际落地为「消费侧统一归还（Builder+Manual）+ 全路径 version 校验 + tcs 对象池化」，达成同等安全性，全部既有行为兼容（Unity EditMode 368/368）。详见 CHANGELOG「V2.0 — C11」段与 `.scratch/c2-c11-hardening/issues/02`。

**来源参考**：hsenl HTaskCompletionBody（完成瞬间 `IncrementVersion` + `GetResult` finally `ReturnBody`，`HTaskCompletionBody.cs:39-83`）+ BigCat `ValuePromise<T>.Acquire(out int rid)` 池化 promise + 回收令牌（`TimerMgr.cs:44-56`）。**双印证**。

**必要性**：

6ter.3 自审坐实三项关联技术债：① `TGTask.IsCompleted` 不校验 Version（`TGTask.cs:50-54`），body 复用后旧句柄读到他人状态；② Manual 型 body 依赖显式 `Return()` 否则漏给 GC（`TGTaskCompletionSource.cs:57-66`）；③ TGTaskScheduler 热路径每次 Yield/Delay `new TGTaskCompletionSource()` 且 SetResult 后无法安全归还（`TGTaskScheduler.cs:146,160,180,260-267`）——池化在最热路径完全失效。根因相同：**TryGet 的 Version 在 Reset（归还）时才递增，归还时机无人能定**；hsenl 的答案是把失效提前到完成瞬间，归还统一放到消费侧。

**范围**：

- `TGTaskBody.SetResult/SetException` 时 Version++（完成即失效）；`GetResult` 改为按快照 version 校验。
- `Awaiter.GetResult` 的 finally 归还**统一覆盖 Builder 与 Manual 型**；`Forget` 路径同步调整。
- `IsCompleted`（TGTask 与 Awaiter）补 Version 校验：`Version != Body.Version` 视为已完成（过期=已消费）。
- `TGTaskCompletionSource` 池化（对象本身可复用，Acquire/内部回收），删除公开 `Return()` 或降级为 no-op 兼容；TGTaskScheduler 三类队列改用池化 tcs。
- 无人 await 且异常的路径对齐 hsenl：进 `UnobservedException` 钩子后立即归还 body。
- **不取** hsenl 的 lock/ManualResetEventSlim 线程同步（单线程模型）。

**预期收益**：

- 热路径（每帧 Yield/Delay）回到零稳态分配，池化真实生效。
- 旧句柄误读他人状态从「隐患」变为「确定抛 Expired/视为完成」。
- 删除「何时该调 tcs.Return()」这一无法回答的 API 心智负担。

**验证标准**：

- 完成后旧 TGTask 句柄的 IsCompleted/GetResult 行为有明确定义并有测试。
- Yield/Delay 循环 N 帧后池命中率 100%（无新 body/tcs 分配）。
- 现有 350 个 EditMode 测试全绿（语义兼容）；ServerProject shadow csproj 可编译。

**优先级**：**P1**（修复已知最高优先级技术债，且是 V2.1 事件升级前的地基）。

---

### Candidate 12 — Framework Contract Analyzer（Roslyn Analyzer 框架契约编译期执法）

**来源参考**：MyFramework `AnalyzerUnity`（RESET001 池化类 resetProperty 必须重置全部实例字段、BASE001 override 必须调 base，均 Error 级，`ToolProject/AnalyzerUnity/AnalyzerUnity/AnalyzerResetProperty.cs:10-16`、`AnalyzerCallBase.cs:9-15`）+ BigCat 自带 `csharp/Analyzers/`。**双印证**。

> 与 C10（SourceGen 对非法输入 ReportDiagnostic）同属编译期诊断，但方向相反：C10 管「生成器输入合法性」，本候选管「业务代码遵守框架契约」。两者共享 Roslyn tooling 管线，可同一迭代实施。

**必要性**：

TryGet 的框架契约目前只靠文档和运行时异常兜底：事件类型必须是 struct（违者编译器报泛型约束错误，但提示晦涩）、`IObjectPool` 的 Reset 委托应清干净状态（漏字段=池化对象状态残留，最难排查的一类 bug）、`[Module]` 类的服务接口注册约束等。MyFramework 证明了把这类契约做成 Error 级 Analyzer 后，整类 bug 在编译期消失。TryGet 已有 SourceGenerator 工程（`Runtime/Core/Generators/`），追加 Analyzer 是同管线增量成本。

**范围**（首批规则按真实痛点裁剪，宁缺勿滥）：

- TG-A001：池化对象的 Reset 路径未覆盖全部实例字段（对标 RESET001；具体绑定形态待 C9/池 API 定型后细化）。
- TG-A002：`[EventHandler]` 方法签名/static 约束（与 C10 生成器诊断互补，Analyzer 在 IDE 即时红线）。
- TG-A003：Core 程序集内 `using UnityEngine`（把 Shadow csproj 的事后验证提前到 IDE）。
- GeneratorDriver/AnalyzerVerifier 单测覆盖；规则文档化（ID/severity/理由）。

**预期收益**：

- 框架契约从「文档约定」升级为「编译期执法」，违约成本前移。
- ADR-0012 纯 C# 边界获得 IDE 即时反馈，不再依赖 CI 阶段 shadow build 才发现。

**验证标准**：

- 违规代码触发预期诊断 ID + Location；合法代码零误报。
- Analyzer 单测（Microsoft.CodeAnalysis.Testing）全绿。
- 不进 Core 运行时（纯 tooling）。

**优先级**：P2（与 C10 同迭代实施摊薄成本）。

---

## 9. 推荐迭代顺序

推荐顺序：

1. **P0 — FrameLoop Module** ✅ **已落地（2026/05/30, 0bfc33f）**
   底层时序语言已建立，ModuleHost 5 阶段派发 + 执行表分桶完成。

2. **P1 — EventBus Lifecycle Policy + 零 GC（Candidate 2，强化）**
   消除 Publish 的 `list.ToArray()` 分配，吸收 AlicizaX PendingChange / DGame EventDelegateData 的重入安全延迟增删（双印证），**并补上两个框架都缺的 handler 异常隔离**。这是当前最高价值、有真实 GC 靶点的项。

3. **P1 — Phase-aware TGTaskScheduler（Candidate 3）**
   FrameLoop 已落地解锁此项；扩展 Yield/Delay 支持 phase + scaled/unscaled。可与 Candidate 7 协同。

4. **P2 — Timer Catch-up Policy（Candidate 7，新增）**
   修正 repeating timer 丢拍，每定时器可配 maxCatchUp。可并入 TimerModule 增强或随 C3 一起做。

5. **P2 — SourceGen Diagnostics（Candidate 10，新增）**
   投入小收益直接：让自动注册非法输入编译期报错，不再静默丢弃。

6. **P2 — Procedure Transition Result（Candidate 4）**
   清理 Procedure 异步状态泄漏，让流程切换更 deep。

7. **P2 — Generated Registry Diagnostics（Candidate 5）**
   提升自动注册运行时可观察性（与 C10 编译期诊断互补）。

8. **P3 — Lightweight Pipeline Module（Candidate 6）**
   在有真实启动链 / 登录链需求时引入。

9. **P3 — Pool / Timer Diagnostics（Candidate 9，新增）**
   真实诊断需求出现后，吸收诊断快照 + generation-token 重复释放检测。

10. **前置决策门 — Service Scope Layering（Candidate 8，新增）**
    架构级，与「ModuleHost 单一容器」有张力。等 V2.3 UI / V2.5 场景真实需求出现、并先定 ADR 是否允许分层容器后再评估。

### 9bis. 第三轮后的推荐顺序（2026/06/13 更新）

C1/C3/C7 已落地；6ter.3 自审发现 C2 已基本完成（仅剩收尾）。当前推荐：

1. **P1 — C2 收尾（小）** ✅ **已完成（2026/06/13，同日实施）**：HandlerException 上报钩子 + MaxPublishDepth=32 递归护栏 + 关单，EditMode 368/368 验证。
2. **P1 — C11 TGTask 池化生命周期收口（本轮主菜）** ✅ **已完成（2026/06/13，同日实施）**：消费侧统一归还 + 全路径 version 校验 + tcs 池化（设计调整见 Candidate 11 注记），调度器热路径稳态零分配。
3. **P2 — C10 SourceGen 诊断** ✅ **已完成（2026/06/13）**：生成器源码工程化（专属 sln + 测试工程 + 构建脚本 + 文档）+ TG0001-TG0006 编译期诊断 + 修复命名漂移 bug。（生成器源码后续已迁入框架内 `Generators~/`，对标 Unity Netcode Source~。）
4. **P2 — C4 Procedure Transition Result** ✅ **已完成（2026/06/13）**：Start/Push/Pop/Replace 返回可 await transition，消除 IsEntering/IsExiting/LastAsyncError 轮询；比参考框架（全是同步切换）更 deep。**C5 Registry 诊断**顺位不变。
5. **P2 — C12 框架契约 Analyzer ← 下一个迭代起点**：在 C10 建好的 Roslyn tooling 基建上续作（对标 MyFramework AnalyzerUnity 的编译期契约执法）。
6. **P3 — C9（已获 MyFramework ClassPool 蓝本强化）、C6**：等真实需求。
7. **决策门 — C8**：不变，等 V2.3/V2.5。
8. **观察项（不立候选）**：owner-scope 异步/定时器批量取消（MyFramework DelayCmdWatcher 思想）、Module 异步初始化段与 destroy 顺序解耦（V2.4 资源 Adapter 期再评估）、TGTaskScheduler 帧边界启发式误判（C11 实施时审视过，保持现状：真实 Unity 接入时由 ModuleSystem 显式通知帧首再修）。

---

## 10. 暂不进入任务池的能力

以下能力虽然参考框架中很强，但本阶段暂不纳入底层 Core 实现：

| 能力 | 暂缓原因 |
|---|---|
| TEngine YooAsset 全量资源系统 | 需要 Unity Adapter / 内容管线，当前暂缓 |
| TEngine HybridCLR / Obfuz | 会绑定构建链，当前 Core 未到此阶段 |
| TEngine UI 代码生成 | 依赖 UI 框架和资源系统 |
| hsenl Entity / Component | 与 ADR-0020 当前路线冲突 |
| hsenl 双端 Network / RPC | 当前只做客户端网络契约 |
| hsenl EventSystem 热重载程序集 | 复杂且有 Unity 多程序集残留风险 |
| BigCat Node / Worker / Disruptor | 双端/服务端复杂度过高 |
| BigCat S2S RPC / 服务路由 | 当前不做服务端框架 |
| DGame Unity UI / Input / Anim / GMPanel / SuperScrollView | 强绑定 Unity Runtime 与具体业务 UI |
| DGame YooAsset / HybridCLR / Luban / Editor 工具链 | 会提前绑定资源、热更和构建链 |
| DGame 静态 ModuleSystem / 弱类型 EventDispatcher | 与当前 typed EventBus 和实例化 Host 方向不一致 |
| AlicizaX RootModule / AppServiceRoot / MonoServiceBehaviour | MonoBehaviour 驱动层，Core 禁用 UnityEngine，只能进 Adapter |
| AlicizaX AppServices 静态全局单例 | 隐式全局状态，与 TryGet 显式 host 注入冲突 |
| AlicizaX Cysharp.Text(ZString) / UniTask 依赖 | Core 零外部依赖原则，移植须改 string.Format / TGTask |
| AlicizaX GameObjectPool / UI SourceGen 产物 | 强绑 UnityEngine/UnityEditor，属 UI Adapter（V2.3） |
| AlicizaX Register 多契约绑定（TContract+TExtraContract） | 超出 ADR-0020 最小化范围 |
| Dgame/Dgame（D 语言 2D 框架） | 非 Unity / 非 C# / 2019 归档，与 TryGet 完全无关 |

---

## 11. 未来新增参考框架分析模板

后续如果加入新的参考框架，请按以下模板分析，避免只看 README 或凭印象判断。

### 11.1 基本信息

| 项 | 内容 |
|---|---|
| 框架名称 |  |
| 源码路径 |  |
| 定位 | 纯客户端 / 双端 / 服务端 / 工具链 |
| 是否依赖 Unity |  |
| 是否有 ECS / Entity 模型 |  |
| 是否有服务端 / 网络模型 |  |
| 是否有热更 / 资源工具链 |  |

### 11.2 必读源码点

至少读取：

- Entry / Launcher。
- Module / Service / Manager 基础抽象。
- 生命周期与 Update 调度。
- Event / Message 系统。
- Async / Timer / Coroutine。
- Procedure / FSM / Flow。
- Resource / Config。
- UI / Scene / Audio。
- Network。
- Hotfix。
- Editor / Build 工具。
- Tests / Samples。

每个结论必须附真实文件路径和行号。

### 11.3 可吸收判断

每个设计点必须判断：

| 判断项 | 问题 |
|---|---|
| 解决什么问题 | 它解决的是 TryGet 当前真实问题吗？ |
| 是否符合 ADR-0020 | 是否会带回 ECS / 服务端 / Plugin？ |
| 是否能纯 C# 验证 | 是否需要 Unity Runtime？ |
| Interface 是否更 deep | 是否提高 leverage，而不是增加调用复杂度？ |
| Implementation locality | 是否把复杂度集中在一个 Module 内？ |
| 引入成本 | 影响文件和测试规模多大？ |
| 替代方案 | 是否有更小实现？ |

### 11.4 输出格式

新增框架分析输出应包含：

1. 关键源码证据。
2. 设计思想。
3. 可吸收点。
4. 不吸收点。
5. 与当前 Candidate 的关系。
6. 是否需要新增 Candidate。

---

## 12. 当前结论

本轮源码级分析后，最值得优先吸收的是：

1. BigCat 的单线程外部驱动 EventLoop / 多阶段 Update。
2. TEngine 的 Update 执行表缓存和事件 owner-clear 生命周期。
3. hsenl 的注册目录诊断和轻量 Pipeline 思想。
4. DGame 的 GameTimer catch-up policy、Pool 诊断和商业客户端数据服务样板。

最不应该吸收的是：

1. hsenl 的完整 Entity / Component。
2. BigCat 的 Node / Worker / RPC。
3. TEngine / DGame 的静态全局 ModuleSystem。
4. DGame 的弱类型 int eventId 事件派发。
5. 任何会提前绑定 Unity 运行时、热更、资源打包工具链的实现。

下一轮如果进入实现，建议从 **Candidate 1 — FrameLoop Module** 开始，因为它最符合当前 V2 Core 目标，且可以不接 Unity Runtime、纯 C# 验证。

### 12bis. 第二轮（AlicizaX / DGame 复核，2026/05/30）增量结论

Candidate 1 FrameLoop 已于 2026/05/30（提交 0bfc33f）落地。第二轮分析新增/强化的可吸收点：

最值得优先吸收（均符合 ADR-0020、纯 C# 可验证）：

1. **EventBus 零 GC + 重入安全延迟增删（强化 C2）**——AlicizaX `ServiceScope` PendingChange 与 DGame `EventDelegateData` **双印证**同一模式，对症 TryGet `Publish` 每次 `list.ToArray()` 分配。**关键：两个参考框架都不做 handler 异常隔离（DGame 抛异常会卡死延迟队列），TryGet 的 C2 要在吸收延迟增删的同时补上异常隔离——这是超越参考的设计点。**
2. **SourceGen 编译期诊断（新增 C10）**——AlicizaX `ReportDiagnostic` 思路，对症 TryGet 生成器对非法输入静默 return null。投入小收益直接。
3. **Timer 有界坏帧追赶（新增 C7）**——DGame catch-up 思想，但改为每定时器可配 maxCatchUp，修正其全局共享预算缺陷。

谨慎/延后：

4. **服务作用域分层（新增 C8）**——AlicizaX ServiceWorld 三层 scope，是 TryGet 缺失且对应 V2.3/V2.5 真实需求的能力，但与「ModuleHost 单一容器」定位有张力，**设为前置决策门**：先定 ADR 是否允许分层容器，等 UI/场景真实需求出现再评估。
5. **池诊断 + generation-token（新增 C9）**——AlicizaX/DGame 双印证，低优先，真实诊断需求出现后再做。

实现纪律重申：AlicizaX 的 Mono\* 驱动层 / AppServices 静态单例 / Cysharp.Text 依赖、DGame 的静态 ModuleSystem / 弱类型 int eventId 均为红线，吸收时只取纯 C# 机制、剥离 Unity 与第三方依赖、保持 ServerProject shadow csproj 可验证。

**第二轮推荐起点**：进入实现时从 **Candidate 2（EventBus 零 GC + 生命周期，含异常隔离）** 开始——它是 P1、有真实 GC 靶点、双框架印证了实现模式、且 FrameLoop 已为其铺好基础。

### 12ter. 第三轮（MyFramework 首析 + 自审 + 定向复查，2026/06/13）增量结论

1. **C2 的主体已在 v2.0-route-c-landing 分支落地**（延迟增删/重入安全/异常隔离/零 ToArray 均有实现与测试），第二轮「从 C2 开始」的建议已被执行过半；剩余为异常上报策略 + 递归深度护栏 + 关单，半天量级。
2. **本轮最高价值发现是 C11（TGTask 池化生命周期收口）**：hsenl 的「完成即失效 + 消费侧归还」与 BigCat 的「池化 promise + 回收令牌」双印证了同一答案，直接对症 TryGet 三项关联技术债——这是当前唯一的 P1 级架构债，建议作为下一个实现迭代的主体。
3. **MyFramework 的独特贡献不在运行时而在工程化**：Roslyn Analyzer 契约执法（C12，与 BigCat 双印证）、池泄漏检测三件套（强化 C9）、事件递归护栏（并入 C2）。其运行时设计（CommandSystem/静态单例/树形流程/弱类型事件）全部确认不吸收——它是「同一问题域的反方向解」，价值在于校准 TryGet 的取舍而非提供实现。
4. 修订后的近期路径：**C2 收尾 → C11 → C10+C12 → C4/C5**。V2.3 UI 期再启用 TEngine UIModule 契约样板与 C8 决策门。
