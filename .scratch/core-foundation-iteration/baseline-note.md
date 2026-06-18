Status: baseline-recorded (2026/06/18)

# Core Contract Baseline Note

## Verification

- `dotnet build ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj`
  - Result: passed, 0 warnings, 0 errors.
- `dotnet test ServerProject/MyTryGetFramework.Tests/MyTryGetFramework.Tests.csproj`
  - Result: passed, 448 tests passed, 0 failed, 0 skipped.

## Protected Core Contracts

This iteration protects the current route C Core foundation contracts:

- ModuleSystem lifecycle: service-interface registration discipline, explicit dependency topology, Priority tie-breaks only among topology-equivalent Modules, Initialize rollback, Shutdown reverse-order cleanup and aggregate failures, and FramePhase dispatch only after initialization.
- EventModule dispatch: typed struct events, deterministic Subscribe/Unsubscribe/Publish behavior, EventScope-owned subscription lifetime, handler exception diagnostics, nested publish protection, and EventHandlerRegistry metadata snapshots.
- TGTask scheduling and cancellation: pooled TGTask/TGTask<T> bodies with version guards, manual `TGTask.Abort()`, owner-scope `TGCancelSource`/`TGCancelToken`, phase-aware TGTaskScheduler operations, scaled/unscaled delay behavior, Shutdown cancellation, combinators, and cancellation exceptions staying out of unobserved exception reporting.
- Timer: callback-oriented one-shot and repeat timers, pause/resume/cancel behavior, scaled/unscaled time, long-frame catch-up, callback exception aggregation, Shutdown cleanup, and the explicit boundary between TimerModule callbacks and TGTaskScheduler await-oriented delays.
- Pool: generic pure C# object-pool creation, prewarm, Rent/Return, reset hooks, diagnostics, selected pool destruction, Shutdown cleanup, and debug double-return safety without Unity GameObject assumptions.
- Source Generator registration: `[Module]` and `[EventHandler]` compile-time registration metadata, generated runtime registry calls, diagnostics for invalid inputs, and `Generators~/` as the source of truth for the Unity-required analyzer dll artifact.

## Explicitly Out Of Scope

This iteration does not cover UI, resource implementations, scenes, audio, input, hot update, GameObjectPool, ECS, server abstractions, Plugin systems, runtime reflection scanning, or Unity/third-party Adapter implementations.

## Drift Review

The baseline verification found no current build or test failures. The protected scope matches `CONTEXT.md` and does not require a drift correction against ADR-0011, ADR-0012, ADR-0020, ADR-0021, or ADR-0022 for issue 01.
