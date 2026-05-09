Status: ready-for-agent

# Preserve SystemGroup deterministic scheduling

## Parent

.scratch/framework-design/PRD.md

## What to build

引入 SystemGroup 作为纯调度分组，只负责组织 System 执行顺序和责任，不成为 module system、service registry 或 domain boundary。

## Acceptance criteria

- [ ] 可以把 Systems 放入 SystemGroup。
- [ ] SystemGroup 内 System 执行顺序确定。
- [ ] 多个 SystemGroup 的执行顺序确定。
- [ ] SystemGroup 不提供服务定位、模块注册或 domain boundary 能力。
- [ ] SystemGroup 术语不漂移为 Layer、Category、Bucket。

## Testing

- [ ] 测试同一 SystemGroup 内多个 System 的执行顺序。
- [ ] 测试多个 SystemGroup 的执行顺序。
- [ ] 测试 SystemGroup 不影响 Query 语义。
- [ ] 测试公开 API 不包含 module/service registry 行为。

## Blocked by

- .scratch/framework-design/issues/13-run-a-system-over-query-selected-entities.md
