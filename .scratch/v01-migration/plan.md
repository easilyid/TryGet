# V0.1 → V2 资产迁移规划

> V0.2 Gate criteria 之一："V0.1 的 35% 资产已迁移到新结构"。
> 本规划落地 `.scratch/framework-design-v2/design.md` §14「与 V0.1 的对话」的迁移决策。

## 1. 现状盘点

V0.1 Core 共 **15 个文件 / 1249 行**：

| # | 文件 | 行数 | V0.1 职责 |
|---|------|------|-----------|
| 1 | Aspect.cs | 62 | Aspect 基类（ADR-0007 隔离纪律） |
| 2 | Entity.cs | 288 | Entity 主体 + Tag / Aspect / Child 管理 |
| 3 | EntityEventDispatcher.cs | 68 | Entity 级别事件分发器 |
| 4 | EntityId.cs | 46 | EntityId 句柄类型 |
| 5 | Handle.cs | 56 | 弱引用 Handle |
| 6 | IEntityEventDispatcher.cs | 18 | EntityEventDispatcher 接口 |
| 7 | IWorldAdapter.cs | 27 | 旧版 Adapter 接口 |
| 8 | IWorldEventBus.cs | 12 | 全局事件总线接口（V0.2 已转为 IEventBus 别名）|
| 9 | Phase.cs | 24 | Phase 三档枚举（Enter/Update/Exit）|
| 10 | Query.cs | 134 | 查询 API |
| 11 | SystemBase.cs | 70 | System 基类 |
| 12 | SystemGroup.cs | 51 | System 分组（ADR-0006）|
| 13 | Tag.cs | 16 | Tag 类型 |
| 14 | World.cs | 315 | 运行时根：Entity + System + EventBus + 生命周期 |
| 15 | WorldEventBus.cs | 62 | IWorldEventBus 默认实现 |

## 2. 迁移决策表

按 design.md §14 给出的比例（35% 复用 / 25% 增强 / 25% 重写 / 15% 弃），落到具体文件：

### 直接复用（保留原位 或 平移到新位置）— 5 个 / 196 行 / **16%**

| 文件 | 目的位置 | 备注 |
|------|---------|------|
| Aspect.cs | Runtime/Core/Entity/Aspect.cs | ADR-0007 隔离纪律不变 |
| EntityId.cs | Runtime/Core/Entity/EntityId.cs | 句柄类型稳定 |
| Handle.cs | Runtime/Core/Entity/Handle.cs | 弱引用 API 稳定 |
| Tag.cs | Runtime/Core/Entity/Tag.cs | Tag 类型稳定 |
| Phase.cs | Runtime/Core/Entity/Phase.cs | 枚举本身稳定（ADR-0004 三档保留），但 SystemBase 重写 |

### 增强（接口稳定，实现优化）— 5 个 / 484 行 / **39%**

| 文件 | 目的位置 | 增强点 |
|------|---------|--------|
| Entity.cs | Runtime/Core/Entity/Entity.cs | 拆掉对 World 的强耦合（World 引用改为 EntityWorld 引用）<br>引入 BitArray256 类型索引（V0.3+ 优化点） |
| Query.cs | Runtime/Core/Entity/Query.cs | 接口保留，内部用 BitArray256 加速 |
| EntityEventDispatcher.cs | Runtime/Core/Entity/EntityEventDispatcher.cs | 接口对齐新的 IEventBus 模型 |
| IEntityEventDispatcher.cs | Runtime/Core/Entity/IEntityEventDispatcher.cs | 同上 |
| WorldEventBus.cs | Runtime/Core/Module/EventBus.cs | 重命名为 EventBus（已完成 V0.2 接口拆分） |

### 重写（V2 新接口）— 3 个 / 436 行 / **35%**

| 文件 | 目的位置 | 重写原因 |
|------|---------|---------|
| World.cs | Runtime/Core/Entity/EntityWorld.cs | World 承担过多职责，拆为：<br>- ModuleHost（框架根，V0.2 已完成）<br>- EntityWorld（玩法层根，本身也是 IModule） |
| SystemBase.cs | Runtime/Core/Entity/SystemBase.cs | ADR-0008 重写：System 注册改为按 Phase + DependsOn 拓扑序 |
| SystemGroup.cs | Runtime/Core/Entity/SystemGroup.cs | ADR-0006 重写：SystemGroup 改为 Phase 内的弱分组 |

