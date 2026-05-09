Status: ready-for-agent

# Match Query by Tag presence

## Parent

.scratch/framework-design/PRD.md

## What to build

扩展 Query，使其可以按 Tag presence 筛选 Entity，并可与 Aspect presence 组合。

## Acceptance criteria

- [ ] Query 可以匹配拥有指定 Tag 的 Entity。
- [ ] Query 可以组合 Aspect presence 和 Tag presence。
- [ ] remove Tag 后 Query 结果变化。
- [ ] Query 不把 Tag 当作有状态数据。
- [ ] Query 仍限定在 World 边界内。

## Testing

- [ ] 测试单 Tag Query。
- [ ] 测试 Aspect + Tag 组合 Query。
- [ ] 测试 remove Tag 后 Entity 不再匹配。
- [ ] 测试已销毁 Entity 不进入 Tag Query 结果。

## Blocked by

- .scratch/framework-design/issues/06-add-and-remove-presence-only-tags.md
- .scratch/framework-design/issues/10-match-query-by-aspect-presence.md
