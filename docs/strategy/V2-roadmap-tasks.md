# V2 路线图 — 任务拆解（V0.6 详细 + V0.7-V1.0 Epic 骨架）

> **撰写日期**：2026/05/24
> **状态**：等待用户 review 后进入 V0.6 实施
> **配套**：`docs/design/V2-commercial-framework-architecture.md`（设计本体）
> **目的**：把设计文档 §6 的任务拆解落到可追踪的 Issue / Epic 粒度。V0.6 详细到 Iter 级，V0.7+ 仅给 Epic 级骨架，避免决策疲劳。

---

## 0. 实施前置条件

用户必须先就以下 4 个关键决策给出明确意见，否则 V0.6 启动后会反复返工：

| # | 决策 | 默认方案 | 阻塞影响 |
|---|---|---|---|
| **A** | **`ITask` 自研路线** vs **UniTask 包装路线** vs **混合方案**（Core 只暴露 `ITask` 接口，由 UniTask 实现） | **自研路线** | 阻塞 V0.6 全部 Iter |
| **B** | **Aspect 保留行为** vs **改走 ET 纯数据 Component** | **保留 Aspect-with-methods，V0.9 增 `IPureComponent` 二级方案** | 阻塞 V0.9 ECS 二级方案 |
| **C** | **`ILogModule` → `ILogger` 命名替换** 是否启动 | **V0.7 启动**（Obsolete 一个 minor 版本） | 阻塞 V0.7 |
| **D** | **第一刀实施位置**：从 `ITask`（最致命缺口）开始 vs 从 `IEntry / Bootstrap`（最容易出双端样例）开始 | **从 `ITask` 开始**（设计文档 §6.1） | 决定 V0.6 / V0.7 顺序 |

待这 4 个决策定下，才进入 V0.6 Iter 0。

---

## 1. V0.6 — `ITask` 异步原语（详细到 Iter 级）

### 1.1 V0.6 总目标

让 Core 在 dotnet console 也能跑出 `async ITask MyMethod() { await timer.Delay(1f); await asset.LoadAsync(...); return; }` 这样的异步业务代码，**不依赖 UniTask、不依赖 `System.Threading.Tasks.Task`**。

### 1.2 V0.6 成功标准（Definition of Done）

1. `Samples/Net/Program.cs` 跑通：Boot → 异步加载 Config → 异步等待 1s → Login → 异步广播 → InGame（全部 `async ITask` 实现）。
2. `Samples/Unity/TryGetMonoEntry.cs` 跑通同样流程（Unity Editor PlayMode）。
3. 100 万次 `await timer.Delay(0)` 的 GC alloc < 1MB（pool 生效证明）。
4. `dotnet build` Shadow csproj 仍然通过（不引入 Unity 依赖）。
5. 测试覆盖 30+，全绿。
6. CHANGELOG `[V0.6]` 条目完整。

### 1.3 V0.6 Iter 列表

