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

### Candidate 2 — EventBus Lifecycle Policy

**来源参考**：TEngine GameEvent owner-clear，hsenl EventSystem 注册目录。

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

### Candidate 4 — Procedure Transition Result

**来源参考**：TEngine FSM transition 封装，hsenl ProcedureLine 的流程结果思想。

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

## 9. 推荐迭代顺序

推荐顺序：

1. **P0 — FrameLoop Module**  
   先建立底层时序语言，为后续所有 Module 提供统一 phase。

2. **P1 — EventBus Lifecycle Policy**  
   提前解决订阅生命周期，避免 UI / Procedure 后续泄漏。

3. **P1 — Phase-aware TGTaskScheduler**  
   等 FramePhase 稳定后扩展 Scheduler。

4. **P2 — Procedure Transition Result**  
   清理 Procedure 异步状态泄漏，让流程切换更 deep。

5. **P2 — Generated Registry Diagnostics**  
   提升自动注册可观察性。

6. **P3 — Lightweight Pipeline Module**  
   在有真实启动链 / 登录链需求时引入。

7. **P3 — Timer / Pool Diagnostics**  
   只在真实性能与生命周期诊断需求出现后，从 DGame 的 GameTimer / Pool 思想中吸收 catch-up policy、容量、重复释放检查和诊断快照。

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
