Status: ready-for-agent

# Resolve weak Entity Handles

## Parent

.scratch/framework-design/PRD.md

## What to build

提供 Handle 或 weak Reference 语义，让调用者可以弱引用 Entity，并在需要时解析目标。目标不存在或已销毁时，解析必须有明确结果。

## Acceptance criteria

- [ ] 可以从 Entity 创建 Handle。
- [ ] Handle 可以解析到仍然存在的 Entity。
- [ ] Entity 销毁后，Handle 解析能体现目标不可用。
- [ ] Handle 不保持 Entity 生命周期。
- [ ] Handle 不建立 Ownership。

## Testing

- [ ] 测试 Handle 解析活跃 Entity。
- [ ] 测试销毁 Entity 后 Handle 解析失败或返回明确不可用结果。
- [ ] 测试 Handle 不阻止 Entity 销毁。
- [ ] 测试 Handle 不改变 parent/child Ownership。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
- .scratch/framework-design/issues/03-destroy-entity-through-world-lifecycle.md
