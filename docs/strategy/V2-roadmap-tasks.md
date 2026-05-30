# V2 路线图 — 客户端服务框架 + 轻量 Procedure Stack

> **修订日期**：2026/05/26
> **当前状态**：V2.0 路线 C 已落地，进入 V2.1+ 客户端框架迭代
> **配套文档**：`docs/design/V2.0-route-C-prd.md`、`docs/adr/0020-route-c-pivot.md`、`Assets/MyTryGetFramework/ARCHITECTURE.md`
> **目的**：记录 V2.0 路线重定向后的实施事实与后续任务边界，避免继续沿旧的双端 / ECS / IPlugin 路线扩张。

---

## 0. 路线结论

TryGet V2.0 的主线从“过早双端框架 + 自制 ECS + 横切 Plugin”重定向为：

**纯客户端服务框架 + 轻量 Procedure Stack + 可验证的 Core 基础设施。**

当前阶段只建设 Unity 客户端框架骨架。双端、服务端、Actor、Shared 业务层等内容延后到客户端架构稳定后再评估。

### 0.1 V2.0 保留内容

| 范围 | 状态 | 说明 |
|---|---|---|
| `ModuleHost` / `IModule` | 保留 | 继续作为框架根与模块生命周期基础 |
| `IEventBus` / `IEventScope` | 保留 | 全局类型安全事件系统，不再绑定 Entity/World |
| `TGTask` / Scheduler | 保留 | 自研异步原语已落地，继续作为 Core 异步基础 |
| `IProcedureModule` | 升级 | 吸收 BigCat Scene Stack 思路，支持 Push/Pop/Replace |
| `ILogger` / `IClock` / `ITimerModule` / `IPoolModule` | 保留 | 客户端基础服务 |
| `IKVStore` / `IConfigSource` / `IAssetSource` / `ISerializer` | 保留 | 数据源与加载抽象 |
| `[Module]` / `[EventHandler]` Source Generator | 保留 | 只保留客户端服务注册与事件订阅自动化 |
| `INetClient` | 简化保留 | 只保留客户端网络契约，不再提前定义服务端模型 |

### 0.2 V2.0 移除内容

| 范围 | 状态 | 原因 |
|---|---|---|
| 自制 ECS / EC：Entity、Aspect、Tag、Query、SystemGroup、IPureComponent | 已移除 | 对当前 Unity 客户端框架过重，与 Module 系统职责混杂 |
| IPlugin / IPlugPoint / IPluginHost | 已移除 | 当前横切点不足，不应在框架骨架阶段引入第二套扩展系统 |
| 服务端契约：INetServer、ConnectionId、ITickLoop、IFrameLoop、Shared sample | 已移除 | 双端架构延后，当前只保留客户端网络入口 |
| 旧 Module：ILogModule、IConfigModule、IResourceModule、ISaveModule、ILocalizationModule | 已移除 | 已被更小、更准确的接口替代 |
| ECS 相关 Source Generator | 已移除 | Source Generator 只保留 Module / EventHandler 两条客户端主线 |

---

## 1. V2.0 — 路线 C 落地状态

### 1.1 总目标

完成从旧架构到客户端服务框架的收敛：删掉与当前阶段不匹配的系统，保留并强化真正需要的客户端框架骨架。

### 1.2 Iter 结果

| Iter | 主题 | 状态 | 关键产物 |
|---|---|---|---|
| 0 | PRD + ADR | Done | `docs/design/V2.0-route-C-prd.md`、`docs/adr/0020-route-c-pivot.md` |
| 1 | ECS 整套移除 | Done | 移除 Entity/Aspect/Tag/Query/System/SystemGroup/IPureComponent 及相关测试 |
| 2 | IPlugin 系统移除 | Done | `ModuleHost` 回归单一模块生命周期职责 |
| 3 | 服务端契约移除 | Done | `INetClient` 简化为客户端契约，服务端/Shared 推迟 |
| 4 | 废弃 Module 移除 | Done | 保留 `ILogger`、`IKVStore`、`IConfigSource`、`IAssetSource` 等准确命名接口 |
| 5 | Procedure Stack | Done | `Start` / `Push` / `Pop` / `Replace`，新增 `OnPause` / `OnResume` |
| 6 | 文档同步 | Done | `ARCHITECTURE.md`、`CHANGELOG.md`、本路线图同步到 V2.0 |

