Status: ready-for-agent

# TGTask.Abort() handle + cancellation excluded from UnobservedException

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

ADR-0021 D2/D5:

- `TGTask.Abort()` / `TGTask<T>.Abort()`: handle-style 1:1 cancel. A pending self-created (`Manual`) task → `SetException(TGTaskAbortException)`; version-guarded; a `Builder`-type task can't be aborted (throw, consistent with hsenl and with the existing `SetResult`/`SetException` guards).
- `TGTaskAbortException : OperationCanceledException`: typed cancel marker (hsenl `HTaskAborter` style).
- `Forget()` recognizes `OperationCanceledException` (incl. `TGTaskAbortException`) and silently swallows it — NOT into `TGTaskScheduler.UnobservedException`.
- Confirm `AsyncTGTaskMethodBuilder` doesn't double-report cancellation.

## Acceptance criteria

- [ ] `Abort()` on a pending Manual task → await throws OCE.
- [ ] `Abort()` on a completed task → no-op.
- [ ] `Abort()` on a Builder task → `InvalidOperationException` (consistent with SetResult/SetException guard).
- [ ] A forgotten cancelled task → no `UnobservedException`.
- [ ] A forgotten task with a REAL exception → still raises `UnobservedException` (regression guard, must not over-swallow).

## Testing

- [ ] `Abort_PendingManualTask_AwaitThrowsOCE`
- [ ] `Abort_CompletedTask_NoOp`
- [ ] `Abort_BuilderTask_Throws`
- [ ] `Forget_CanceledTask_NoUnobserved`
- [ ] `Forget_FaultedTask_StillRaisesUnobserved`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

issues/02
