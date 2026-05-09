Status: ready-for-agent

# Run a System over Query-selected Entities

## Parent

.scratch/framework-design/PRD.md

## What to build

让 System 在某个 Phase 中通过 Query 获取目标 Entities，并执行 cross-Entity scheduling rule。System 不拥有 per-Entity capability state。

## Acceptance criteria

- [ ] System 可以绑定或声明 Query。
- [ ] World 执行对应 Phase 时运行 System。
- [ ] System 只接收 Query 匹配的 Entity。
- [ ] System 不接收其他 World 的 Entity。
- [ ] System 不要求把 per-Entity state 放进 System 内。
- [ ] 术语使用 System，不使用 Manager、Module、Processor。

## Testing

- [ ] 测试 System 在 Phase 中被调用。
- [ ] 测试 System 接收到 Query 匹配 Entity。
- [ ] 测试 Aspect 变化后 System 输入变化。
- [ ] 测试销毁 Entity 后 System 不再接收它。

## Blocked by

- .scratch/framework-design/issues/10-match-query-by-aspect-presence.md
- .scratch/framework-design/issues/12-run-fixed-enter-update-and-exit-phases.md
