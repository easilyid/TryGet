# TGTask Cancellation PRD — owner-scope cancellation for TGTask

> Source decision: `docs/adr/0021-tgtask-cancellation-model.md`
> Reference evidence: hsenl `HTaskAborter` (handle-style Abort), ET `ETCancelToken`, Fantasy `FCancellationToken` — all custom, none use .NET `CancellationToken`.
> Status: done (2026/06/14 — all 7 issues implemented; Shadow csproj 0 warn/0 err; temp .NET harness ALL PASS across 3 rounds; EditMode CancellationTests pending Unity rerun)

## Goal

Add a usable cancellation model to `TGTask` with the smallest useful scope (ADR-0021 phases 1–2):

- Self-researched cancellation, **NOT** `System.Threading.CancellationToken` (single-thread, zero-lock, pooled, zero-GC — consistent with the TGTask philosophy).
- Handle-style `TGTask.Abort()` for **1:1** cancellation (hsenl-style).
- Token-style `TGCancelSource` + `TGCancelToken` for **1:N** owner-scope cancellation (ET/Fantasy-style).
- Exception-based semantics: cancellation = `SetException(OperationCanceledException)`, await rethrows, async chain unwinds (current behavior + hsenl/ET; Fantasy return-value style rejected, ADR-0021 D3).
- `TGTaskScheduler` `Delay`/`Yield`/`WaitForFrames` gain `TGCancelToken` overloads.
- Registration pooled (Fantasy-style) **and** deregistered on completion (avoid long-lived scope leak).
- `version` guard reuse so cancellation never hits a recycled/reused tcs.
- Cancellation OCE does **not** flow into `TGTaskScheduler.UnobservedException` (typed `TGTaskAbortException` + builder/Forget recognition, hsenl-style).
- owner-scope binding for `Module` (Shutdown) and `Procedure` (OnExit).

## Non-goals

- No `System.Threading.CancellationToken` in Core (interop bridge is ADR-0021 phase 4, deferred).
- No multi-threading / ThreadPool.
- No return-value cancellation (`FTask<bool>=false` rejected, ADR-0021 D3).
- No UI/Scene scope wiring yet (lands with V2.3 / V2.5).
- No `UnityEngine` dependency in Core.

## Issues

1. `issues/01-cancellation-contract-red-tests.md`
2. `issues/02-cancel-token-source-core.md`
3. `issues/03-scheduler-token-overloads.md`
4. `issues/04-task-abort-handle-and-unobserved.md`
5. `issues/05-owner-scope-module-procedure.md`
6. `issues/06-combinators-timeout-whenall-whenany.md`
7. `issues/07-docs-sync-cancellation.md`

## Verification commands

- `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`
- Unity EditMode tests for the framework project when Unity is available.
- Optional: a temporary .NET console harness under `.scratch/` to assert cancellation logic, deleted after verification.
