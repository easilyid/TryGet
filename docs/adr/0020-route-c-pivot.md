# ADR-0020: 路线 C 重定向 — 纯客户端服务框架

## Status

Accepted（V2.0 起生效，Supersedes ADR-0014/0016/0017/0018/0019）

## Context

V0.1–V1.0 期间 TryGet 同时追求：
1. 自制 ECS（Entity/Aspect/Tag/Query/SystemBase/IPureComponent 双轨）
2. 服务端抽象（INetServer/IConnection/ITickLoop/NetPlugPoints）
3. 横切插件系统（IPlugin/IPlugPoint/IPluginHost）
4. 跨端代码共享（Samples/Shared + Shadow csproj）

2026/05/25 用户明确指令："当前只需客户端框架，双端架构可以等客户端架构完整后再加入迭代"。

基于 TEngine / BigCat / hsenl / Fantasy 四框架对比分析：

| 框架 | 定位 | 自制 ECS | 服务端抽象 | Plugin 系统 |
|------|------|----------|-----------|-------------|
| TEngine | 纯客户端 | 无 | 无 | 无 |
| BigCat | 双端 | Scene+GameUnit | Worker/Node/RPC | 无 |
| hsenl | 双端 | Entity-Component | Channel/Service | IPlug |
| Fantasy | 双端 | Entity=Component | 全栈 | 无 |

**结论**：纯客户端框架（TEngine）不做 ECS、不做服务端抽象、不做 Plugin 系统。这些都是双端框架的产物。

## Decision

**V2.0 起 TryGet 转向路线 C：纯客户端服务框架 + 轻量场景栈。**

### 移除

1. **ECS 整套**：Entity/Aspect/Tag/Query/SystemBase/SystemGroup/Phase/IPureComponent/ComponentSystemHooks/BitArray256/TypeIndex/EntityWorld/WorldEventBus(接口)/EntityEventDispatcher
   - 理由：纯客户端不需要自制 EC；Unity 原生 GameObject/DOTS 已覆盖
   - 参考：TEngine 完全不做 ECS

2. **IPlugin/IPlugPoint/IPluginHost**：
   - 理由：客户端横切用事件即可；Plugin 系统是服务端中间件模式（hsenl IPlug 设计初衷是网络中间件）
   - 参考：TEngine/BigCat/Fantasy 均无 Plugin 系统

3. **服务端契约**：INetServer/IConnection/ConnectionId/ConnectionState/NetPlugPoints/ITickLoop/IFrameLoop/Samples/Shared
   - 理由：用户明确"当前只需客户端"
   - 保留：INetClient（简化为纯客户端连接）+ INetMessage

4. **已废弃 Module**：ILogModule/ISaveModule/IConfigModule/IResourceModule/ILocalizationModule + 所有 Memory 实现
   - 理由：V0.7/V0.8 已标记 [Obsolete]，新接口（ILogger/IKVStore/IConfigSource/IAssetSource）已替代

5. **Source Generator 部分**：SystemRegisterGenerator/ComponentSystemGenerator/HelloWorldGenerator
   - 理由：随 ECS 移除，这些 Generator 无目标类型

### 保留

- ModuleHost / IModule / Bootstrap / IEventBus / IEventScope
- TGTask 全家桶
- IClock / ILogger / ITimerModule / IPoolModule
- IProcedureModule（升级为 Stack 模式）
- ISerializer / IKVStore / IConfigSource / IAssetSource
- Source Generator（Module + EventHandler）
- INetClient（简化）+ INetMessage

### 新增

- IProcedure.OnPause / OnResume（吸收 BigCat Scene.Pause/Resume）
- IProcedureModule.Push / Pop / StackDepth（吸收 BigCat SceneMgr.stack）

## Consequences

### 好处

- Core 文件数从 86 降到 ~40，认知负担减半
- 不与 Unity 原生游戏对象管理竞争
- 框架定位清晰：服务管理 + 流程管理 + 异步 + 事件
- 为 V2.1+ 客户端核心能力（UI/资源/场景/音频/热更）腾出空间
- Procedure Stack 比平坦 FSM 更贴近游戏客户端实际（暂停菜单/设置页/弹窗叠加）

### 风险

- 未来加双端时需要重新引入 EC 模型（V3.0+）
  - 缓解：V2.0 的 ModuleHost 骨架不阻碍未来加 EntityWorld Module
- 移除 ECS 后现有测试大量删除（~60+ 测试）
  - 缓解：Procedure Stack 新增测试补回覆盖率
- IPlugin 移除后 ModuleHost.Update 路径变简单，但失去了 BeforeUpdate/AfterUpdate hook 点
  - 缓解：V2.2 计划加 EarlyUpdate/FixedUpdate 调度点，比 Plugin 更直接

### 不允许的退路

- ✗ 保留 ECS "以防万一"：死代码增加维护成本，且与"客户端不需要"的分析结论矛盾
- ✗ 保留 IPlugin "因为已经写好了"：沉没成本不是保留理由
- ✗ 保留 INetServer "为了双端"：用户明确说"双端等客户端完整后再加"

## 关联

- Supersedes: ADR-0014（Network Adapter）、ADR-0016（Adapter 退出 Core）、ADR-0017（双轨 ECS）、ADR-0018（Shared 代码边界）、ADR-0019（网络抽象层定位）
- 保持有效: ADR-0011（ModuleHost 契约）、ADR-0012（Shadow csproj）、ADR-0013（Aspect 隔离纪律 → 随 Aspect 移除而归档）
- 新增: `docs/design/V2.0-route-C-prd.md`（本 ADR 的实施 PRD）