| Iter | 主题 | 详细任务 | 依赖 | DoD |
|---|---|---|---|---|
| **0** | PRD `docs/design/V0.6-ITask.md` | 详细 API 设计 + 对标 ETTask / HTask / FTask 取舍 + 性能基准 + 已知失败模式 | 决策 A 通过 | 用户 review 通过 |
| **1** | `ITask` 骨架 | `ITask` + `ITask<T>` + `Awaiter` struct + `AsyncITaskMethodBuilder` + `AsyncITaskMethodBuilder<T>` + `[AsyncMethodBuilder]` attribute | Iter 0 | `async ITask MyMethod() { return; }` 编译通过 |
| **2** | `ITaskCompletionSource` | `ITaskCompletionSource` + `ITaskCompletionSource<T>` + `ITaskBody` 状态机内部接口 + 同步 `SetResult` / `SetException` / `SetCanceled` | Iter 1 | `var tcs = new ITaskCompletionSource(); var t = tcs.Task; tcs.SetResult();` 工作 |
| **3** | `_version` 防过期 | 借鉴 hsenl HTask 的 `_version` 字段：struct 实例持 body 的 version 快照，body 回收时 version++；await 时校验失配抛 `TaskExpiredException` | Iter 2 | 测试覆盖 "ITask body 已 Reset 后被复制的 struct 实例 await" 抛异常 |
| **4** | `TaskPool` 状态机池化 | `TaskPool.MaxPoolSize` + `TaskPool.GetCacheInfo<T>()` + body 复用机制；Pool 满时 body 不入池（直接 GC） | Iter 3 | 100 万 `await tcs.Task` 后 GC alloc < 1MB |
| **5** | `ITaskScheduler` 接口 | `ITaskScheduler : IModule, IUpdateModule` + `Yield() / Delay(seconds) / WaitForFrames(int)` + 内部用环形队列管理 continuation | Iter 4 | `await scheduler.Delay(1f)` 在 1s 后正确 continuation |
| **6** | `ITimerModule` 异步衔接 | `ITimerModule.WaitAsync(seconds): ITask` 扩展方法；保持现有 `Schedule(Action)` 不变 | Iter 5 | `await timer.WaitAsync(1f)` 工作 |
| **7** | `IAsyncProcedure` + Procedure 异步支持 | `IAsyncProcedure : IProcedure` + `OnEnterAsync(): ITask` + `OnExitAsync(): ITask`；`ProcedureModule.TransitionTo` 检测异步路径 | Iter 6 | Procedure 可以 `OnEnterAsync()` 异步加载资源 |
| **8** | 异常传播 + Cancellation | `ITaskException` 包装；`TaskCanceledException`；`ITask.Forget()` 显式 fire-and-forget；`ITaskScheduler` 全局 `OnUnobservedException` 钩子 | Iter 7 | 测试覆盖 "未 await 的 ITask 抛异常" 进入全局钩子 |
| **9** | 测试覆盖 30+ | 基本 await / 异常 / pool 边界 / cancel / 嵌套 / 多次 await 同 ITask（应抛）/ 多次 SetResult（应抛）/ Forget / Procedure 异步流 | Iter 8 | 全绿 |
| **10** | `Samples/Net/Program.cs` 雏形 | dotnet console 跑通 Boot → Delay 1s → Login → Delay 1s → InGame 三 Procedure 转移 | Iter 9 | `dotnet run` 输出三阶段切换 |
| **11** | CHANGELOG `[V0.6]` 条目 + ARCHITECTURE.md V0.6 更新 | 文档同步 | Iter 10 | merge |

### 1.4 V0.6 已知风险点

| # | 风险 | 缓解 |
|---|---|---|
| 1 | `AsyncMethodBuilder` 与 C# 编译器交互复杂；C# 编译期可能因 attribute 缺失静默拒绝 async 方法 | Iter 1 写最小 spike 验证编译期行为，再扩展 |
| 2 | `_version` int 溢出（int.MaxValue 次 await 后回绕）| 用 `int.MinValue` 作 "expired sentinel"，到 `MaxVersion = int.MaxValue - 2` 时主动 Reset Pool（参考 hsenl） |
| 3 | Pool 在多线程场景下竞态 | V0.6 锁定单线程心智，多线程明确不支持；测试加 `ThrowIfNotMainThread` 检测 |
| 4 | `OnCompleted` 同步执行 continuation 可能导致深递归爆栈 | 内部维护"调度深度计数器"，超过 16 层时强制入队 `TaskScheduler` 异步执行 |
| 5 | `IAsyncProcedure` 与同步 `IProcedure` 共存的状态机复杂度 | 走 union 路径：`ProcedureModule` 内部判 `proc is IAsyncProcedure` 选异步流程，否则同步 |

### 1.5 V0.6 回退方案

如果 Iter 1-4（`ITask` 状态机 + Pool）实现风险超预期：

- **回退到混合方案**：Core 暴露 `ITask` 接口（不是 struct，而是 interface），Unity Adapter 用 UniTask 实现，Net Adapter 用 `ValueTask` 包装实现。性能略差但实现简单。
- 触发条件：Iter 3 完成时若 GC alloc 测试不达标 + 实现工作量已超 5 个工作日。

