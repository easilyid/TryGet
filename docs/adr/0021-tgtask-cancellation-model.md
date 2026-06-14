# ADR-0021: TGTask 取消模型 — 自研轻量取消（句柄式 Abort + token 式 Scope）

## Status

Accepted（2026/06/14；阶段 1–3 实现完成，Shadow csproj 0 警告 0 错误 + 临时 .NET 工程三轮端到端验证 ALL PASS；EditMode 测试 `CancellationTests.cs` 待 Unity 重跑确认）

## Context

`TGTask` 全家桶（ADR-0020 保留）已提供 struct 异步原语、池化、version 防过期、单线程 continuation。但它当前**只有"全有或全无"的取消能力**，缺一个可用的取消模型：

- `TGTaskScheduler.Shutdown()` 会把所有 pending tcs `SetCanceled()`，但**没有"局部取消"**——无法只取消某个流程 / UI / 场景发起的那一批 pending task。
- 取消的**落地机制已存在**：`SetException(OperationCanceledException)`（见 `TGTaskCompletionSource.SetCanceled` / `TGTask.FromCanceled`），await 处会 rethrow，async 链自然展开。**缺的只是"外部信号如何触发某个特定 task 取消"**。
- 没有 token 传递、没有 `Timeout`、没有 `WhenAll/WhenAny`。
- `TGTaskScheduler` 公开 API 仅 `Yield/Delay/WaitForFrames/DelayUntilPhase`（确认无取消重载）。

### 为什么现在做

取消不是某个 V2.x minor 的独立特性，而是**多个上层服务的公共前置**（见 `docs/strategy/V2-roadmap-tasks.md`）：

- **V2.3 UI**：Open/Close 返回 TGTask，需要"防并发过期 / 关窗时取消其加载"。
- **V2.4 资源**：异步加载与释放语义要与 TGTask、引用计数、Procedure 生命周期对齐。
- **V2.5 场景**：加载进度与取消策略要与 TGTask 协作。

这些服务一旦开建就会各自发明取消，所以取消模型必须**先于它们**定型，否则上层代码形态会反复返工。

### 三框架对标（实证依据）

对 TryGet 的三个参考来源（hsenl 是 TGTask 的直接设计来源；ET/Fantasy 经 deepwiki 查证官方仓库）做取消模型对比：

| | hsenl `HTask` | ET `ETTask` | Fantasy `FTask` |
|---|---|---|---|
| 取消载体 | **自研** `HTaskAborter : OperationCanceledException` | **自研** `ETCancelToken` | **自研** `FCancellationToken` |
| 用 .NET `CancellationToken`？ | 否 | 否（README：去掉烦人的 CancellationToken 传递） | 否 |
| 取消形态 | **句柄式** `task.Abort()`（owner 持 task） | **token 式** `new`+`Cancel()`+注册 action | **token 式** 注册 cancel action，`Cancel()` 触发 |
| 取消语义 | **异常型**（抛 OCE 子类，builder 识别后静默） | 异常型 | **返回值型**（`FTask<bool>=false`，await 后查返回值） |
| 与池化集成 | version 守卫防过期 | （deepwiki 未覆盖细节） | **cancel action 也从对象池 Rent**，零 GC |

> 注：ET 取消模型跨版本有演进（`docs/design/V2-commercial-framework-architecture.md` §3.1 另记有"ET9 起取消用 CancellationToken 传递"的说法，与 deepwiki 的 `ETCancelToken` 并存，细节存疑）。但**三家"自研、不依赖 .NET CancellationToken 的线程安全模型"的方向高度一致**，这是本 ADR 的核心依据，不依赖 ET 的精确实现细节。

**关键观察**：三个同类自研 task 框架，无一使用 .NET `CancellationToken`。理由一致——单线程游戏框架里，`CancellationToken` 的线程安全开销（volatile/Interlocked/锁）、`CancellationTokenSource` 的 class 分配、强制逐层传 token 的样板，都是负担。

## Decision

### D1. 方向：自研轻量取消，不用 .NET `CancellationToken`

