Status: ready-for-agent

# Cascade lifecycle through Entity Ownership

## Parent

.scratch/framework-design/PRD.md

## What to build

补齐 Ownership 对生命周期的影响：父 Entity 销毁时，默认带动子 Entity 生命周期；已 Detach 的子 Entity 不再受原 parent 销毁影响。

## Acceptance criteria

- [ ] 父 Entity 销毁时，仍 attached 的 child Entity 按默认生命周期规则被处理。
- [ ] Detach 后的 child Entity 不再随原 parent 销毁。
- [ ] 生命周期级联只沿 Ownership 树发生。
- [ ] 生命周期级联不沿 Reference 或 Handle 发生。
- [ ] 多层 Ownership 树销毁时，按叶子优先顺序（深度优先 post-order）依次标记为已销毁，确保子 Entity 的清理回调在父 Entity 之前执行。
- [ ] 级联销毁过程中已被标记销毁的 Entity 不可再被 Detach；尝试 Detach 已销毁 Entity 应被框架拒绝。
- [ ] World.Shutdown 关闭时，未显式销毁的剩余 Entity 通过 World 直接清理（不再走级联，因为父子关系在此时已无意义），并保证不重复触发 Aspect.OnDetach。

## Testing

- [ ] 测试销毁 parent 后 child 的外部状态。
- [ ] 测试 Detach child 后销毁原 parent，child 继续存在。
- [ ] 测试多层 Ownership 树的销毁结果（A → B → C，销毁 A 后 C/B/A 全部 IsDestroyed 为 true）。
- [ ] 测试叶子优先顺序：在 Aspect.OnDetach 中记录顺序，验证子 Entity 的 Aspect 在父 Entity 的 Aspect 之前 OnDetach。
- [ ] 测试 Reference/Handle 不触发生命周期级联。
- [ ] 测试 World.Shutdown 时所有 Entity（无论是否在 Ownership 树中）都被销毁，且 _entities/_entityList 被清空。

## Blocked by

- .scratch/framework-design/issues/03-destroy-entity-through-world-lifecycle.md
- .scratch/framework-design/issues/07-attach-and-detach-entity-ownership.md
