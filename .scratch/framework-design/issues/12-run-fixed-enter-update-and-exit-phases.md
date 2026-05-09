Status: ready-for-agent

# Run fixed Enter, Update, and Exit Phases

## Parent

.scratch/framework-design/PRD.md

## What to build

为 World 提供固定三阶段 Phase 生命周期：Enter、Update、Exit。V0.1 不支持自定义 Phase graph。

## Acceptance criteria

- [ ] World 可以进入 Enter Phase。
- [ ] World 可以执行 Update Phase。
- [ ] World 可以进入 Exit Phase。
- [ ] Phase 顺序固定为 Enter / Update / Exit。
- [ ] 不提供自定义 Phase graph 或 gameplay-specific stage taxonomy。
- [ ] Phase 术语不漂移为 State、Step、Mode。

## Testing

- [ ] 测试 Phase 执行顺序。
- [ ] 测试 Update 不能替代 Enter/Exit 语义。
- [ ] 测试 Exit 后生命周期结果明确。
- [ ] 测试公开命名使用 Phase。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
- .scratch/framework-design/issues/03-destroy-entity-through-world-lifecycle.md
