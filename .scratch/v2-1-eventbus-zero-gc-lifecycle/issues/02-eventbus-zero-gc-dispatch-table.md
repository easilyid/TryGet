Status: ready-for-agent

# EventBus steady-state zero-GC dispatch table

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

Replace the current Publish snapshot-array dispatch path with an internal per-event-type dispatch table that can synchronously invoke stable subscribers in subscribe order without allocating in steady state. Keep the existing public EventBus API and compatibility semantics.

## Acceptance criteria

- [ ] `Publish<T>` no longer calls `ToArray()` or creates an equivalent per-Publish snapshot array on the steady-state path.
- [ ] Stable subscribers are invoked synchronously in subscribe order.
- [ ] `Subscribe(null)` still throws `ArgumentNullException`.
- [ ] Duplicate Subscribe for the same event type and delegate still throws `InvalidOperationException`.
- [ ] `Unsubscribe(null)` and missing handler Unsubscribe remain no-ops.
- [ ] Removing the final handler for an event type still removes that event type from diagnostics/event-type enumeration.
- [ ] Public `IEventBus` remains generic and constrained to `where T : struct`.
- [ ] Core shadow project builds with 0 errors.

## Testing

- [ ] `Publish_InvokesHandlersInSubscribeOrder`
- [ ] `Publish_NoSubscribers_DoesNothing`
- [ ] `Subscribe_NullHandler_ThrowsArgumentNullException`
- [ ] `Subscribe_DuplicateHandler_ThrowsInvalidOperationException`
- [ ] `Unsubscribe_NullOrMissingHandler_DoesNothing`
- [ ] `Unsubscribe_LastHandler_RemovesEventTypeFromDiagnostics`
- [ ] `Publish_SteadyState_DoesNotAllocateSnapshot`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

- 01-eventbus-contract-red-tests.md
