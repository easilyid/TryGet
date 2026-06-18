Status: final-aligned (2026/06/18)

# Core Foundation Final Alignment

## Scope

This note closes the Core foundation iteration by recording the final documentation and reference-framework alignment for route C. It is limited to Core contracts: ModuleSystem, EventModule, EventScope, TGTask, TGTaskScheduler, TimerModule, PoolModule, GameLauncher, and Source Generator.

UI, resources, scenes, audio, input, hot update, GameObject pooling, ECS, server abstractions, Plugin systems, Unity adapters, and third-party runtime implementations remain out of scope.

## CONTEXT.md Terminology

`CONTEXT.md` still matches the implemented Core contracts:

- ModuleSystem owns explicit module registration, dependency topology, lifecycle dispatch, rollback, Shutdown aggregation, and FramePhase dispatch.
- EventModule owns typed struct event publish/subscribe behavior, EventScope lifetimes, handler diagnostics, mutation safety during dispatch, and generated handler registration metadata.
- TGTask remains the framework async primitive; TGTaskScheduler owns phase-aware queues, scaled/unscaled delays, cancellation registration cleanup, combinators, and Shutdown reset behavior.
- TimerModule remains callback-oriented and pure C#; TGTaskScheduler remains await-oriented. Timer async extensions do not introduce a separate TGCancelToken model.
- PoolModule remains a generic pure C# object pool with prewarm, rent/return, diagnostics, selected pool destruction, Shutdown cleanup, and debug double-return safety.
- GameLauncher remains the standard Core host factory for baseline services and generated registrations.
- Source Generator remains the compile-time registration path for `[Module]` and `[EventHandler]`, with zero runtime reflection scanning.

## ADR Alignment

The completed issues do not contradict the relevant ADRs:

- ADR-0011 is historical for the ModuleHost / IModule contract shape. Route C now names the runtime host ModuleSystem and the event service EventModule, but the core decision remains intact: explicit module contracts and lifecycle ownership stay in Core.
- ADR-0012 remains intact: Runtime/Core is validated through a shadow csproj and stays independent from Unity runtime dependencies.
- ADR-0020 remains intact: the implementation uses route C vocabulary and does not resurrect System, World, runtime reflection scan, UI, or server-side framework concepts in Core.
- ADR-0021 remains intact: cancellation is self-managed through TGCancelSource / TGCancelToken for owner-scope cancellation and TGTask.Abort for manual one-shot cancellation; BCL CancellationToken is not introduced into Core contracts.
- ADR-0022 remains intact: the Tests shadow csproj remains the verification path for Core runtime tests.

## Generator Workflow

`MyTryGetFramework/Assets/MyTryGetFramework/Generators~/README.md` already records the required generator workflow:

- `Generators~/` is the source of truth for generator behavior.
- `Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll` is the Unity-required analyzer artifact.
- Generator tests can be run with `dotnet test MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.sln`.
- Behavior-changing generator edits must run `pwsh MyTryGetFramework/Assets/MyTryGetFramework/Generators~/build.ps1` or the matching shell script so tests pass, Release builds, and the dll syncs.
- The dll sync target only runs for Release builds; `dotnet test` and Debug builds intentionally do not churn the Unity-loaded dll.

Issue 07 made no generator behavior changes, so running generator tests was sufficient and the committed dll was intentionally not rebuilt.

## ReferenceFramework Alignment

ReferenceFramework was used as evidence for contract shape and risk areas, not as source to port wholesale.

Adopted principles:

- Unified module lifecycle with explicit dependencies and diagnostics.
- Typed event dispatch with mutation protection during publish and visible handler failures.
- Timer pause, resume, repeat, and long-frame catch-up semantics.
- Pool diagnostics, reset hooks, Shutdown cleanup, and debug double-return detection.
- Compile-time diagnostics and generated registration metadata for modules and event handlers.

Rejected implementation details:

- Static global ModuleSystem.
- Runtime reflection scan for module or event-handler discovery.
- Int/string event id Core API.
- Unity timer dependencies in Core.
- GameObjectPool in Core.
- UI, resources, scenes, audio, input, hot update, ECS, server abstractions, and Plugin systems in this iteration.

## Final Verification Commands

Run these commands from the repository root:

```powershell
dotnet build ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj
dotnet test ServerProject/MyTryGetFramework.Tests/MyTryGetFramework.Tests.csproj
dotnet test MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.sln
```

Only for behavior-changing generator edits, also run:

```powershell
pwsh MyTryGetFramework/Assets/MyTryGetFramework/Generators~/build.ps1
```