---

## 2. V0.7 — Bootstrap + `ILogger` + `IClock`（Epic 级骨架）

### 2.1 V0.7 总目标

Core 获得"双端启动入口规范" + "日志接口现代化" + "时钟解耦 Unity Time"。

### 2.2 V0.7 Epic 列表

| Epic | 范围 | 关键产物 |
|---|---|---|
| **E1** | `IEntry / Bootstrap` 双端入口规范 | `IEntry.cs` + `Bootstrap.Run(entry, ctx)` + `BootstrapContext`；`Samples/Net/Program.cs` + `Samples/Unity/TryGetMonoEntry.cs` |
| **E2** | `ILogger` 替代 `ILogModule` | `ILogger` + `ILogSink` 接口 + `ConsoleLoggerSink`；`ILogModule` 标记 Obsolete + 桥接实现保留一个 minor 版本 |
| **E3** | `IClock` 替代直接读 `Time.deltaTime` | `IClock` 接口 + `SystemClock`（Net，外部传 dt）+ `UnityClock`（Adapter，读 `Time.*`）；`ITimerModule / EntityWorld / IProcedureModule` 改为读 `IClock` |
| **E4** | `IEventScope`（订阅自动解绑） | 借鉴 TEngine `GameEventMgr`：订阅者持 `IEventScope` 实例，Dispose 时批量解绑所有 handler；保留现有 `Subscribe / Unsubscribe` API（共存） |
| **E5** | V0.7 测试 20+ + CHANGELOG | 单测 + Samples 跑通 |

### 2.3 V0.7 决策点

- E2 / E3 是否在同一 Iter 完成 vs 拆两个 minor？（推荐：合并为 V0.7 一个 minor 落地）
- E4 `IEventScope` 是否引入？（设计文档决策点 #11，需用户确认）

---

## 3. V0.8 — KV / Config / Asset / Serializer 重构（Epic 级骨架）

### 3.1 V0.8 总目标

把 V0.5.5 残留的 `ISaveModule / IConfigModule / IResourceModule / ILocalizationModule` 重构为更通用的 "数据源 + 加载器" 抽象。

### 3.2 V0.8 Epic 列表

| Epic | 范围 | 关键产物 |
|---|---|---|
| **E1** | `IKVStore` 替代 `ISaveModule` | `IKVStore` + `MemoryKVStore`；`ISaveModule` Obsolete + 桥接；KV 强类型 `Get<T>/Set<T>` |
| **E2** | `IConfigSource + ConfigLoader<T>` 替代 `IConfigModule` | `IConfigSource` 二进制 + JSON 双访问；`ConfigLoader<T>` 加载 + 热重载；`JsonConfigSource` Memory 实现 |
| **E3** | `IAssetSource + ISerializer` 替代 `IResourceModule` | `IAssetSource.LoadAsync<T>(): ITask<T>`（与 V0.6 ITask 集成）；`ISerializer.Serialize/Deserialize<T>`；`MemoryAssetSource` + `JsonSerializer` |
| **E4** | `ILocalizationModule` 评估降级 | 移到 `Optional/` 子目录；不再算 Core 标准模块；接口保持不变 |
| **E5** | V0.8 测试 25+ + CHANGELOG | Round-trip 测试 + 桥接兼容性测试 |

---

## 4. V0.9 — IPlugin（hsenl 风格切面）+ IPureComponent（ECS 二级方案）— **完整落地** ✓

> 2026/05/24 修订：原 V0.9 路线含 Source Generator，拆分为 V0.9（运行时部分）+ V0.9.5（Source Generator 独立 minor）。

### 4.1 V0.9 总目标

吸收 hsenl IPlug 设计落地 ModuleHost 横切关注点；引入 `IPureComponent` ECS 二级方案与 Aspect 双轨并存。

### 4.2 V0.9 Epic 列表（已全部完成）

