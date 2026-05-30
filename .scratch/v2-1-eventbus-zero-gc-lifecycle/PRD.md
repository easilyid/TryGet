# V2.1 PRD — EventBus zero-GC lifecycle

> Source design: `docs/design/V2.1-eventbus-zero-gc-lifecycle.md`
> Strategy candidate: `docs/strategy/V2-reference-framework-architecture-plan.md` Candidate 2
> Status: in-progress

## Goal

Implement the V2.1 EventBus core upgrade with the smallest useful scope:

- Remove steady-state `Publish<T>` snapshot allocation caused by `ToArray()` or equivalent array snapshots.
- Preserve existing public `IEventBus` API and typed `struct` event contract.
- Preserve synchronous subscribe-order dispatch.
- Preserve next-publish-only semantics for Subscribe/Unsubscribe during dispatch.
- Isolate handler exceptions so later handlers still run.
- Guarantee pending add/remove flush from `finally` or equivalent logic even when handlers throw.
- Keep `EventScope` owner-managed.
- Keep `EventHandlerRegistry` changes limited to diagnostics/compatibility; no full SourceGen expansion in this slice.

## Non-goals

- No async/cross-thread EventBus.
- No weak integer event-id EventBus.
- No UnityEngine dependency in Core.
- No host-owned EventScope lifecycle model.
- No full Source Generator metadata rewrite.
- No runtime reflection scanning.

## Issues

1. `issues/01-eventbus-contract-red-tests.md`
2. `issues/02-eventbus-zero-gc-dispatch-table.md`
3. `issues/03-eventbus-reentrancy-pending-flush.md`
4. `issues/04-eventbus-handler-exception-diagnostics.md`
5. `issues/05-eventscope-lifecycle-boundary-tests.md`
6. `issues/06-eventhandlerregistry-metadata-diagnostics.md`
7. `issues/07-docs-sync-v2-1-eventbus.md`

## Verification commands

- `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`
- `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/Tools/MyTryGetFramework.SourceGenerator/MyTryGetFramework.SourceGenerator.csproj --no-restore`
- Unity EditMode tests for the framework project when Unity is available.
