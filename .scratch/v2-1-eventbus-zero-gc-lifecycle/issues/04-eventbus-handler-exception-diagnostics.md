Status: ready-for-agent

# EventBus handler exception isolation and diagnostics

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

Change Publish from fail-fast handler exception behavior to isolated handler execution. A throwing handler must not stop later handlers, and exceptions must remain observable. Use the smallest public-API-preserving strategy: execute all handlers, collect thrown exceptions for the Publish call, then report them through the chosen minimal diagnostic/throwing contract without expanding `IEventBus` unless a separate decision is made.

Recommended minimal contract for this issue: after all handlers run and pending mutations flush, throw an `AggregateException` containing all handler exceptions. If implementation chooses an internal diagnostics-only path instead, update the issue and tests to make the reporting path explicit before coding.

## Acceptance criteria

- [ ] A throwing handler does not prevent later handlers from running.
- [ ] Multiple throwing handlers are all represented in the reporting contract.
- [ ] The old fail-fast test is updated to the V2.1 behavior.
- [ ] Pending add/remove still flushes when one or more handlers throw.
- [ ] Exception reporting does not silently swallow exceptions with no diagnostic outlet.
- [ ] Public `IEventBus` API is preserved unless a separate approved issue changes it.
- [ ] Bootstrap and EventHandlerRegistry startup fail-fast semantics are not changed as part of this issue.
- [ ] Core shadow project builds with 0 errors.

## Testing

- [ ] `Publish_HandlerThrows_ContinuesAndReportsAggregate`
- [ ] `Publish_MultipleHandlersThrow_ReportsAllExceptions`
- [ ] `Publish_HandlerThrows_PendingChangesStillFlush`
- [ ] `Bootstrap_EventHandlerRegistryFailure_RemainsFailFast`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

- 03-eventbus-reentrancy-pending-flush.md