三家实证 + Route C 单线程定位（CONTEXT.md「TGTask：单线程模型」）共同指向自研。`CancellationToken` 的线程安全在 TryGet 单线程主循环里是纯浪费，且与 TGTask「struct + 池化 + 零 GC」哲学冲突。

被否决的方案：直接用 `System.Threading.CancellationToken`（线程安全开销 + CTS class 分配 + 与 TGTask 池化是两套生命周期，交互易错）。

### D2. 双形态：句柄式 `Abort()` + token 式 `Scope`

取消有两类粒度，分别对标不同框架：

1. **句柄式 `TGTask.Abort()`**（对标 hsenl）：用于 **1:1** 取消——owner 持有单个 task 句柄，直接 `task.Abort()`。最轻，无额外类型。
2. **token 式 `TGCancelSource` + `TGCancelToken`**（对标 ET/Fantasy）：用于 **1:N** 取消——一个 source 派生 token 传给多个异步操作，`source.Cancel()` 一次取消整批。这是 owner-scope（流程/UI/场景关闭取消其名下全部 pending）的落点。

### D3. 取消语义：维持异常型（不采用 Fantasy 返回值型）

取消 = `SetException(OperationCanceledException)`，await 处 rethrow，async 链**自动中断**。与现状（`FromCanceled`）+ hsenl/ET 一致。

不采用 Fantasy 的返回值型（`FTask<bool>=false`）：返回值型零异常成本，但要求调用方**每处检查返回值**，漏检即继续执行，对业务不安全；异常型自动中断更符合"取消即终止整条链"的直觉。

### D4. registration 池化（对标 Fantasy）

token 注册的取消回调（registration）必须可池化 Rent/Return，零 GC。这解决了自研相对 .NET CTS 多出来的一块工作——长寿命 scope 上反复注册不会让 registration 列表膨胀（详见 D7）。

### D5. 取消异常不进 `UnobservedException`（对标 hsenl）

现状隐患：`TGTask.Forget()` 会把任何异常（含取消 OCE）送进 `TGTaskScheduler.UnobservedException` 全局钩子。但**取消是预期行为，不该上报为"未观察异常"**。

解法（对标 hsenl `HTaskAborter`）：定义类型化取消异常 `TGTaskAbortException : OperationCanceledException`，`Forget()` 与 `AsyncTGTaskMethodBuilder` 识别它并静默吞掉，不进 `UnobservedException`。

### D6. version 守卫防误杀已复用 tcs

取消回调持有 tcs 引用，但 tcs 正常完成后会 `Recycle` 进池并可能被复用。取消触发时若直接 `SetCanceled` 会误杀已复用的 tcs。**复用 TGTask 现有的 version 机制**：注册时快照 tcs 的 `body.Version`，触发时比对，不匹配则 skip。

### D7. registration 生命周期：完成时注销

仅靠 version 守卫不够——正常完成若不注销 registration，长寿命 scope（如 App 级）的 registration 列表会无限膨胀。因此 **scheduler 队列 entry 持有 registration，tcs 完成（SetResult）时一并注销并归还池**。这是本设计相对 .NET CTS（它自己管 registration）多出来的明确工作量，已计入。

### D8. owner-scope 绑定的落点

| Owner | source 创建 | `Cancel()` 时机 | 落地版本 |
|---|---|---|---|
| Module | `OnInit` | `Shutdown` | 阶段 2 |
| Procedure | `OnEnter` | `OnExit`（切流程取消上一个的 pending） | 阶段 2 |
| UI Window | Open | Close | V2.3 落地 |
| Scene | Enter | Exit | V2.5 落地 |

### D9. 命名

- 信号源：`TGCancelSource`（class，池化，owner 持有）
- 信号句柄：`TGCancelToken`（`readonly struct`，零分配传递，对标 .NET `CancellationToken` 的句柄角色）
- 单操作取消：`TGTask.Abort()`（对标 hsenl）
- 取消异常：`TGTaskAbortException : OperationCanceledException`
- 注册句柄：`TGCancelRegistration`（`readonly struct`，`Dispose` 注销）

