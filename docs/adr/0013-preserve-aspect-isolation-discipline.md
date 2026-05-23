# 保留 Aspect 隔离纪律（对抗 hsenl Component 自由访问）

## Status

Accepted

## Context

V0.1 ADR-0007（见 `docs/adr/0007-aspect-behavior-scope.md`）定义了 Aspect 的行为边界：Aspect 是"smart data object"，只能访问自身字段 + 注入的 `IEntityEventDispatcher`，**不能**访问其他 Aspect / Entity / World。跨 Entity 的逻辑必须放进 System。

V2 设计借鉴了 hsenl 框架的若干优点（Entity 树结构、Bitlist 类型索引、虚方法反射缓存），评审过程中出现了一个分叉点：要不要顺手把 hsenl 的 Component 自由度也吸过来？

hsenl Component 模式：
- Component 内可写 `Owner.Parent.GetComponent<X>()`、`Owner.World.GetEntity(id)` 等表达式。
- 灵活，写小项目业务时手感顺，"想到什么写什么"。
- 代价是失去隔离边界：跨 Entity 逻辑可以散落在任何 Component 里，重构时难以追踪"这个 Aspect 到底改了谁的状态"。

V2 设计文档（design.md §3、§6、§13 纪律 #3）已明确"Aspect 词汇保留，不改名 Component；ADR-0007 纪律继承"。本 ADR 把这个决策正式归档，并补充 V2 阶段的执行手段。

## Decision

V2 保留 V0.1 ADR-0007 的 Aspect 隔离纪律，**不**采用 hsenl 的 Component 自由访问模式。差异化点写入框架核心定位。

**词汇与边界：**

1. **保留 `Aspect` 命名**，不改名为 `Component`。词汇承载纪律——`Aspect` 暗示"实体的一个侧面"，`Component` 暗示"可组合的零件"，前者的隔离感更强。
2. **保留 ADR-0007 全部禁/允清单**：
   - 允许：操作自身字段、通过注入的 `IEntityEventDispatcher` 发 Entity-level Event、框架触发的生命周期回调（OnAttach / OnDetach / OnEnable / OnDisable / OnUpdate）。
   - 禁止：访问其他 Aspect、访问 Entity / World / 其他 Entity、持有上述引用、写跨 Entity 逻辑。
3. **跨 Entity 逻辑只能写在 System**（System 在 V2 降级为可选批处理加速，但仍是跨 Entity 协作的唯一合法位置）。

**hsenl 优点的有限吸收：**

- `BitArray256` 类型索引：吸收，作为 `Entity._typeMask` 加速 Query。
- 虚方法反射缓存（启动期扫描哪些 `OnUpdate` 被实现以跳过空调用）：吸收，作为 Aspect 性能优化。
- Component 自由访问 Parent / World：**不**吸收。

**执行手段（分阶段）：**

| 阶段 | 手段 |
|---|---|
| V0.2 – V0.4 | 代码审查 + 命名约定 + `Aspect.Owner` 保持 `protected`、不外露 |
| V0.5+ | 引入 Roslyn analyzer，在编译期挡住 Aspect 内对 `Owner.Parent` / `Owner.World` / `Owner.GetAspect<>` 的访问 |

Analyzer 的具体规则设计列为 design.md §附 "未解决问题 #3"，由 V0.5 阶段处理。

## Consequences

**好处：**
- 跨 Entity 逻辑全部集中在 System，重构时定位影响面只需看 System 集合，不必扫遍 Aspect 实现。
- 框架在"hsenl-like 灵活 EC"和"DOTS-like 纯 ECS"之间保持中间立场（见 ADR-0007 Consequences 最后一段），与 V0.1 的 EC/ECS 混合意图一致。
- 与同类参考框架做出明确差异化：用户选 MyTryGetFramework 而不是 hsenl，看中的就是这层纪律。
- Aspect 行为可被单测覆盖（无 Owner.Parent 依赖，注入 Mock dispatcher 即可），测试金字塔健康。

**成本：**
- 学习曲线高于 hsenl：从 hsenl 迁移过来的人会问"为啥不让我直接 `Owner.Parent.GetComponent<X>()`"，文档要正面回答。
- 短期内（V0.2-V0.4）靠 review 把关，存在被绕过的风险，PR review checklist 必须列上"Aspect 是否访问了 Owner 之外的东西"。
- Roslyn analyzer 是真实工程量，V0.5 必须排期，不能一直靠口头纪律。
- 部分场景（如父 Entity 状态变化触发子 Entity Aspect 反应）需要绕一道 Event，比 hsenl 直接调用啰嗦。这是显式优于隐式的代价。