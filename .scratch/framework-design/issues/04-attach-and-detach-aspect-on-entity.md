Status: ready-for-agent

# Attach and detach Aspect on Entity

## Parent

.scratch/framework-design/PRD.md

## What to build

让 Entity 可以拥有 Aspect，并支持 attach、detach、has、get 这一条最小组合路径。Aspect 表达 Entity-owned state 和 local behavior，不叫 Component。

## Acceptance criteria

- [ ] Entity 可以 attach 一个 Aspect。
- [ ] Entity 可以判断是否拥有某类 Aspect。
- [ ] Entity 可以获取已 attach 的 Aspect。
- [ ] Entity 可以 detach 已 attach 的 Aspect。
- [ ] detach 后 Entity 不再报告拥有该 Aspect。
- [ ] 公开语言使用 Aspect，不使用 Component 替代核心概念。

## Testing

- [ ] 测试 attach 后 has/get 成功。
- [ ] 测试 detach 后 has/get 结果变化。
- [ ] 测试多个 Entity 的 Aspect 互不影响。
- [ ] 测试 Aspect 行为通过 Entity 组合路径可见。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
