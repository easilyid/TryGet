Status: ready-for-agent

# Scope EntityId uniqueness to one World

## Parent

.scratch/framework-design/PRD.md

## What to build

为 Entity 提供 EntityId，并确保 EntityId 只在单个 World 内唯一。多个 World 之间不要求全局唯一。

## Acceptance criteria

- [ ] 同一 World 内创建的 Entity 拥有不同 EntityId。
- [ ] EntityId 唯一性限定在 World 内。
- [ ] 不引入 GlobalId 或 UUID 作为核心语义。
- [ ] 可以通过 World 边界判断 EntityId 是否属于当前 World。

## Testing

- [ ] 测试同一 World 内 EntityId 不重复。
- [ ] 测试不同 World 可以独立分配 EntityId。
- [ ] 测试跨 World 不把 EntityId 当作全局身份使用。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
