# TryGet V2 战略方向回切（V0.5 → V0.6+ 路线重定向）

> **撰写日期**：2026/05/24
> **状态**：方向回切提案，等待用户 review
> **作用**：基于用户最新明确的"商业框架基础架构"定位，对 V0.4-V0.6 已做工作做去留决策，并定义 V0.6+ 新路线图。
>
> **本文不动代码**。所有"抛弃 / 重构"决策需用户 review + 接受后才进入实施阶段。

## 1. 触发原因

V0.4-V0.6 实际产出（30+ commits）大量集中在 Unity Adapter（Audio/Input/Save/UI/Scene）+ PlayMode 测试基建 + Adapter 综合 demo + Adapter 契约 ADR。用户 V0.6 Iter 3（TryGetBootstrap.cs）期间明确指出：

> "我暂时其实不需要你去给我扩展什么 Network YooAsset 热更新等，我需要的是**商业框架的基础架构**，其余 Unity 内的内容其实都算是业务内容，这些可以后续扩展的不是么？先重新理清，是按 **TEngine, hsenl, et, fantasy 等**的架构路线，然后你出文档内容，根据我的 claude code 工作流，后续再实际开始代码实现，目前的内容如果你评估后可以抛弃就抛弃。"

**核心反馈**：方向偏离——Unity Adapter 是业务/扩展，不是框架基础架构。

## 2. 对标研究（4 框架决定性架构选择）

### 2.1 ET — 双端共码的标杆

5 个决定性架构选择：

1. **数据-行为分离的 ECS**：`Entity` 是身份+组合宿主，`Component` **只放字段无方法**，行为靠 `IAwake/IStart/IUpdate` 等 System interface 的"Event 类"通过事件分发挂上去
2. **Fiber + ETTask 协程**：自创 `ETTask`（**非** UniTask），抽象 Fiber 概念，单线程开发体验但利用多核；ET9 起 ETTask 支持上下文传递，**取消显式 CancellationToken**
3. **Actor + Location Server**：任意 Entity 挂 `MailBoxComponent` 即成 Actor，Location Server 路由，Gate/Scene/Game 跨进程位置透明
4. **All-in-One 双端**：`ET.Model` / `ET.Hotfix` 是共享 dll；服务端项目"引用"客户端代码（csproj include / 自动生成）；`ENABLE_VIEW` define 隔离 Unity API
5. **Luban 配置**：`cn.etetet.yiuiluban` 包替代旧 excel 方案，大规模数据驱动

**借鉴 → TryGet**：数据/行为分离 + 事件挂载比 TryGet 当前 `Aspect`（包行为）的纪律更纯粹；双端机制比 Shadow csproj 更工程化（共享物理 dll 而非反向引用源文件）
**避免 → TryGet**：Fiber/Actor 是 MMO 级复杂度，**商业框架基础架构不必一上来就做**

### 2.2 Fantasy — ET 的演进版（Source Generator-first）

5 个决定性架构选择：

1. **Source Generator 全家桶**：`Fantasy.SourceGenerator.csproj` 编译期生成 `MessageHandlerGenerator`、`AwakeSystem<T>/UpdateSystem<T>/DestroySystem<T>` 注册器、`SceneType/DatabaseName` 常量、`OpCode.cs` — **零运行时反射、Native AOT 友好**
2. **"一切皆 Entity"的层级化树**：`Scene` 是特殊 Entity，作为容器；Entity 可嵌 Entity 形成树；父销毁级联销毁子；内置对象池
3. **多协议网络抽象**：单一 `Session`，`NetworkProtocolType` 枚举切换 TCP/KCP/WebSocket/HTTP
4. **FTask 替代 Task**：明确规定"all async ops use FTask, not Task"，自研而非用 UniTask
5. **入口分两路**：Unity 用 `[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]` 自动跑 SourceGen 注册器；.NET 用 `[ModuleInitializer]`

**借鉴 → TryGet**：`[ModuleInitializer]` + Source Generator 做 System 自动注册，能干掉 TryGet 现在所有手动 `host.Register<>()`；双入口（Unity / .NET）是真正的双端 bootstrap 解
**避免 → TryGet**：Source Generator 投入大，**V0.6 暂不上**，放 V0.9 路线

