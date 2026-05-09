Status: ready-for-agent

# Dispatch cross-System Events during World execution

## Parent

.scratch/framework-design/PRD.md

## What to build

实现 cross-System Event，用于 Systems 在 World 执行路径中协调。它仍然是 Event 层次的一部分，不是完整消息平台。

## Acceptance criteria

- [ ] 一个 System 可以在执行时产生 cross-System Event。
- [ ] 另一个 System 可以在 World 执行路径中观察或响应该 Event。
- [ ] cross-System Event 受 World 边界约束。
- [ ] cross-System Event 不变成全局 Message Bus。
- [ ] 不引入异步网络、队列或外部消息系统。

## Testing

- [ ] 测试 System A 产生 Event，System B 在同一 World 执行路径中响应。
- [ ] 测试跨 World 不传播。
- [ ] 测试 SystemGroup/Phase 顺序下 Event 行为稳定。
- [ ] 测试不依赖内部 dispatch 结构断言。

## Blocked by

- .scratch/framework-design/issues/13-run-a-system-over-query-selected-entities.md
- .scratch/framework-design/issues/16-dispatch-world-level-events-inside-one-world.md
