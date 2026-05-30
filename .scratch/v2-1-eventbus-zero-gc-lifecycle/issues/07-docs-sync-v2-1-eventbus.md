Status: ready-for-agent

# V2.1 EventBus documentation sync

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

After implementation and verification, align V2.1 design and strategy documentation with the actual EventBus behavior. Record the final exception reporting strategy and the allocation test scope so future agents do not reintroduce drift.

## Acceptance criteria

- [ ] V2.1 design document status reflects the implemented and verified behavior.
- [ ] Strategy roadmap status reflects whether V2.1 is in-progress, landed, or blocked.
- [ ] Documentation records that steady-state Publish is allocation-free only after warmup and excludes Subscribe, first-use bucket creation, pending-list growth, diagnostics snapshots, and event-type enumeration.
- [ ] Documentation records the chosen handler exception reporting contract.
- [ ] Documentation records that Subscribe/Unsubscribe during dispatch are next-publish-only and pending changes flush on exception paths.
- [ ] No obsolete fail-fast Publish description remains in V2.1 docs.

## Testing

- [ ] Documentation review against implementation and tests.
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`
- [ ] Unity EditMode tests when Unity is available.

## Blocked by

- 05-eventscope-lifecycle-boundary-tests.md
- 06-eventhandlerregistry-metadata-diagnostics.md
