Status: ready-for-agent

# TGCancelToken / TGCancelSource / TGCancelRegistration core

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

The core cancellation types (ADR-0021 D1/D2/D4/D6/D9), single-thread and zero-lock, living in `Runtime/Core/Async/`:

- `TGCancelToken` (`readonly struct`): `IsCancellationRequested`, `ThrowIfCancellationRequested()`, `Register(Action) -> TGCancelRegistration`, `None == default`, version-guarded against source reuse.
- `TGCancelSource` (class, pooled): `Token` property, `IsCancellationRequested`, `Cancel()` (iterate registrations, single-thread, each callback once), `Dispose`/`Recycle` (version++ invalidates outstanding tokens).
- `TGCancelRegistration` (`readonly struct`): `Dispose()` deregisters; the registration node is rented/returned from a pool (Fantasy-style) for zero-GC.

No `System.Threading.CancellationToken` anywhere (ADR-0021 D1).

## Acceptance criteria

- [ ] `default(TGCancelToken)` == None: `IsCancellationRequested` false, `Register` returns an already-inert registration, `ThrowIfCancellationRequested` no-op.
- [ ] `Register` after the source is already cancelled → callback invoked immediately.
- [ ] `Cancel()` invokes all registered callbacks exactly once; a second `Cancel()` is a no-op.
- [ ] `Registration.Dispose()` removes the callback so a later `Cancel()` won't invoke it.
- [ ] Source pooled: after `Recycle`, previously issued tokens report dead via version mismatch, not falsely active.
- [ ] No `System.Threading.CancellationToken` referenced.

## Testing

- [ ] `CancelToken_Default_IsNone`
- [ ] `Register_AfterCancelled_InvokesImmediately`
- [ ] `Cancel_InvokesAllOnce_SecondCancelNoOp`
- [ ] `Registration_Dispose_RemovesCallback`
- [ ] `Source_Recycle_VersionInvalidatesOldTokens`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

issues/01 (red tests should exist first)
