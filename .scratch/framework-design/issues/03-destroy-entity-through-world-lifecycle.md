Status: ready-for-agent

# Destroy Entity through World lifecycle

## Parent

.scratch/framework-design/PRD.md

## What to build

补齐 Entity 销毁路径。Entity 的销毁应由 World 统一协调，销毁后的 Entity 不再被 World 视为活跃 Entity。

## Acceptance criteria

- [ ] World 可以销毁自己持有的 Entity。
- [ ] 销毁后的 Entity 不再出现在 World 的活跃 Entity 集合中。
- [ ] 销毁操作对非本 World 的 Entity 无效或不可执行。
- [ ] 销毁状态能被后续 Query/System 行为识别。

## Testing

- [ ] 测试 World 创建后再销毁 Entity。
- [ ] 测试销毁后的 Entity 不再可见。
- [ ] 测试 World 不能销毁其他 World 的 Entity。
- [ ] 测试重复销毁行为具有明确外部结果。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
- .scratch/framework-design/issues/02-scope-entityid-uniqueness-to-one-world.md