**关于 `Token` 用词与 CONTEXT.md 的关系**：CONTEXT.md 中 `EventScope` 的 `_Avoid_` 列了 `Token`/`Owner`——那是针对**事件订阅作用域**这一概念（别把 EventScope 叫成 SubscriptionToken/Owner），**不禁止取消领域使用 `Token`**。取消句柄用 `TGCancelToken` 是行业标准语义（CancellationToken 谱系），与事件域不冲突。本 ADR 落地时同步更新 CONTEXT.md，把取消术语加入 glossary 并澄清这一区分。

### D10. 分阶段落地（每阶段 .NET 侧 Shadow csproj 可验证）

1. **阶段 1 取消核心**：`TGCancelToken`/`TGCancelSource`/`TGCancelRegistration` + scheduler 三个 token 重载 + registration 池化与注销（D4/D6/D7）+ `ThrowIfCancellationRequested`。
2. **阶段 2 句柄 + owner-scope**：`TGTask.Abort()`（D2）+ 取消 OCE 不进 `UnobservedException`（D5）+ Module/Procedure scope 绑定（D8）。
3. **阶段 3 组合子**：`Timeout`（`source.CancelAfter`）+ `WhenAll`/`WhenAny`。
4. **阶段 4（可选）**：与 .NET `CancellationToken` 的互操作桥（仅在确有需求时）。

## Consequences

### 好处

- 取消能力从"只能全取消"升级为"owner-scope 局部取消"，补齐 V2.3/V2.4/V2.5 的公共前置。
- 维持单线程、零锁、池化、零 GC，与 TGTask 哲学一致。
- 异常型自动中断语义对业务安全；句柄式 + token 式覆盖 1:1 与 1:N 两类粒度。
- 三家实证背书，命名与语义有据可循，降低后续被反复挑战的概率。

### 风险

- **池化 × 取消生命周期交织**是最硬的点（D6/D7）。缓解：version 守卫 + 完成即注销 + registration 池化，三者配合；阶段 1 必须有针对"取消已复用 tcs"和"长寿命 scope 不膨胀"的回归测试。
- 自研要自己管 registration 生命周期（.NET CTS 替你管）。缓解：D7 明确把它作为阶段 1 的一等工作量，不留给后期。
- 异常型取消有 throw 成本（hsenl 注释也提到）。缓解：性能敏感的高频路径可用句柄式 Abort 或后续评估返回值型旁路；当前取消属低频（关窗/切流程/超时），可接受。

### 不允许的退路

- ✗ 为"省事"直接引入 `System.Threading.CancellationToken`：违反 D1 与 Route C 单线程定位，把线程安全开销和 class 分配带进 Core。
- ✗ 取消异常进 `UnobservedException`：取消是预期行为，不是"未观察异常"，违反 D5。
- ✗ 用全局 `Shutdown` 式全取消假装实现了局部取消：那正是当前缺口本身。
- ✗ 跳过 registration 注销只靠 version 守卫：会导致长寿命 scope 的 registration 列表膨胀（D7）。

## 关联

- 保持有效：ADR-0011（ModuleSystem 契约）、ADR-0012（Shadow csproj 双端验证）、ADR-0020（路线 C 定位）
- 影响：`CONTEXT.md` 需加入取消术语（`TGCancelSource`/`TGCancelToken`/`Abort`）并澄清 `EventScope` 的 Token-avoid 边界
- 配套：`.scratch/tgtask-cancellation/`（PRD + TDD issues）为本 ADR 的实施拆解
- 前置于：V2.3 UI（`docs/strategy/V2-roadmap-tasks.md` §4）、V2.4 资源（§5）、V2.5 场景（§6）
- 对标信源：`ReferenceFramework/hsenl/`（本地源码：`HTask/Classes/HTaskAborter.cs`、`HTask.Functions.cs`）、deepwiki egametang/ET、deepwiki qq362946/Fantasy
