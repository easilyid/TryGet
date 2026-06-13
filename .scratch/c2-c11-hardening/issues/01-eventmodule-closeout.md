# Issue 01 — C2 收尾：EventModule 异常上报策略 + 派发递归深度护栏

Status: done (2026/06/13, EditMode 368/368)

## 背景

`docs/strategy/V2-reference-framework-architecture-plan.md` §6ter.3 自审：C2（EventBus 零 GC + 生命周期）主体已落地（延迟增删/重入安全/per-handler 异常隔离/零 ToArray），剩三个收尾项。

## 范围

1. **异常上报策略**：handler 异常目前仅存入 internal `_lastPublishExceptions`，生产路径静默吞掉。新增 `IEventModule.HandlerException` 实例事件（`Action<Type, Exception>`），每个被隔离的 handler 异常触发一次；钩子自身异常吞掉防级联（对齐 TGTaskScheduler.UnobservedException 模式）。
2. **派发递归深度护栏**：EventModule 级 `_publishDepth`，超过 `MaxPublishDepth=32` 抛 `InvalidOperationException`（嵌套发布时该异常会被外层 per-handler 隔离捕获并上报，防栈溢出；来源参考 MyFramework EventSystem MAX_DEPTH，§6ter.2-4）。
3. 测试 + CHANGELOG + 计划文档关单（C2 标记完成）。

## 验收

- 新增测试：HandlerException 事件触发（类型/异常正确、其余 handler 不受影响、钩子异常不级联）；自递归发布不栈溢出且记录 InvalidOperationException；护栏触发后正常发布可恢复。
- 现有 350 EditMode 测试全绿；Shadow csproj 可编译。
