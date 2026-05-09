Status: ready-for-agent

# Dispatch World-level Events inside one World

## Parent

.scratch/framework-design/PRD.md

## What to build

实现 World-level Event，用于单个 World 边界内的通知。World-level Event 不跨 World。

## Acceptance criteria

- [ ] World 可以发布 World-level Event。
- [ ] 同一 World 内的订阅方可以观察该 Event。
- [ ] 其他 World 不会收到该 Event。
- [ ] World-level Event 不替代 Entity-level Event。
- [ ] 不引入全局 Event bus。

## Testing

- [ ] 测试 World 发布 Event 后本 World 内可观察。
- [ ] 测试另一个 World 不会收到 Event。
- [ ] 测试 Entity-level 和 World-level 范围不同。
- [ ] 测试无全局静态事件通道要求。

## Blocked by

- .scratch/framework-design/issues/15-dispatch-entity-level-events-locally.md
