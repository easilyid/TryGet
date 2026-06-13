# Issue 02 — C11：TGTask 池化生命周期收口

Status: done (2026/06/13, EditMode 368/368；设计调整：未采用完成即 Version++，改消费侧统一归还 + 全路径 version 校验，理由见正文)

## 背景

`docs/strategy/V2-reference-framework-architecture-plan.md` Candidate 11。三项关联技术债：
- `TGTask.IsCompleted` 不校验 Version（`TGTask.cs:50-54`）
- Manual body 依赖显式 `tcs.Return()`，不调则漏 GC（`TGTaskCompletionSource.cs:57-66`）
- TGTaskScheduler 热路径每次 `new TGTaskCompletionSource()` 且 SetResult 后无法归还（`TGTaskScheduler.cs:146,160,180,260-267`）

## 设计（相对 C11 草案的调整）

草案写的「SetResult 时 Version++（完成即失效）」实施评审后**不采用**：它会破坏已钉住的 tcs TrySet 静默语义（`TGTaskCompletionSourceTests`）并引入 version+1 算术脆弱性。改用等效更小的方案：

1. **消费侧统一归还**：`Awaiter.GetResult` finally 与 `Forget` 对 **Builder 与 Manual** body 一律归还池（删除 TaskType 条件）。归还前的 version 检查天然防双重归还（第二个 struct 副本在进入 try 前即抛 Expired）。
2. **IsCompleted 全路径 version 校验**：`TGTask.IsCompleted`/`Awaiter.IsCompleted` 在 `Version != Body.Version` 时返回 true（过期=已消费=已完成），后续 GetResult 抛 `TGTaskExpiredException`——消灭「旧句柄读到他人状态」（06/11 修过的 bug 类从根上关闭）。
3. **tcs.Return() 加 version 守卫**：body 已被消费侧归还后调 Return 为 no-op（防双归还入池）。
4. **tcs 池化（非泛型）**：`TGTaskCompletionSource` 增加 internal `Rent()/Recycle()` 静态池；TGTaskScheduler 三类队列与 TimerModuleAsyncExtensions 改用池化 tcs，SetResult 后立即 Recycle（task 句柄先于 SetResult 捕获）。
5. `WaitForFrames(0)` 改返回 `TGTask.CompletedTask`（零分配，语义等价）。
6. 不取 hsenl 的线程同步机制（单线程模型）。

## 验收

- 新增测试：Manual body await 后回池；tcs.Return 双归还防护；过期句柄 IsCompleted=true + GetResult 抛 Expired；调度器 Yield/Delay 稳态循环池命中（body 池计数不增长）；FromException 消费后回池。
- 现有 350 EditMode 测试全绿；Shadow csproj 可编译。
- 计划文档 C11 范围按本设计修订并标记完成。
