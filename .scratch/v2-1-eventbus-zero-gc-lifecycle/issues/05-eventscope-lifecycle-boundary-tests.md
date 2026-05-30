Status: ready-for-agent

# EventScope lifecycle boundary tests

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

Strengthen EventScope lifecycle boundary coverage without changing the owner-managed scope model. Verify scoped subscription cleanup behavior remains compatible with the V2.1 EventBus internals and that ModuleHost does not silently become the owner of external scopes.

## Acceptance criteria

- [ ] Existing EventScope tests for reverse-order Dispose continue to pass.
- [ ] Existing EventScope tests for idempotent Dispose continue to pass.
- [ ] Existing EventScope tests for swallowing unsubscribe cleanup failures while continuing cleanup continue to pass.
- [ ] A scoped Subscribe performed after the scope has already been disposed leaves no residual handler.
- [ ] ModuleHost shutdown does not automatically Dispose externally owned EventScope instances.
- [ ] ModuleHost end-to-end tests continue to demonstrate modules explicitly Unsubscribe or Dispose their own scope during Shutdown.
- [ ] No new host-owned scope API is introduced.

## Testing

- [ ] `EventScope_Dispose_UnsubscribesInReverseOrder`
- [ ] `EventScope_Dispose_IsIdempotent`
- [ ] `EventScope_Dispose_ContinuesAfterUnsubscribeFailure`
- [ ] `EventScopeSubscribe_AfterDispose_DoesNotLeaveHandler`
- [ ] `ModuleHost_Shutdown_DoesNotDisposeExternalEventScope`
- [ ] Unity EditMode EventScope and ModuleHost test filters when Unity is available.

## Blocked by

- 04-eventbus-handler-exception-diagnostics.md