### 弃（V2 不再需要）— 2 个 / 39 行 / **3%**

| 文件 | 弃原因 |
|------|--------|
| IWorldEventBus.cs | V0.2 已转为 IEventBus 别名，V0.3 直接删 |
| IWorldAdapter.cs | 被 ADR-0014 Adapter 模式替代（Adapters/* 各自定义自己的接口） |

### 已完成迁移（V0.2 已落地）— 12% (IEventBus + ModuleHost 接管 EventBus)

- WorldEventBus → EventBus 接口拆分（IEventBus）
- IWorldEventBus → 保留作为 IEventBus 兼容别名

## 3. 当前完成度

V0.2 Gate criteria 要求"V0.1 的 35% 资产已迁移到新结构"。

已完成（V0.2 阶段）：
- ✅ IEventBus 抽取并接入 ModuleHost（事件总线 76 行 = 6%）
- ✅ IWorldEventBus 保留作为兼容别名
- ✅ 新增 Common 三件套（Log/Timer/Pool）— 这是新增而非迁移

**已迁移占比：约 6%（仅 EventBus 一项）**。距离 35% 还差 29%。

## 4. V0.2 收官追加任务（达成 35% Gate）

为闭合 V0.2 Gate，需在 V0.2 收官前完成以下迁移：

### 任务 A：搭建 Entity 子目录骨架（仅平移文件，不改实现）
- 在 `Runtime/Core/Entity/` 下新建子目录
- 平移直接复用的 5 个文件（Aspect/EntityId/Handle/Tag/Phase）+ Entity.cs 主体到 `Runtime/Core/Entity/`
- **占比：~26%**（5 文件 196 行 + Entity.cs 288 行 = 484 行 / 1249 行 = 39%）

> 注意：平移而非重写。仅文件位置变更，namespace 保留 `TryGet`，已存在的引用关系不破坏。

### 任务 B：移除 IWorldAdapter（弃）
- 删除 `IWorldAdapter.cs` + 其使用点（FakeWorldAdapter 等）
- 更新相关测试 FakeAdapterTests
- **占比：~2%**

### 任务 C：World.cs 拆分骨架（重写第一步）
- 抽取 `EntityWorld.cs`：承接 Entity 管理 + System 调度（不含 EventBus，已迁出）
- 移除 World.cs 中已迁到 ModuleHost 的部分（EventBus）
- **占比：~25%**（World 315 行重写）

**V0.2 完成度预测：26% + 2% + 25% + 6% (已完成) = 59%** > 35% ✓

## 5. 不在 V0.2 范围（推迟到 V0.3）

- BitArray256 类型索引（Query/Entity 内部优化）
- Aspect 虚方法反射缓存
- ResourceModule / LubanConfigModule / FSM / Procedure 落地
- 完整 demo（登录 Procedure + 主菜单 UI）

## 6. 迁移注意事项

1. **不破坏现有测试**：所有 V0.1 EditMode 测试（WorldLifecycleTests / EntityAspectTests / TagTests / QueryTests / EventTests / OwnershipTests / FakeAdapterTests / SystemSchedulingTests）必须继续通过。
2. **namespace 保留 `TryGet`**：仅文件位置变更，namespace 不变。
3. **每个迁移子任务一次 commit**：避免大批量改动难以审阅。
4. **每个任务跟 Plan agent 验收**：按既有节奏（commit → review → fix）。
5. **V0.1 ADR 标记**：废止的 ADR-0006/0008 标记 status: Superseded，链接到 V2 新 ADR。

## 7. 状态追踪

- [ ] 任务 A：Entity 子目录骨架
- [ ] 任务 B：移除 IWorldAdapter
- [ ] 任务 C：World.cs 拆分骨架
- [ ] V0.2 Gate criteria 验收（六项全过 → V0.2 收尾，进 V0.3）

## 8. 关联文档

- `.scratch/framework-design-v2/design.md` §14：与 V0.1 的对话
- ADR-0011：ModuleHost + IModule 契约
- ADR-0012：Shadow csproj 双端编译（已完成）
- ADR-0013：Aspect 隔离纪律（保留）
- ADR-0014：Network/HotReload Adapter 抽象
