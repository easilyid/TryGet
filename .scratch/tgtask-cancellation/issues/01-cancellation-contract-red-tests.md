Status: ready-for-agent

# TGTask cancellation contract red tests

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

Lock the cancellation behavior contract into EditMode tests **before** implementation (TDD red). Tests describe the intended end-to-end behavior of token-style cancellation, handle-style `Abort()`, owner-scope, registration lifecycle, and the `UnobservedException` exclusion — per ADR-0021 (D2/D5/D6/D7).

## Acceptance criteria

- [ ] Token already-cancelled before scheduling → task completes cancelled immediately (no queue entry).
- [ ] Token cancelled while task pending → awaiting it throws `OperationCanceledException`.
- [ ] Cancelling a `TGCancelSource` cancels ALL tasks derived from its token (1:N); unrelated tasks unaffected.
- [ ] `TGTask.Abort()` on a pending self-created task → await throws `OperationCanceledException`.
- [ ] Normal completion deregisters the cancel registration (no growth on a long-lived source after N completed ops).
- [ ] A cancelled task that is `Forget()`-ten does NOT raise `TGTaskScheduler.UnobservedException`.
- [ ] version guard: cancelling after the underlying tcs was completed+recycled does not throw and does not cancel a reused tcs.
- [ ] `ThrowIfCancellationRequested()` throws when cancelled, is a no-op otherwise.

## Testing

- [ ] `Delay_TokenAlreadyCancelled_CompletesCanceledImmediately`
- [ ] `Delay_TokenCancelledWhilePending_AwaitThrowsOCE`
- [ ] `CancelSource_CancelsAllDerivedTasks_UnrelatedUnaffected`
- [ ] `Abort_PendingTask_AwaitThrowsOCE`
- [ ] `CancelSource_NormalCompletion_DeregistersRegistration_NoLeak`
- [ ] `ForgottenCanceledTask_DoesNotRaiseUnobserved`
- [ ] `Cancel_AfterTcsRecycled_VersionGuardNoMisfire`
- [ ] `Token_ThrowIfCancellationRequested_ThrowsWhenCancelled`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

None - can start immediately
