# ADR-0017: Aspect 与 IPureComponent 双轨并存（Aspect & Pure Component Dual-Path ECS）

## Status

Accepted（V0.9 起生效）

## Context

V0.1–V0.8 期间 TryGet 的 ECS 组合层只有 **Aspect**——继承 `Aspect` 基类、行为内嵌（`OnAttach` / `OnDetach` / 自定义业务方法）、与 Entity 通过 `Attach(aspect)` 双向绑定（`Aspect.Owner` 反指 Entity）。

V0.9 调研主流商业框架的 ECS 路线后发现明显分歧：

| 框架 | ECS Component 形态 | 行为承载 |
|------|-----------------|---------|
| **TryGet 当前** | Aspect (class 含方法) | 行为内嵌 Aspect |
| **ET** | Component（纯 POCO，零方法） | 行为外置 `IEventSystem<Component, Event>` |
| **Fantasy** | 同 ET（POCO + 外置 Sytem） | 同上 |
| **TEngine** | 同 ET（GameFramework 体系） | 同上 |
| **hsenl** | Aspect-like + IPlug 切面 | 行为混合 |
| **传统 Unity DOTS** | IComponentData (struct, no methods) | ISystem 外置 |

V0.9 PRD（`docs/design/V0.9-plugin-pure-component.md`）面临的抉择：

1. **全面切换 ET 路线**：废弃 Aspect，引入 IPureComponent，所有业务迁移
2. **保持 Aspect 单轨**：忽略 ET 路线，只演化 Aspect
3. **双轨并存**：保留 Aspect 同时新增 IPureComponent

## Decision

**采用方案 3：Aspect 与 IPureComponent 双轨并存**。

### 1. 各自定位

| 维度 | Aspect | IPureComponent |
|------|--------|----------------|
| 风格 | OO（数据 + 行为同位置） | DOD（数据/行为分离） |
| 基类 | `abstract class Aspect` + `OnAttach` / `OnDetach` | 纯 marker interface（无方法） |
| Entity 绑定 | `Aspect.Owner` 反指 Entity，可双向访问 | 仅作为数据存储，无反向引用 |
| 行为承载 | Aspect 内嵌方法 + Entity Event 订阅 | 业务实现 `IComponentSystem<T>` 外置 |
| 存储位置 | `Entity._aspects` + `_aspectMask` （Query 友好） | `Entity._components`（独立 Dict） |
| 查询 | `entity.GetAspect<T>()` / `Query.WithAll<T>()` | `entity.GetComponent<T>()`（扩展方法） |
| Source Gen 友好度 | 低（方法签名不可预测） | 高（System 模式可 codegen） |
| 测试单元 | 中（需 Entity 上下文） | 高（POCO 直接实例化） |

### 2. 选择矩阵（业务侧）

| 场景 | 推荐 |
|------|------|
| 行为复杂、需订阅 Entity Event、操作 Owner 数据 | **Aspect** |
| 跨多个 Entity 的批量行为、System 风格 | **IPureComponent + IComponentSystem** |
| 单字段标识（Flag-like） | `Tag`（已有，V0.3） |
| 需要 Source Gen 自动生成调度 | **IPureComponent**（V0.9.5+） |

### 3. IPureComponent 的最小契约

```csharp
public interface IPureComponent { }                              // marker only
public interface IComponentSystem<TComponent>
    where TComponent : IPureComponent
{
    void OnAttach(Entity entity, TComponent component);
    void OnDetach(Entity entity, TComponent component);
}
```

- **无强制约束**（无 struct/class 限制、不强制无方法）
- 编译期不阻止业务给 IPureComponent 加方法——这是"文档纪律"
- struct component 当前走 boxed 存储（`Dictionary<Type, IPureComponent>`）；V0.9.5 Source Gen 后评估泛型化

### 4. V0.9 不做的事

- **不引入自动调度**：framework 不调 `IComponentSystem.OnAttach`，业务显式触发
- **不写 Component Mask**：Query 暂不支持 `WithAll<IPureComponent>`（保留 Aspect 单一 Query 通道）
- **不实现 Burst-friendly 存储**：boxed Dict 即可，V1.x 性能优化时再说

### 5. 为何不全面切换 ET 路线

