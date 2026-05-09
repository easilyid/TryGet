Status: ready-for-agent

# Drive World through a fake Adapter without Unity leakage

## Parent

.scratch/framework-design/PRD.md

## What to build

定义最小 Adapter 边界，并用 fake Adapter 驱动 World 生命周期，证明外部运行环境可以连接核心模型，而核心不依赖 Unity。

## Acceptance criteria

- [ ] fake Adapter 可以创建或持有 World。
- [ ] fake Adapter 可以驱动 Enter / Update / Exit。
- [ ] Adapter 不拥有 Aspect state。
- [ ] Adapter 不承载 System scheduling rule。
- [ ] 核心运行时不依赖 Unity Scene、MonoBehaviour、Unity Component、UnityEditor、UI、资源、网络。
- [ ] Adapter 术语不漂移为 Wrapper、Bridge、Proxy。

## Testing

- [ ] 测试 fake Adapter 驱动 World Phase。
- [ ] 测试 fake Adapter 驱动 System 执行路径。
- [ ] 测试核心测试不需要 Unity 对象。
- [ ] 测试 Adapter 与 Aspect/System 职责分离。

## Blocked by

- .scratch/framework-design/issues/12-run-fixed-enter-update-and-exit-phases.md
- .scratch/framework-design/issues/13-run-a-system-over-query-selected-entities.md
