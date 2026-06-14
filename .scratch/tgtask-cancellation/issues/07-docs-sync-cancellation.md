Status: ready-for-agent

# Docs sync: CONTEXT glossary + ADR-0021 accept + ARCHITECTURE / CHANGELOG

## Parent

.scratch/tgtask-cancellation/PRD.md

## What to build

Only AFTER implementation lands and EditMode is green (docs follow code, no drift — see V2-roadmap §11.6):

- `CONTEXT.md`: add `TGCancelSource` / `TGCancelToken` / `Abort` terms + their relationships; clarify that `EventScope`'s `Token`/`Owner` avoid is event-subscription-domain-only and does not forbid the cancellation domain's `TGCancelToken`; update the `TGTaskScheduler` term (now offers `TGCancelToken` overloads).
- `docs/adr/0021-tgtask-cancellation-model.md`: Status `Proposed` → `Accepted`, recording the verification evidence (.NET build + EditMode green).
- `Assets/MyTryGetFramework/ARCHITECTURE.md`: note cancellation under the Async area.
- `CHANGELOG.md`: cancellation entry.

## Acceptance criteria

- [ ] CONTEXT glossary reflects the shipped cancellation terms (added only after the code exists).
- [ ] ADR-0021 marked Accepted with evidence.
- [ ] ARCHITECTURE + CHANGELOG updated.

## Testing

- [ ] grep `CONTEXT.md` shows the new terms present
- [ ] manual review of ADR-0021 status line

## Blocked by

issues/02, issues/03, issues/04, issues/05, issues/06
