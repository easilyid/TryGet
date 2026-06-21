## Agent skills

### Issue tracker

Issues are tracked as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Triage uses the default five-label vocabulary. See `docs/agents/triage-labels.md`.

### Domain docs

This repo uses a single-context domain-doc layout. See `docs/agents/domain.md`.

## Source Generator

The `[Module]` / `[EventHandler]` auto-registration generator lives as **source inside the framework** at `MyTryGetFramework/Assets/MyTryGetFramework/Generators~/`. The trailing `~` makes Unity ignore the folder entirely (it won't try to compile the generator `.cs`, which references `Microsoft.CodeAnalysis`) — this is the same pattern Unity's own Netcode for Entities package uses (`Source~`). The dll at `MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Generators/MyTryGetFramework.SourceGenerator.dll` is a **build artifact** — Unity can only load a precompiled generator dll (hard constraint, confirmed in Unity 6 docs), so it must be committed, but the `.cs` under `Generators~/` is the source of truth.

To change generator behavior: edit `Generators~/MyTryGetFramework.SourceGenerator/src/`, add/adjust tests in `Generators~/MyTryGetFramework.SourceGenerator.Tests/`, then run `pwsh MyTryGetFramework/Assets/MyTryGetFramework/Generators~/build.ps1` (or the `.sh`) to test + rebuild + sync the dll, and commit the dll alongside the source. The dll only re-syncs on a Release build, so `dotnet test` won't touch it. See `Generators~/README.md`.


