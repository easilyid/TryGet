# Remaining Tests Todo

This list tracks high-value coverage that should remain visible after the one-off bug proposal documents were removed.

## P0

### Scheduler frame boundary coverage

- `Delay_FixedOnlyFrame_ThenEarlyOnlyFrame_FrameCountIncrementsCorrectly`

  Verify a fixed-only frame followed by an early-only frame increments `FrameCount` correctly and keeps elapsed-time accounting consistent.

- `Delay_FixedUpdate_UnscaledTimeMode_UsesFixedUnscaledTime`

  Verify `Delay(seconds, FramePhase.FixedUpdate, TimeMode.Unscaled, token)` uses the fixed unscaled time axis, not update-phase unscaled time.

## P1

### Scheduler fixed-time semantics

- `Delay_FixedUpdate_UsesFixedTimeAxis_NotUpdateTimeAxis`

  Use different Update and FixedUpdate delta values to prove FixedUpdate delays advance on the fixed time axis.

- `Delay_MultipleFixedUpdateInOneFrame_AccumulatesFixedTime`

  Verify multiple FixedUpdate ticks in one rendered frame accumulate fixed time without incorrectly advancing frame count per fixed tick.

### Procedure lifecycle integration

- `Pop_AsyncExit_TriggersCancelScope_OnExitCalled`
- `Stop_AsyncProcedure_TriggersOnExitAsync_ThenCancelScope`
- `Shutdown_AsyncProcedure_PendingEnter_CancelsTransition`

  Cover Pop / Stop / Shutdown cancellation behavior beyond the Replace path already covered by ADR-0021 tests.

### Procedure and Event integration

- Add a positive integration test proving a Procedure transition can publish and observe typed events through `EventModule` without leaking handlers across procedure lifecycle boundaries.

## P2

### Pooling policy documentation tests

- `Return_SameBody_Twice_ReleaseAlsoGuards`

  Document whether `TGTaskPool` double-return protection is expected in all configurations or only debug/assertion builds.

- `Return_OnReturnThrows_ExceptionIsSwallowed_NotBubbled`

  Document the current `PoolModule` behavior when `onReturn` throws. If the intended policy changes to warning or aggregation, update both implementation and test.