### 2.3 TEngine — Unity 商业框架代表

5 个决定性架构选择：

1. **`ModuleSystem` + `GameModule` facade**：静态服务定位器 + 缓存外观类；`Module` 基类 `OnInit/Shutdown`，`IUpdateModule.Update(float,float)`；`Priority` 决定更新顺序；`RootModule` MonoBehaviour 驱动主循环
2. **Procedure FSM 启动流**：`ProcedureLaunch → ProcedureInitPackage → ProcedureLoadAssembly`，反射调 hot-fix `GameApp.Entrance`
3. **YooAsset + Luban + UniTask + HybridCLR 全套**：业界主流商业组合
4. **零 GC GameEvent**：int/string 双 ID 事件总线 + 接口事件类型安全；UI panel 通过 `GameEventMgr` 自动解绑
5. **服务端复用能力 = 0**：`ResourceModule/UIModule` 直接依赖 `UnityEngine`（`TextAsset` 等）；**TEngine 不支持纯 .NET 服务端**

**借鉴 → TryGet**：Module + Priority + Facade 的总体架构与 TryGet ModuleHost **几乎同构** — 验证 TryGet 当前 Core 设计方向正确；Procedure FSM 启动流（已有，可深化）
**避免 → TryGet**：TEngine 把 Unity 写进了 Core — **这正是用户要避免的反模式**；Facade 静态类污染跨端纪律，不应跟

### 2.4 Hsenl — ET 风格但更轻量

Hsenl（`gitee.com/dcze/hsenl`，MIT，C# 99.9%，14 stars）定位"灵活高速的游戏双端框架，适用于 unity，也适用于做服务器开发"，沿 ET 谱系但更精简。

**结论**：Hsenl 作为对标价值低于 ET/Fantasy。建议**降级为参考素材**，把 ET/Fantasy 作为 TryGet 的主对标。如需深度对标 Hsenl，需手工 clone 源码人工读。

## 3. TryGet 现状审视（5 维度对照）

| 维度 | TryGet 现状 | ET/Fantasy 标杆 | 差距 |
|---|---|---|---|
| **Entity-Component 模型** | `Entity + Aspect`（有方法、ADR-0007 隔离）+ `Tag` + `BitArray256` 位掩码 | ET：Component 只字段无方法；Fantasy：Entity 树 + Component 也是 Entity | TryGet 的 Aspect=有方法小 OOP，**与 ET 谱系背道而驰**，需评估转向 |
| **协程 / 异步** | **完全空白** — 既无 ITask 抽象，也无 UniTask 引用 | ETTask（pool + ctx）/ FTask | **最大缺口**。商业框架基础架构无异步原语等于残废 |
| **配置加载** | `IConfigModule + MemoryConfigModule` 纯 KV 骨架 | Luban code-gen 类 + 二进制 + 热加载（ET/TEngine）；自研 XML/JSON（Fantasy） | 缺类型化配置类、缺二进制加载、缺热更新 |
| **事件系统** | `IEventBus`（全局）+ `IWorldEventBus`（Entity 范围）+ `EntityEventDispatcher` 三层 | Fantasy SourceGen 注册；ET 零 GC + Event 类 hot-update | 反射开销未优化；订阅按 `List<Action<T>>` 同步派发 |
| **跨端纪律** | Shadow csproj 反向引用 + `dotnet build` 验证 | ET：`ET.Model` 物理共享 dll + `ENABLE_VIEW` define；Fantasy：双 Platform.Entry | "可编译"≈"双端"，**未验证业务层真的双端 ready**；无双入口示范 |

**冗余识别**：
- 5 个 Unity Adapter（V0.5 Iter 6-10）= Unity 业务适配，**对标 4 框架全部把这些视为业务层**
- TryGetBootstrap.cs（V0.6 Iter 3 未 commit）= Unity MonoBehaviour 启动样例，框架基础架构不该提供，留给项目模板
- UnityFlowDemoPlayModeTests = Adapter 综合 demo，与 Adapter 共存亡
- ADR-0014 / ADR-0015 = 围绕 Adapter 展开，Adapter 退场则需重写

