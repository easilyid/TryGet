Status: ready-for-agent

# Attach and detach Entity Ownership

## Parent

.scratch/framework-design/PRD.md

## What to build

实现 Entity 的 parent-child Ownership 树。一个 Entity 最多只能有一个 parent，可以 Attach 到父 Entity，也可以 Detach 后继续存在。

## Acceptance criteria

- [ ] Entity 可以 Attach 到父 Entity。
- [ ] 子 Entity 能报告自己的 parent。
- [ ] 父 Entity 能识别自己的 children。
- [ ] 一个 Entity 最多只有一个 parent。
- [ ] Detach 后子 Entity 不再属于原 parent。
- [ ] Reference 不参与 Ownership 树。

## Testing

- [ ] 测试 Attach 后 parent/children 关系。
- [ ] 测试重复 Attach 或更换 parent 的明确行为。
- [ ] 测试 Detach 后子 Entity 继续存在。
- [ ] 测试 Ownership 关系不跨 World 混用。

## Blocked by

- .scratch/framework-design/issues/01-create-world-with-isolated-entity-creation.md
- .scratch/framework-design/issues/03-destroy-entity-through-world-lifecycle.md
