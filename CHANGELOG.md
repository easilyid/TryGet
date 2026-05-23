# Changelog

V0.x 时期：未承诺时间，按 Gate criteria 升版本（design.md §12）。

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