## 4. TryGet V2 新方向设计

### 4.1 核心架构边界（必答的关键问题）

**框架（Core，跨端必有，不可抛弃）**：
1. `ModuleHost` + `IModule` + `DependsOn` 拓扑序
2. `IEventBus` 全局 + `IWorldEventBus` Entity 范围
3. `EntityWorld` + `Entity` + `Aspect` + `Tag` + `Phase` + `Query` + `System`
4. `IProcedureModule` 状态机
5. `BitArray256` + `TypeIndex<T>` 索引基础设施
6. **V0.6+ 新增**：`ITask` 异步原语、`ILogger`、`IConfigSource` 类型化配置、`ISerializer`、`IClock`

**扩展（不强求跨端，独立仓库 / Samples/ 目录）**：
- 所有 Unity Adapter（Audio/Input/Save/UI/Scene）
- Network（KCP/TCP/WebSocket Adapter）
- 热更（HybridCLR Loader）
- YooAsset / Luban 集成
- TryGetBootstrap MonoBehaviour 启动样例

**边界判定原则**：
> Core = 在没有 Unity 的 dotnet 终端里也能跑完整流程（含异步、状态机、Entity 行为、事件、配置加载、序列化）。任何"需要看屏幕、听声音、读 PlayerPrefs"的能力**统统不在 Core**。

### 4.2 V0.5 已做内容的去留决策表

