# MyTryGetFramework Verification Gate

This gate is the required pre-publish check list for changes under `MyTryGetFramework` Runtime, Tests, Source Generator, or Unity project settings.

## Required Checks

1. Core shadow project:

   ```powershell
   dotnet test ServerProject/MyTryGetFramework.Tests/MyTryGetFramework.Tests.csproj
   ```

   Proves `Runtime/Core` and EditMode tests still run outside Unity, preserving ADR-0012 and ADR-0022.

2. Source Generator tests and dll sync:

   ```powershell
   pwsh MyTryGetFramework/Assets/MyTryGetFramework/Generators~/build.ps1
   ```

   Proves `[Module]` / `[EventHandler]` generator tests pass and syncs the Release build output to `Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll`.

3. Unity EditMode:

   Run `MyTryGetFramework.Tests` in Unity Test Runner.

   Proves Unity asmdef, NUnit, and editor compilation behavior match the shadow project.

4. Unity PlayMode:

   Run `MyTryGetFramework.Tests.PlayMode` in Unity Test Runner.

   Proves `TryGetMonoEntry`, real Unity frame driving, scene lifecycle, time, cancellation, Procedure / Timer / Event / Pool integration, and Source Generator auto-registration work at runtime.

## Commit Checklist

```powershell
git status --short --untracked-files=all
```

Only include files that belong to the current change. Unity MCP packages, ProjectSettings, Graphics/URP settings, and local environment files should be committed only when they are part of the intended work.

## High-Risk Boundaries

- `TryGetMonoEntry`: failure rollback, shutdown cleanup, persistence, scene unload, and real EndOfFrame dispatch.
- `ModuleSystem`: registration discipline, dependency ordering, lifecycle rollback, shutdown aggregation, and frame dispatch fail-fast behavior.
- `TGTaskScheduler` / `TGCancelSource`: Unity time, phase-aware scheduling, cancellation, `CancelAfter`, pooling, and version guards.
- Source Generator: Unity Roslyn compatibility, conditional compilation, diagnostics, and PlayMode auto-registration.
