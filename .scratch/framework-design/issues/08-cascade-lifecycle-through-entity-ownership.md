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

## Testing

- [ ] 测试销毁 parent 后 child 的外部状态。
- [ ] 测试 Detach child 后销毁原 parent，child 继续存在。
- [ ] 测试多层 Ownership 树的销毁结果。
- [ ] 测试 Reference/Handle 不触发生命周期级联。

## Blocked by

- .scratch/framework-design/issues/03-destroy-entity-through-world-lifecycle.md
- .scratch/framework-design/issues/07-attach-and-detach-entity-ownership.md
