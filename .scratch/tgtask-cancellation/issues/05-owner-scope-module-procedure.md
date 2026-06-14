Status: ready-for-agent

# owner-scope binding: Module + Procedure

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

ADR-0021 D8 — bind a cancel source to owner lifecycles, **without** introducing a general ServiceScope (Route C discipline, ADR-0021 leaves ServiceScope to a future decision):

- A `Module` / `Procedure` owns a `TGCancelSource`, cancelled on `Shutdown` / `OnExit`.
- Provide a minimal way for owner code to get its scope token to pass into async ops (a `protected` accessor or a base helper — keep it small).
- On `Module.Shutdown` → `source.Cancel()` cancels all its pending tasks; on `Procedure.OnExit` similarly.
- `Procedure.OnPause` does NOT cancel (a paused procedure may resume); only `OnExit` / leave-stack cancels.

## Acceptance criteria

- [ ] A module can obtain its cancel token; `Shutdown` cancels tasks started with it.
- [ ] A procedure can obtain its cancel token; `OnExit` cancels its pending tasks.
- [ ] `OnPause` does NOT cancel (resume keeps pending alive).
- [ ] Switching procedure (`Replace` / `Pop`) cancels the leaving procedure's pending.
- [ ] No general ServiceScope / multi-layer scope introduced.

## Testing

- [ ] `Module_Shutdown_CancelsItsPendingTasks`
- [ ] `Procedure_OnExit_CancelsPending`
- [ ] `Procedure_OnPause_DoesNotCancel`
- [ ] `ProcedureReplace_CancelsLeavingProcedurePending`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

issues/03, issues/04
