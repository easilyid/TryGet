Status: ready-for-agent

# Dispatch Entity-level Events locally

## Parent

.scratch/framework-design/PRD.md

## What to build

实现 Entity-level Event，用于 Entity 和其 Aspects 的局部通知。这个 issue 只覆盖 Entity 层，不实现 World-level 或 cross-System Event。

## Acceptance criteria

- [ ] Entity 可以发布 Entity-level Event。
- [ ] Entity-level Event 只在目标 Entity 局部范围内可观察。
- [ ] Aspect 可以参与本 Entity 的局部 Event 行为。
- [ ] Event 不命名为 Message、Signal、Bus。
- [ ] 不引入完整消息平台。

## Testing

- [ ] 测试 Entity 发布本地 Event。
- [ ] 测试只有目标 Entity 局部观察者收到 Event。
- [ ] 测试其他 Entity 不受影响。
- [ ] 测试命名和 API 不使用 Message/Signal/Bus。

## Blocked by

- .scratch/framework-design/issues/04-attach-and-detach-aspect-on-entity.md
