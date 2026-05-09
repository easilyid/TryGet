Status: ready-for-agent

# Enforce predictable Aspect ownership per Entity

## Parent

.scratch/framework-design/PRD.md

## What to build

明确单个 Entity 对同一种 Aspect 的拥有规则，使 Query 和 System 后续行为稳定。若 V0.1 采用“一种 Aspect 一份实例”的语义，则重复 attach 必须有确定结果。

## Acceptance criteria

- [ ] 同一 Entity 对同一 Aspect kind 的重复 attach 行为明确。
- [ ] Query 可依赖 Aspect presence 得到稳定结果。
- [ ] Aspect 存储细节不暴露给调用者。
- [ ] 不要求跨 Entity 共享 Aspect 实例。

## Testing

- [ ] 测试同一 Entity 重复 attach 同类 Aspect 的外部行为。
- [ ] 测试不同 Entity attach 同类 Aspect 互不影响。
- [ ] 测试 detach 后可重新 attach。
- [ ] 测试调用者不需要知道内部存储结构。

## Blocked by

- .scratch/framework-design/issues/04-attach-and-detach-aspect-on-entity.md
