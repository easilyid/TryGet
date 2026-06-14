Status: ready-for-agent

# Scheduler TGCancelToken overloads + registration lifecycle

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

Add `TGCancelToken` overloads to `ITGTaskScheduler` / `TGTaskScheduler` for `Delay` / `Yield` / `WaitForFrames` (ADR-0021 D7):

- If token already cancelled → return `TGTask.FromCanceled()` (no queue entry).
- Else enqueue the tcs AND `token.Register(cancel-this-tcs)`: on cancel, `SetCanceled(tcs)` + remove from its phase queue + `Recycle`, guarded by a snapshot of the tcs body `Version`.
- On NORMAL completion (the `ProcessYieldQueue` / `ProcessDelayQueue` / `ProcessFrameWaitQueue` `SetResult` path): **deregister** the registration (`Dispose`) so a long-lived source doesn't accumulate registrations.
- Phase queue entries carry the registration handle so completion can deregister it.

Existing overloads delegate to the new ones with `TGCancelToken.None`, so current behavior is unchanged.

## Acceptance criteria

- [ ] `Delay`/`Yield`/`WaitForFrames` each gain a `(..., TGCancelToken)` overload; existing overloads pass `None`.
- [ ] Already-cancelled token → immediate canceled task, nothing enqueued.
- [ ] Cancel while pending → tcs SetCanceled, removed from queue, recycled; await throws OCE.
- [ ] Normal completion deregisters; a long-lived source after 1000 completed `Delay`s holds 0 live registrations.
- [ ] version guard: cancel after the tcs already completed+recycled is a safe no-op (no misfire onto a reused tcs).

## Testing

- [ ] `Delay_WithToken_AlreadyCancelled_NoEnqueue`
- [ ] `Delay_WithToken_CancelWhilePending_RemovesAndCancels`
- [ ] `Yield_WithToken_CancelWhilePending_Cancels`
- [ ] `WaitForFrames_WithToken_CancelWhilePending_Cancels`
- [ ] `LongLivedSource_AfterManyCompletions_NoRegistrationLeak`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Implementation notes (post code-read of TGTaskBody / TGTaskCompletionSource)

- `TGTaskBody.SetResult/SetException` are idempotent (no-op once `_completed`); `GetResult` rethrows the stored exception. So a "cancel vs normal-complete" race is first-writer-wins and never throws at the body level.
- `TGTaskCompletionSource.Recycle(tcs)` sets `_body = null`; afterwards `tcs.SetXxx()` throws `TGTaskExpiredException`, `tcs.Task.IsCompleted` is true, and the tcs object is reused by the next `Rent()`.
- Therefore the cancel callback MUST remove the queue entry **immediately** (not lazily): once the tcs is Recycled it gets reused, so a stale entry's `Tcs` reference becomes meaningless. Cancel callback shape: `if (tcs.Task.IsCompleted) return; else remove its entry from the phase queue + tcs.SetCanceled() + Recycle(tcs)`.
- Normal completion path (`ProcessYieldQueue`/`ProcessDelayQueue`/`ProcessFrameWaitQueue` after SetResult) MUST `reg.Dispose()` to deregister, else a long-lived source accumulates registrations (ADR-0021 D7).
- Queue entries must carry the `TGCancelRegistration` so completion can deregister it: extend the `YieldQueues` lists, `DelayedEntry`, and `FrameEntry` to hold the registration.
- Zero-GC tradeoff: phase 1 MAY use a captured closure for the cancel callback (one `Action` alloc per token-bound schedule); pooling the cancel-action object (Fantasy-style) to reach true zero-GC is a follow-up optimization, NOT required for correctness. The non-token overloads keep the existing zero-alloc path untouched.

## Blocked by

issues/02
