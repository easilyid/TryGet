Status: ready-for-agent

# EventBus reentrancy-safe pending add/remove

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

Implement dispatch-time mutation handling so Subscribe and Unsubscribe called by handlers are deferred until a safe flush point. Preserve the old observable snapshot semantics: mutations made during a Publish affect the next Publish only, not the current handler set.

## Acceptance criteria

- [ ] Subscribe during dispatch does not run in the current Publish.
- [ ] Subscribe during dispatch is visible to the next Publish after the safe flush boundary.
- [ ] Unsubscribe during dispatch does not remove the handler from the current Publish.
- [ ] Unsubscribe during dispatch is visible to the next Publish after the safe flush boundary.
- [ ] Nested Publish behavior is deterministic and covered by tests.
- [ ] Pending add/remove is tracked per event type or with equivalent isolation, so unrelated event types do not get surprising global flush behavior.
- [ ] Pending changes are flushed from `finally` or equivalent logic at the outer safe boundary.
- [ ] Core shadow project builds with 0 errors.

## Testing

- [ ] `SubscribeDuringDispatch_AffectsNextPublishOnly`
- [ ] `UnsubscribeDuringDispatch_AffectsNextPublishOnly`
- [ ] `NestedPublish_PendingChangesFlushAtOuterPublishBoundary`
- [ ] `PendingChanges_AreScopedByEventType`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`

## Blocked by

- 02-eventbus-zero-gc-dispatch-table.md
