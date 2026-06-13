## Agent skills

### Issue tracker

Issues are tracked as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Triage uses the default five-label vocabulary. See `docs/agents/triage-labels.md`.

### Domain docs

This repo uses a single-context domain-doc layout. See `docs/agents/domain.md`.

## Source Generator

The `[Module]` / `[EventHandler]` auto-registration generator lives as **source** under `Tools/MyTryGetFramework.SourceGenerator/` (NOT just the dll). The dll at `MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll` is a build artifact — Unity can only load a precompiled generator dll, so it must be committed, but the `.cs` under `Tools/` is the source of truth.

To change generator behavior: edit `Tools/MyTryGetFramework.SourceGenerator/src/`, add/adjust tests in `Tools/MyTryGetFramework.SourceGenerator.Tests/`, then run `pwsh Tools/build.ps1` (or `./Tools/build.sh`) to test + rebuild + sync the dll, and commit the dll alongside the source. The dll only re-syncs on a Release build, so `dotnet test` won't touch it. See `Tools/README.md`.