| 反对理由 | 解释 |
|---------|------|
| **现有业务投资** | V0.1-V0.8 累积的 ~10 个 Aspect、对应测试、Sample demo 全部以 OO 风格写就 |
| **学习成本** | Aspect 的 OO 风格对 Unity / 游戏开发者直觉强，Component + System 需训练 |
| **小项目过度设计** | ECS DOD 的优势在大规模 Entity（万级），小型 Indie 项目 OO Aspect 反而更顺 |
| **混合可行** | 业务可同一 Entity 上同时挂 Aspect（OO 域）和 PureComponent（DOD 域） |

### 6. 为何不保持 Aspect 单轨

| 反对理由 | 解释 |
|---------|------|
| **Source Gen 友好度差** | V0.9.5 路线需要 codegen 触发 OnAttach/OnDetach；Aspect 方法签名不可预测 |
| **业务多样性** | 当业务确有"纯数据 + 批量 System"场景时，Aspect 强行包裹是过度设计 |
| **生态对齐** | 主流框架（ET/Fantasy/TEngine/DOTS）都走 POCO + System；TryGet 留一条对齐路径降低引入门槛 |
| **不增加 Core 体量** | IPureComponent 实际新增代码 < 100 行，成本极低 |

## Consequences

### 好处

- **业务自由选择**：行为复杂用 Aspect、数据 + 批量 System 用 PureComponent，按场景适配
- **生态友好**：从 ET/Fantasy 迁来的开发者可直接复用熟悉的 POCO 风格
- **Source Gen 铺路**：V0.9.5 Roslyn IIncrementalGenerator 可针对 IPureComponent 生成调度代码
- **测试受益**：PureComponent 是 POCO，单测无需 Entity 上下文
- **V0.1-V0.8 业务零迁移**：所有现有 Aspect 代码继续工作

### 风险

- **认知成本**：用户需理解两条路线的取舍（ADR-0017 + 选择矩阵缓解）
- **概念混淆**：可能误以为 Aspect 就是 Component（文档+命名差异化缓解，IPureComponent 命名前缀刻意区分）
- **Query 不对称**：V0.9 Query 只跑 Aspect mask，PureComponent 无法 Query。业务需自管 `Dictionary<Type, List<Entity>>` 或在 Aspect 上加索引（V1.x 评估 Query 扩展）
- **API 表面增加**：5 个扩展方法（AddComponent / GetComponent / HasComponent / RemoveComponent / ComponentCount）

### 不允许的退路

- ✗ 把 IPureComponent 升级为含 OnAttach/OnDetach 抽象基类：那就变回 Aspect，失去双轨意义
- ✗ 偷偷给 Component 加 Mask 让 Query 支持：会让 Aspect mask 路径性能退化（共享 BitArray256 槽位竞争）
- ✗ 因 IPureComponent 引入而 deprecate Aspect：违背"双轨并存"决策本意

## Future Work（V0.9.5 / V1.0+）

| 项 | 何时 | 备注 |
|----|------|------|
| Source Generator 自动 `IComponentSystem` 调度 | V0.9.5 | Roslyn IIncrementalGenerator 扫 `IComponentSystem<T>` 实现，生成 `OnAttach`/`OnDetach` hook 注入 `Entity.AddComponent`/`RemoveComponent` |
| 评估 PureComponent Query 支持 | V1.0+ | 是否给 PureComponent 独立 mask（与 Aspect mask 隔离） |
| struct PureComponent 泛型化（消除 boxing） | V1.x | `Dictionary<Type, IPureComponent>` → 类型化容器 + Source Gen |
| ECS 性能基准 | V1.x | Aspect vs PureComponent vs hybrid 在 1万/10万 Entity 下的对比 |

## 关联

- `docs/design/V0.9-plugin-pure-component.md`：V0.9 PRD（含 ECS 二级方案章节）
- ADR-0001：World/Entity/Aspect 起源决策
- ADR-0007：Aspect 行为范围（OnAttach/OnDetach + 自定义方法）
- ADR-0009：Query AllOf/NoneOf 语义（V0.9 仍只跑 Aspect mask）
- ADR-0013：Aspect 隔离纪律（V0.9 IPureComponent 不破坏此纪律——两条路线互不引用）
- ReferenceFramework/ET：纯数据 Component + IEventSystem 原型
- ReferenceFramework/Fantasy：同 ET
