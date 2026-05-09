Status: ready-for-agent

# Create a World with isolated Entity creation

## Parent

.scratch/framework-design/PRD.md

## What to build

建立最小 World 运行边界，使调用者可以创建 World，并通过 World 创建 Entity。Entity 必须知道自己所属的 World，World 必须能识别自己持有的 Entity。

## Acceptance criteria

- [ ] 可以创建一个独立 World。
- [ ] 可以通过 World 创建 Entity。
- [ ] Entity 归属于创建它的 World。
- [ ] 一个 Entity 不会同时属于多个 World。
- [ ] World 可识别自己当前持有的 Entity。
- [ ] 不引入 Unity Scene、Manager、Module、GameObject 等核心域外概念。

## Testing

- [ ] 测试创建 World 后可创建 Entity。
- [ ] 测试 Entity 的 World 归属。
- [ ] 测试两个 World 创建的 Entity 不会混用归属。
- [ ] 测试核心行为不依赖 Unity 类型。

## Blocked by

None - can start immediately
