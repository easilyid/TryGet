Status: ready-for-agent

# EventBus V2.1 contract red tests

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

Lock the V2.1 EventBus behavior contract into EditMode tests before changing implementation. The tests should describe the intended end-to-end behavior of Publish, Subscribe and Unsubscribe, including the intentional change from fail-fast handler exceptions to exception isolation with an observable diagnostic path.

## Acceptance criteria

- [ ] EventBus tests cover subscribe-order synchronous dispatch.
- [ ] EventBus tests cover Publish with no subscribers as a no-op.
- [ ] EventBus tests cover Subscribe during dispatch affecting next Publish only.
- [ ] EventBus tests cover Unsubscribe during dispatch affecting next Publish only.
- [ ] EventBus tests cover nested Publish and document the safe pending flush boundary.
- [ ] EventBus tests cover handler exception isolation and the chosen reporting contract.
- [ ] EventBus tests cover pending add/remove flush when a handler throws.
- [ ] EventBus tests include a steady-state Publish allocation check that excludes Subscribe, first-use bucket creation and pending list growth.
- [ ] The old fail-fast test is replaced or renamed so it no longer conflicts with V2.1.

## Testing

- [ ] `Publish_InvokesHandlersInSubscribeOrder`
- [ ] `Publish_NoSubscribers_DoesNothing`
- [ ] `SubscribeDuringDispatch_AffectsNextPublishOnly`
- [ ] `UnsubscribeDuringDispatch_AffectsNextPublishOnly`
- [ ] `NestedPublish_PendingChangesFlushAtOuterPublishBoundary`
- [ ] `Publish_HandlerThrows_ContinuesAndReports`
- [ ] `Publish_HandlerThrows_PendingChangesStillFlush`
- [ ] `Publish_SteadyState_DoesNotAllocateSnapshot`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

None - can start immediately