### 1.3 V2.0 Definition of Done

| 验证项 | 状态 |
|---|---|
| Core 不引用 UnityEngine | 已由 Shadow csproj 持续验证 |
| Samples/Net 可运行 | 已跑通当前主流程 |
| Source Generator 可构建 | 保留 Module/EventHandler 后继续验证 |
| Procedure Stack 行为有 EditMode 测试覆盖 | 已新增同步栈行为测试 |
| 架构文档不再宣传 ECS / IPlugin / 双端为当前主线 | 已同步 |

---

## 2. V2.1 — 事件系统升级

### 2.1 目标

让事件系统更适合 Unity 客户端高频场景：降低 GC、强化生命周期管理，并保留当前 `IEventBus` 的简单使用体验。

### 2.2 候选任务

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | 零 GC 事件派发评估 | 对比 TEngine GameEvent 风格与当前泛型 delegate 实现 |
| E2 | Source Gen 事件接口 | 评估是否生成静态 invoker，避免运行时反射或装箱 |
| E3 | EventScope 体验完善 | 确保订阅自动解绑在 UI / Procedure / Module 中一致可用 |
| E4 | 压测与基准 | 高频事件、订阅/退订、异常隔离、重复订阅行为 |

### 2.3 启动前决策

- 是否接受更复杂的生成代码来换取更低 GC。
- 是否继续保持 `IEventBus` 为唯一公开入口，避免暴露多套事件 API。

---

## 3. V2.2 — ModuleHost 多阶段 Update

### 3.1 目标

吸收 BigCat 多阶段 Update 的可取部分，但不引入 Worker/Node/分布式模型。

### 3.2 候选任务

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | EarlyUpdate / FixedUpdate / LateUpdate 契约 | 在 `IUpdateModule` 外补齐常见 Unity 时序 |
| E2 | 优先级排序一致性 | 确保多阶段模块排序、依赖拓扑、Shutdown 顺序一致 |
| E3 | 时间源策略 | 明确 `IClock` 与 fixed delta / unscaled delta 的关系 |
| E4 | Unity Adapter 桥接 | 由 MonoBehaviour 驱动 Core 多阶段循环 |

### 3.3 不做范围

- 不引入线程 Worker。
- 不引入服务端 TickLoop。
- 不把 Procedure 强行拆成多个阶段，除非真实客户端需求出现。

---

## 4. V2.3 — UI 框架

### 4.1 目标

建设客户端最常用的上层服务：窗口、层级、生命周期、异步打开、与 Procedure Stack 协作。

### 4.2 候选任务

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | `IUIModule` 契约 | Open / Close / Get / IsOpen / 层级管理 |
| E2 | `UIWindow` / `UIWidget` 基类 | 只放生命周期与上下文，不绑定具体业务逻辑 |
| E3 | Procedure + UI 协作样例 | MainMenu / Gameplay / PauseMenu 与 Stack 对齐 |
| E4 | Unity Adapter 实现 | 基于 prefab / Canvas / Addressables 或 YooAsset Adapter |

### 4.3 不做范围

- 不内置具体 UI 美术结构。
- 不做业务 UI 模板生成器。
- 不让 Core 引用 UnityEngine。

---

## 5. V2.4 — 资源管理 Adapter

### 5.1 目标

