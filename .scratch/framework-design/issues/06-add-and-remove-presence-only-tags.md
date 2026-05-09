Status: ready-for-agent

# Add and remove presence-only Tags

## Parent

.scratch/framework-design/PRD.md

## What to build

为 Entity 提供 Tag presence 能力。Tag 只能表达存在与否，不能承载状态。

## Acceptance criteria

- [ ] Entity 可以 add Tag。
- [ ] Entity 可以判断是否拥有 Tag。
- [ ] Entity 可以 remove Tag。
- [ ] remove 后 Entity 不再报告拥有该 Tag。
- [ ] Tag API 不提供状态值存储。
- [ ] 有状态能力必须继续由 Aspect 表达。

## Testing

- [ ] 测试 add/has/remove Tag。
- [ ] 测试重复 add/remove Tag 的外部行为明确。
- [ ] 测试 Tag 不暴露值或状态接口。
- [ ] 测试 Tag 和 Aspect 可以同时存在但语义不同。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