| Epic | 范围 | 状态 | 关键产物 |
|---|---|---|---|
| ~~**E1**~~ | ~~TryGet.SourceGenerator 独立 csproj 骨架~~ | **→ V0.9.5** | 转移 |
| ~~**E2**~~ | ~~`[Module]` Attribute + AssemblyManifest.g.cs~~ | **→ V0.9.5** | 转移 |
| ~~**E3**~~ | ~~`[SystemRegister]`~~ | **→ V0.9.5** | 转移 |
| ~~**E4**~~ | ~~`[EventHandler]`~~ | **→ V0.9.5** | 转移 |
| **E5** | `IPlugin / IPluginHost` 切面机制 | **Done** | `IPlugin.cs` + `ModuleHostPlugPoints.cs` + ModuleHost 集成 + 13 测试 |
| **E6** | `IPureComponent`（ECS 二级方案）+ ADR-0017 | **Done** | `IPureComponent.cs` + `EntityPureComponentExtensions.cs` + ADR-0017 + 16 测试 |
| **E7** | V0.9 测试 + CHANGELOG | **Done** | 29 新增测试 + CHANGELOG V0.9 段 + ARCHITECTURE V0.9 段 |

### 4.3 V0.9 决策点结论

- **E1-E4 → V0.9.5**：Source Generator 独立 csproj + Roslyn IIncrementalGenerator + Unity asmdef + 多 attribute 设计，工作量 ≈ V0.6 完整（11 Iter）。独立发布更稳。
- **E5 IPlugin 先做**：不依赖 SourceGen，是 ModuleHost 切面机制的核心。命名升级 vs hsenl：`IPlug→IPlugin`、`IPluggable→IPluginHost`、`IPlugGroup→IPlugPoint`、`Init/Dispose→Install/Uninstall`；新增 `Priority` 字段与 Module 体系对齐。
- **E6 IPureComponent 独立交付**：marker interface + 外置 IComponentSystem<T>，与 Aspect 双轨并存（ADR-0017）。V0.9 不自动调度（业务显式触发），自动调度留 V0.9.5。

---

## 4.5 V0.9.5 — Source Generator 注册（**完整落地** — 7/7 Iter）

### 4.5.1 V0.9.5 总目标

引入 Roslyn IIncrementalGenerator 让 Module / System / EventHandler / IComponentSystem 自动注册，消除手动 `host.Register<>()` / `world.RegisterSystem(...)` / `bus.Subscribe<T>(...)` 调用。

### 4.5.2 V0.9.5 Epic 列表（已全部完成）

| Epic | 范围 | 状态 | 关键产物 |
|---|---|---|---|
| **E1** | `TryGet.SourceGenerator` 独立 csproj 骨架 | **Done (Iter 1)** | `Tools/MyTryGetFramework.SourceGenerator/` csproj + HelloWorldGenerator 烟测 |
| **E2** | Unity asmdef 集成 + RoslynAnalyzer label | **Done (Iter 2)** | `Assets/.../Runtime/Core/Generators/` DLL + .meta with `RoslynAnalyzer` label |
| **E3** | `[Module]` Attribute + 生成 `__AssemblyManifest.g.cs` | **Done (Iter 3)** | `ModuleAttribute` + `AssemblyManifestRegistry` + `ModuleManifestGenerator` + Samples/Net demo |
| **E4** | `[SystemRegister]` + 生成 System 注册 | **Done (Iter 4)** | `SystemRegisterAttribute` + `SystemRegistry` + `SystemRegisterGenerator` |
| **E5** | `[EventHandler]` + 生成 EventBus 订阅 | **Done (Iter 5)** | `EventHandlerAttribute` + `EventHandlerRegistry` + `EventHandlerGenerator` + Bootstrap 集成 |
| **E6** | `IComponentSystem<T>` 自动调度（V0.9 IPureComponent 配套） | **Done (Iter 6)** | `ComponentSystemHooks<T>` + EntityPureComponentExtensions hook + `ComponentSystemGenerator` |
| **E7** | V0.9.5 测试 + CHANGELOG | **Done (Iter 7)** | Samples/Net 端到端 3 路 demo + CHANGELOG/ARCHITECTURE/路线图 整段收尾 |