在 Core 保持 `IAssetSource` 抽象的前提下，为 Unity 客户端接入真实资源系统。

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | YooAsset Adapter | 推荐优先级最高，适配 `IAssetSource` |
| E2 | Addressables Adapter 评估 | 作为可选替代方案，不强行双轨维护 |
| E3 | 异步加载与释放语义 | 与 TGTask、引用计数、Procedure 生命周期对齐 |
| E4 | 样例 | UI / 场景加载各一个最小可信样例 |

---

## 6. V2.5 — 场景管理

### 6.1 目标

在 Procedure Stack 之上补齐 Unity Scene 加载服务，而不是重新引入 Entity/Scene 双端模型。

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | `ISceneModule` 契约 | Load / Unload / Switch / ActiveScene |
| E2 | Unity SceneManager Adapter | 仅 Adapter 层引用 UnityEngine.SceneManagement |
| E3 | Procedure 驱动场景切换 | GameplayProcedure 进入时加载场景，退出时释放 |
| E4 | 加载进度与取消策略 | 与 TGTask 和 UI Loading 协作 |

---

## 7. V2.6 — 音频管理

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | `IAudioModule` 契约 | BGM / SFX / Voice 基础能力 |
| E2 | Unity AudioSource Adapter | 真实播放实现 |
| E3 | 音量分组与持久化 | 与 `IKVStore` 协作 |

---

## 8. V2.7 — 客户端网络 Adapter

### 8.1 目标

只做客户端连接能力，不提前恢复服务端框架。

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | TCP / KCP / WebSocket 选型 Spike | 先做一个真实 Adapter，不一次性维护多套 |
| E2 | 重连 / 心跳 | 作为客户端网络服务能力，不引入 IPlugin 系统 |
| E3 | 消息序列化 | 通过 `ISerializer` 接入 MemoryPack / MessagePack 等 Adapter |
| E4 | 最小客户端样例 | 连接、发送、接收、断线、重连 |

---

## 9. V2.8 — 热更新

| Epic | 范围 | 说明 |
|---|---|---|
| E1 | HybridCLR 接入评估 | 只在 Unity Adapter / Tooling 层处理 |
| E2 | 热更程序集边界 | Core / Unity / Hotfix 的引用方向必须清晰 |
| E3 | Source Generator 与热更兼容 | 确认生成注册表在热更程序集加载后的行为 |

---

## 10. V3.0+ — 双端扩展候选

双端架构不再是 V2 主线。只有当客户端框架稳定且出现明确服务端复用需求后，才重新评估：

| 候选 | 前置条件 |
|---|---|
| EC / Entity 模型 | 客户端已有真实业务证明 Module + Procedure + UI/Scene 不足 |
| 服务端 Tick / Actor / Mailbox | 有真实服务端样例需求，而不是为了架构完整性 |
| Shared 业务层 | 客户端与服务端确实需要共享协议/配置/纯逻辑代码 |
| INetServer | 已有客户端 INetClient Adapter 和协议层稳定之后 |

---

## 11. 实施纪律

1. **客户端优先**：任何新增能力先说明 Unity 客户端使用场景。
2. **Core 保持纯 C#**：`Runtime/Core` 不引用 UnityEngine。
3. **命名准确优先**：接口名表达真实职责，不为兼容旧设计保留错误命名。
4. **不为未来双端预留复杂系统**：没有当前需求的服务端/Actor/ECS/Plugin 不进入 Core。
5. **每个 minor 必须同步验证**：Shadow csproj、Samples/Net、Source Generator、相关测试与文档。
6. **文档服从代码事实**：现有代码不是权威，但落地后的当前代码和验证结果是汇报依据。

---

## 12. Verdict

V2.0 路线 C 已作为新的架构基线接受：

**TryGet 当前是客户端服务框架，不是双端框架，不是 ECS 框架，也不是 Plugin 框架。**

后续 V2.x 只围绕 Unity 客户端框架能力补齐：事件、多阶段 Update、UI、资源、场景、音频、客户端网络、热更。双端扩展进入 V3.0+ 候选，等待真实需求再决策。