| 文件 / 模块 | 决策 | 理由 |
|---|---|---|
| `Module/`（ModuleHost / IModule / IEventBus / WorldEventBus） | **✓ 保留** | 与 TEngine ModuleSystem 同构，商业框架基础架构核心 |
| `Common/BitArray256.cs / TypeIndex.cs` | **✓ 保留** | 性能基础设施 |
| `Common/ILogModule / ConsoleLogModule` | **✓ 保留**（V0.7 重命名为 `ILogger + ConsoleLogger`） | 跨端必有 |
| `Common/ITimerModule / TimerModule` | **✓ 保留** | 跨端必有 |
| `Common/IPoolModule / PoolModule / IObjectPool` | **✓ 保留** | 跨端必有 |
| `Common/IProcedure / IProcedureModule / ProcedureModule` | **✓ 保留** | 商业框架核心：启动流 FSM |
| `Common/IResourceModule + MemoryResourceModule` | **重构为 ISerializer + IAssetSource**（V0.8） | 当前 IResourceModule 假设 Unity-flavor"asset"概念 |
| `Common/IUIModule + MemoryUIModule` | **✗ 抛弃**（移到 Samples/ 或独立仓库） | UI 是业务 |
| `Common/ISaveModule + MemorySaveModule` | **重构为 IKeyValueStore**（V0.8） | KV 存储是基础，但"Save"语义业务化太强 |
| `Common/ILocalizationModule + MemoryLocalizationModule` | **降级为可选模块**，移到 `Optional/` 子目录 | 不是核心 |
| `Common/IAudioModule + MemoryAudioModule` | **✗ 抛弃** | Audio 是业务 |
| `Common/IInputModule + MemoryInputModule` | **✗ 抛弃** | Input 是业务 |
| `Common/IConfigModule + MemoryConfigModule` | **重构为 IConfigSource + ConfigLoader<T>**（V0.8） | KV 接口太弱，对标 Luban 应支持类型化表 |
| `Common/ISceneModule + MemorySceneModule` | **✗ 抛弃** | Scene 是 Unity 业务 |
| `Entity/`（Entity / Aspect / Tag / Phase / EntityWorld） | **✓ 保留**，V0.7+ 评估 Aspect 是否走向纯数据 Component | 当前 ADR-0007 让 Aspect 持方法，与 ET 风格相悖；渐进式调整 |
| `Unity/Audio/UnityAudioModule.cs` 等 5 Adapter | **✗ 抛弃**（迁出到独立仓库 `TryGet.UnityAdapters` 或 `Samples/`） | 业务 |
| `Tests/PlayMode/*PlayModeTests.cs`（62 测试） | **跟随 Adapter 退场到 Samples/** | 与 Adapter 共存亡 |
| `Tests/PlayMode/MyTryGetFramework.Tests.PlayMode.asmdef` | **保留为空骨架** | 留作未来真 Adapter 需要时的脚手架 |
| `Runtime/Unity/TryGetBootstrap.cs`（V0.6 Iter 3 未 commit） | **✗ 立即冻结，删除未 commit 文件** | 用户已明确指出方向偏离 |
| `UnityFlowDemoPlayModeTests`（V0.6 Iter 1） | **✗ revert 或移到 Samples/** | 围绕 5 Adapter 综合测试 |
| `ADR-0014`（Network/HotReload Adapter） | **✗ 标记 Superseded**，等 Network 真做时重写 | 围绕过早设计 |
| `ADR-0015`（Adapter 契约偏离白名单） | **✗ 标记 Superseded** | Adapter 退场后此 ADR 没有承载对象 |

**EditMode 测试（200+）**：保留所有 Core 测试，**移除** Resource/UI/Save/Audio/Input/Scene/Localization 的 EditMode Memory 实现测试（约 60 个），它们围绕将要被重构 / 抛弃的接口。

### 4.3 V0.6+ 新路线图

#### V0.6 — 异步原语 ITask（最致命缺口）
- **目标**：Core 获得跨端 async 能力，不依赖 `System.Threading.Tasks.Task` 的线程池
- **文件**：`Core/Async/ITask.cs`、`ITaskCompletionSource.cs`、`TaskScheduler.cs`、`AsyncProcedure.cs`
- **对标**：参考 ETTask 状态机池化，但**简化为单线程模型**（不做 Fiber），保留 ctx 传递避免 CancellationToken 污染
- **测试**：~30 个测试覆盖 await/continuation/pool/cancellation/timeout
- **范围**：Core only，无 Unity 依赖

#### V0.7 — 双端入口规范 + Logger / Clock 基础服务
- **目标**：明确"如何在 dotnet console 启动 TryGet"和"如何在 Unity 启动 TryGet"
- **文件**：`Core/Bootstrap/ITryGetEntry.cs`、`Samples/Net/Program.cs`（dotnet console 启动）、`Samples/Unity/TryGetMonoEntry.cs`（**这才是 TryGetBootstrap 该有的位置**）
- **对标**：Fantasy `Platform.Unity.Entry` vs `Platform.Net.Entry` 双入口
- **同时**：`ILogModule` → `ILogger`，新增 `IClock`（解耦 Unity Time）
- **测试**：~20 个 EditMode

#### V0.8 — 配置 + 序列化（IConfigSource / ISerializer）
- **目标**：类型化配置表 + 跨端序列化抽象
- **文件**：`Core/Config/IConfigSource.cs`、`ConfigLoader<T>.cs`、`JsonConfigSource.cs`；`Core/Serialization/ISerializer.cs`、`JsonSerializer.cs`
- **对标**：ET ConfigComponent + Fantasy ConfigLoader 中间形态；不绑 Luban
- **测试**：~25 个，含跨端 Shadow csproj 验证

#### V0.9 — Source Generator 注册（消除手动 host.Register）
- **目标**：用 Roslyn SourceGen 扫描 `[Module]` `[System]` 特性的类，编译期生成注册代码
- **文件**：`TryGet.SourceGenerator/` 独立 csproj、生成的 `__AssemblyManifest.g.cs`
- **对标**：Fantasy SourceGenerator + AssemblyManifest.Register()
- **测试**：~15 个，确保 Manifest 注册与手动注册行为等价

#### V1.0 — 真双端样例 + 文档冻结
- **目标**：发布 `Samples/Net/MmoServerDemo` + `Samples/Unity/MmoClientDemo`，**同一份 Aspect/Entity/Event 代码共享**
- **文件**：物理共享 csproj `TryGet.Shared`；Server `dotnet run`、Client Unity Play
- **对标**：ET All-in-One 轻量版
- **测试**：Server/Client 各 1 个 e2e 测试

#### V1.1+ — 业务扩展层（独立仓库 / Samples/）
- Network 抽象 + 默认实现（LiteNetLib / KCP / WebSocket）
- HybridCLR / YooAsset Adapter
- 用户提到的"业务内容"在这层落地

### 4.4 实施流程建议（贴 Claude Code 工作流）

**文档落地位置**（推荐）：
1. **本文** → `docs/strategy/V2-direction-pivot.md`（已建）
2. **V0.5 内容去留决策** → 待写 `docs/adr/0016-adapter-layer-out-of-core-scope.md`
3. **V0.6 ITask 设计** → 待写 `docs/design/V0.6-ITask.md`（PRD 形态）
4. **路线图** → 更新 `Assets/MyTryGetFramework/ARCHITECTURE.md` §V0.5 之后路线图
5. **CHANGELOG** → 新增 `[V0.5.5-pivot]` 条目记录方向调整

**用户工作流配合（建议顺序）**：
1. **本文 review**：用户审阅本份报告 → 同意 / 修订方向 → 接受后才进入第 2 步
2. **冻结 V0.6 Iter 3**：删除未 commit 的 `TryGetBootstrap.cs`、关闭 task #59
3. **写 ADR-0016**：把本文 §4.2 去留决策表沉淀为 ADR
4. **Adapter 退场方式拍板**：物理删除 + git tag `v0.5-with-adapters` 保留历史 / 移到 `Samples/Unity/Adapters/` / 拆独立仓库 — 三选一
5. **开始 V0.6 ITask**：PRD → 设计 → 实现 → 测试循环

## 5. 用户决策点（关键风险 / 不确定项）

| # | 决策点 | 推荐 | 风险 |
|---|---|---|---|
| 1 | **Aspect 是否走 ET 谱系**（纯数据 Component + 行为外置） | V0.7-V0.8 评估，**不在 V0.6 范围**。如保留 Aspect-with-methods 作为 TryGet 个性化选择，需写 ADR 明确"为何与 ET 谱系分歧" | 当前 80+ Aspect 测试，破坏性重构成本高 |
| 2 | **Hsenl 对标深度** | 降级为参考素材，主对标 ET/Fantasy | 若用户坚持深度对标 Hsenl，需手工 clone 源码 |
| 3 | **Adapter 退场方式** | 移到 `Samples/Unity/Adapters/`（保留代码价值，明确边界） | 物理删除会丢失 ~62 PlayMode 测试投资 |
| 4 | **V0.6 ITask 复杂度** | 先朴素 ITask 包装 ValueTask，性能优化推迟到 V0.8 | 状态机池化耗时大，影响 V0.6 周期 |
| 5 | **Source Generator 时机** | V0.9 引入 | 早做学习曲线陡，晚做测试维护重 |

## 6. Verdict

**应该接受本文档作为 V0.6+ 起点。**理由：
1. 与对标 4 框架的"基础架构"定义一致 — ET/Fantasy 都没把 UI/Audio/Input 当 Core
2. 现有 30+ commit 的 Core 部分（ModuleHost / Entity / Procedure / Event）经对标验证方向正确，**不浪费**
3. 退场范围明确（约 10 个 Unity Adapter 文件 + 5 个 Memory*Module + 2 个 ADR + 62 PlayMode 测试），止损成本 < 继续偏离成本
4. V0.6+ 路线（ITask → Bootstrap → Config → SourceGen → 双端样例）每个 Iter 都有对标参照，不会再发生方向漂移

---

## 附：对标信源

- [egametang/ET — deepwiki](https://deepwiki.com/egametang/ET)
- [qq362946/Fantasy — deepwiki](https://deepwiki.com/qq362946/Fantasy)
- [Alex-Rachel/TEngine — deepwiki](https://deepwiki.com/Alex-Rachel/TEngine)
- [Alex-Rachel/TEngine — GitHub](https://github.com/Alex-Rachel/TEngine)
- [dcze/Hsenl — Gitee](https://gitee.com/dcze/hsenl)
- TEngine 框架研究文章（CSDN）

Plan agent 完整分析报告：见本仓库 `.scratch/strategy-pivot-plan-agent-output-20260524.md`（如需保留 raw 报告）
