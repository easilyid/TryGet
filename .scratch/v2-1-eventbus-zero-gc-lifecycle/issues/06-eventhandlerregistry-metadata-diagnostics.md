Status: ready-for-agent

# EventHandlerRegistry metadata diagnostics minimal enhancement

## Parent

.scratch/v2-1-eventbus-zero-gc-lifecycle/PRD.md

## What to build

Add only the minimal EventHandlerRegistry diagnostics/compatibility support needed for V2.1. Keep the existing registration path working for generated and handwritten code, preserve startup failure semantics, and do not expand this into a full Source Generator rewrite.

## Acceptance criteria

- [ ] Existing `Register(Action<IEventBus>)` path remains available and compatible.
- [ ] `ApplyAll(null)` still throws `ArgumentNullException`.
- [ ] A registration action failure during `ApplyAll` still fails fast.
- [ ] Any metadata overload or diagnostic record is optional and does not enter the EventBus Publish hot path.
- [ ] Diagnostics can answer the minimal useful set: event type, handler identity, source manifest/assembly when available, and registration order.
- [ ] No runtime reflection scanning is introduced.
- [ ] Source Generator project still builds if touched; otherwise it is intentionally left unchanged.

## Testing

- [ ] `EventHandlerRegistry_Register_ActionPath_RemainsCompatible`
- [ ] `EventHandlerRegistry_ApplyAll_NullBus_ThrowsArgumentNullException`
- [ ] `EventHandlerRegistry_ApplyAll_RegistrationFailure_RemainsFailFast`
- [ ] `EventHandlerRegistry_Diagnostics_ReportRegisteredHandlerMetadata`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj --no-restore`
- [ ] `dotnet build C:/Users/Heart/Documents/Learn/Project/TryGet/Tools/MyTryGetFramework.SourceGenerator/MyTryGetFramework.SourceGenerator.csproj --no-restore`

## Blocked by

- 04-eventbus-handler-exception-diagnostics.md
