Status: ready-for-agent

# Combinators: Timeout / WhenAll / WhenAny

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

ADR-0021 D10 phase 3 (after core + owner-scope land):

- `TGCancelSource.CancelAfter(float seconds, ITGTaskScheduler scheduler)`: registers a `Delay` that `Cancel()`s the source — the Timeout primitive.
- `TGTask.WhenAll(params TGTask[])` / `WhenAll(IEnumerable<TGTask>)`: completes when all complete; aggregates exceptions; pooling-safe (version guard on child bodies).
- `TGTask.WhenAny(...)`: completes on the first; reports the winner index; losers are NOT force-cancelled (caller decides via its own token).
- `TGTask<T>` variants where they make sense.

## Acceptance criteria

- [ ] `CancelAfter` cancels token-bound pending tasks after the delay elapses.
- [ ] `WhenAll` completes after all complete; one faulted → `AggregateException`; all cancelled → cancelled.
- [ ] `WhenAny` completes on first; reports winner.
- [ ] Combinators don't double-return or leak pooled bodies (version guard holds under WhenAll/WhenAny).

## Testing

- [ ] `CancelAfter_CancelsAfterDelay`
- [ ] `WhenAll_AllComplete_ThenCompletes`
- [ ] `WhenAll_OneFaults_Aggregates`
- [ ] `WhenAny_FirstWins_ReportsWinner`
- [ ] `Combinators_NoDoubleReturn_PoolStable`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

issues/03