### 4.5.3 V0.9.5 决策点结论

- **`IIncrementalGenerator`（非 `ISourceGenerator`）**：2026 主流实践，Value-equatable DTO + ForAttributeWithMetadataName 入口高效
- **Generator 在 repo root `Tools/` 下**：与 Unity Assets 完全隔离，PostBuild 自动 copy DLL 到 Unity
- **Dual-trigger init**：.NET 端 `[ModuleInitializer]` + Unity 端 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `[Preserve]` 防 IL2CPP strip
- **AssemblyManifestRegistry / EventHandlerRegistry 限制**：仅看到 `ApplyAll` 调用前已 static-init 的 assemblies；后加载 assembly 注册不回填已构造 host
- **SystemRegistry 业务显式 ApplyAll(world)**：避免多 world 实例下的注册歧义 + 测试隔离困难
- **ComponentSystemGenerator 用接口实现触发**：IComponentSystem 是契约接口，不强制业务额外标 attribute（CreateSyntaxProvider + semantic 实现检查）

### 4.5.4 V0.9.5 端到端验证证据

`dotnet run Samples/Net` 输出（关键 3 行）：

```
[Info] Hello from V0.9.5 auto-registered Module, Samples/Net!        // [Module] auto-register
[Info] TickEvent handler observed LastTickIndex = 42                 // [EventHandler] auto-subscribe
[Info] CounterSystem observed AttachCount=1 DetachCount=1            // IComponentSystem auto-hook
```

业务 setup 内**没有任何 host.Register / bus.Subscribe / hook 手动注册**行 — 完全靠 attribute / interface + Generator + dual-trigger init 自动完成。

---

## 5. V1.0 — 真双端样例 + 文档冻结（Epic 级骨架）

### 5.1 V1.0 总目标

发布一份真正的双端 demo：服务端 dotnet console + 客户端 Unity Play，共享 Aspect/Entity/网络消息代码。

### 5.2 V1.0 Epic 列表

| Epic | 范围 | 关键产物 |
|---|---|---|
| **E1** | `Samples/Shared/TryGet.Shared.csproj` + asmdef | 双端共享代码物理位置确立 |
| **E2** | `Samples/Net/MmoServerDemo` | dotnet console 服务端：10 客户端连接 / 广播 / 断线重连 |
| **E3** | `Samples/Unity/MmoClientDemo` | Unity Play 客户端：连服务端 / 收广播 / 显示 |
| **E4** | 共享 Aspect / Entity / 网络消息 | 一份代码两端工作的最小可信样例 |
| **E5** | 文档冻结：`ARCHITECTURE.md` V1.0 完整版 + 所有 ADR 状态确认 | review 通过 + tag `v1.0.0` |

---

## 6. V1.1+ — 业务扩展层（独立仓库或 Samples/，不在 Core）

不属于本路线图的"基础架构"范畴，但作为 V1.0 完整闭环的实现示例需要给出：

