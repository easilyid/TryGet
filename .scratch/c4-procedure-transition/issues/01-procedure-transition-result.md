# Issue — C4：Procedure Transition Result（可 await 的流程切换）

Status: done (2026/06/13, Unity EditMode 378/378；Shadow csproj 0 警告)

## 背景

`docs/strategy/V2-reference-framework-architecture-plan.md` Candidate 4 + §7.4。
ProcedureModule 暴露 `IsEntering`/`IsExiting`/`LastAsyncError` 三个内部异步状态字段，
调用者必须逐帧轮询才能知道异步切换是否完成/出错，是典型 shallow interface。

参考框架复查（2026/06/13）：TEngine（FsmState.ChangeState 同步）、BigCat（SceneMgr Add/Close 同步）、
hsenl（ProcedureLine 是 pipeline，对应 C6）——**没有一个把流程切换做成可 await 的 transition**，
异步性都靠状态内部自己 await。TryGet 已有 TGTask（C11 池化收口完成），可做出更 deep 的设计。

## 设计

- `IProcedureModule.Start/Push/Pop/Replace` 签名 `void` → `TGTask`（**向后兼容**：现有不接收返回值的调用照常编译，TGTask 是 struct 丢弃无副作用）。
- 同步流程返回 `TGTask.CompletedTask`；异步流程（IAsyncProcedure）返回切换全链完成时才完成的 task。
- 异步错误**双通道**：既写入 `LastAsyncError`（保留兼容，旧测试/旧代码不破坏），又通过 transition task 抛出（`await` 时 throw）。
- pending 时再切换：保持现有"拒绝"策略（`ThrowIfAsync`）；可 await 后正常用法是 await 上一个再切下一个，自然避免冲突。
- transition task 用 `new TGTaskCompletionSource()`（不池化 tcs 对象——切换非每帧热路径；body 由 await 消费侧归还，不 await 则 GC 兜底，低频可接受）。

### 范围收敛决策

- **Stop 保持 `void`**：它是"立即清栈 + 后台跑异步 exit"的终结语义（现有测试 Stop 后立即断言栈空），多个 exit 并发完成不适合单一 transition task。C4 plan 提到 Stop 可 await，但评估后收敛为不改 Stop，聚焦 Start/Push/Pop/Replace 这四个"切换到某状态"的操作。
- 保留 `IsEntering`/`IsExiting`/`LastAsyncError`：兼容，且 transition task 未被 await 时仍是错误兜底手段。

### 实现要点

内部 `EnterProcedure`/`ExitTop`/`BeginAsyncEnter`/`BeginSyncExit` 改为接受 `Action<Exception> onComplete` 回调，
在各自真正完成点（同步立即 / 异步 OnCompleted / 错误 catch）调用。公开方法用 `RunTransition` helper
把回调驱动包装成 TGTask：同步完成→CompletedTask/FromException，异步未完成→new tcs 并在 onComplete 时 Set。
Replace 串联两段（exit 异步完成 → enter 可能又异步）：exit onComplete 里若无错则 EnterProcedure(target, onDone)。

## 验收

- 现有 ProcedureModuleTests + AsyncProcedureTests 全绿（IsEntering/IsExiting/LastAsyncError 行为不变）。
- 新增 transition 测试：同步切换 task 立即完成；异步 enter/exit 完成后 task 完成；异步错误通过 await 抛出；Replace 串联两段异步；Pop 的 resume 在 task 完成前执行；pending 时再切换抛异常。
- Shadow csproj 可编译；Unity EditMode 全绿。
