Status: ready-for-agent

# Match Query by Aspect presence

## Parent

.scratch/framework-design/PRD.md

## What to build

实现最小 Query：按 Aspect presence 在一个 World 内筛选 Entity。Query 不成为 SQL、脚本层或任意 predicate 平台。

## Acceptance criteria

- [ ] Query 可以匹配拥有指定 Aspect 的 Entity。
- [ ] Query 可以匹配同时拥有多个 Aspect 的 Entity。
- [ ] Query 只匹配同一 World 内的 Entity。
- [ ] Query 不匹配已销毁 Entity。
- [ ] Query API 不暴露 SQL、脚本或通用 predicate 语义。

## Testing

- [ ] 测试单 Aspect Query。
- [ ] 测试多 Aspect Query。
- [ ] 测试跨 World Entity 不进入结果。
- [ ] 测试销毁 Entity 后 Query 结果更新。

## Blocked by

- .scratch/framework-design/issues/03-destroy-entity-through-world-lifecycle.md
- .scratch/framework-design/issues/04-attach-and-detach-aspect-on-entity.md
- .scratch/framework-design/issues/05-enforce-predictable-aspect-ownership-per-entity.md