| 模块 | 优先级 | 说明 |
|---|---|---|
| **Network Adapter** (KCP / TCP / WebSocket Channel + 拆粘包 + Plug 加密) | 高 | 真做时重写 ADR-0014（当前 Superseded） |
| **HybridCLR Adapter** (`IHotfixLoader` Unity 实现) | 高 | 接口在 Core（V1+），实现 Unity 端 |
| **YooAsset Adapter** (`IAssetSource` 实现) | 高 | 替代 V0.8 `MemoryAssetSource` |
| **Luban Adapter** (`IConfigSource` 实现 + Editor 生成器) | 高 | 替代 V0.8 `JsonConfigSource` |
| **MemoryPack Adapter** (`ISerializer` 实现) | 高 | V0.8 可以直接做（不阻塞 V1.0） |
| **Samples/Unity/Adapters/** (V0.5.5 已迁的 Audio/Input/UI/Scene/PlayerPrefsSave) | 维护 | 保持工作 |
| **Actor / MailBox / Location（参考 ET）** | 中 | V2+ 业务层，按需启动 |

---

## 7. 实施流程（贴 Claude Code 工作流）

### 7.1 单 Iter 实施模板

1. 创建 `.scratch/v06-itask/iter-{N}/` 工作目录（Issue tracker 约定见 `docs/agents/issue-tracker.md`）
2. PRD（如果是 Iter 0 / 整体设计变更）→ 用户 review
3. 接口先行：写 `I*.cs` 接口 + 详细 XML doc 注释
4. Shadow csproj `dotnet build` 验证不引入 Unity 依赖
5. 实现：写 `MemoryXxx.cs` / 具体类
6. 测试：EditMode 单测（PlayMode 仅在 Adapter 需要时用）
7. CHANGELOG 条目 + ARCHITECTURE.md 局部更新
8. commit + push（每 Iter 一 commit）

### 7.2 跨 Iter / 跨 minor 的纪律

- 每个 minor（V0.6 / V0.7 / V0.8 / V0.9 / V0.9.5 / V1.0）落地后打 git tag `v0.X.0`
- 每个 minor 落地后更新 `ARCHITECTURE.md` 的"V0.5 之后路线图"段
- 每个新 ADR 写完后更新 `ARCHITECTURE.md` 的"核心设计决策（ADR 索引）"表
- 任何对 Core 公开接口的破坏性变更必须先发 ADR

---

## 8. 用户决策清单（V0.6 启动前必答）

| # | 问题 | 推荐 | 影响 |
|---|---|---|---|
| 1 | `ITask` 自研 vs UniTask 包装 vs 混合方案 | **自研** | V0.6 全部 |
| 2 | Aspect 保留行为 vs 改纯数据 | **保留（写 ADR-0017）** | V0.9 ECS 二级方案 |
| 3 | `ILogModule → ILogger` 命名替换启动时机 | **V0.7 启动** | V0.7 范围 |
| 4 | V0.6 第一刀位置：ITask vs Bootstrap | **ITask（致命缺口优先）** | V0.6/V0.7 顺序 |
| 5 | Adapter 退场后的 `Samples/Unity/Adapters/` 迁移是否已 commit | （查 git）若否，需先补齐 ADR-0016 §3 实施时序 | 历史包袱 |
| 6 | 是否引入 `IEventScope`（TEngine GameEventMgr 风格） | **V0.7 引入** | V0.7 范围 |
| 7 | Source Generator 引入时机 | **V0.9.5（独立 minor，2026/05/24 修订）** | V0.8 实施压力 |
| 8 | 是否在 V0.6-V0.8 期间允许"用 UniTask 临时挡刀"（spike 验证）vs "纯自研到底" | **允许 spike，但 V0.6.0 release 前必须切回自研** | V0.6 工作量 |

---

## 9. 与现有文档的关系

| 文档 | 关系 |
|---|---|
| `docs/design/V2-commercial-framework-architecture.md` | 设计本体 → 本文档是其 §6 任务拆解的具体化 |
| `docs/strategy/V2-direction-pivot.md` | 战略前情（做减法）→ 本文档承接做加法的执行节奏 |
| `docs/adr/0016-adapter-layer-out-of-core-scope.md` | Adapter 退场决策 → 本文档默认前提 |
| `Assets/MyTryGetFramework/ARCHITECTURE.md` | 每个 minor 落地后由本路线图反向更新 |
| `CHANGELOG.md` | 每个 Iter / minor 落地后追加条目 |

---

## 10. Verdict

**接受本路线图作为 V0.6+ 实施起点的前提**：用户给出第 §8 的 8 个决策回答（最少 #1 / #2 / #4 必答，其余可在 minor 启动时再答）。回答完成即进入 V0.6 Iter 0（PRD `docs/design/V0.6-ITask.md`）。

实施阶段不再做大方向调整。若大方向要调整，回到 `V2-direction-pivot.md` + 本文档 review 流程。
