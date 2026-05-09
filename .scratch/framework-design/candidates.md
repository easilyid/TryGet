# MyTryGetFramework architecture candidates

> Scope: architecture candidates only. No implementation proposed here. Evidence comes from `.scratch/framework-design/research.md` and the current `MyTryGetFramework` Unity project state.

## Current project baseline

`MyTryGetFramework` is still close to a Unity template: runtime code is limited to the tutorial `Readme` asset model and its editor-only inspector, there are no project asmdefs, no framework runtime Modules, no tests, and only `Assets/Scenes/SampleScene.unity` is in build settings. That makes this the right moment to choose seams before framework code starts accumulating around Unity defaults.

## 1. Plain C# framework kernel with Unity as an Adapter

**Files**

- Current anchor: `MyTryGetFramework/Assets/Scenes/SampleScene.unity`
- Future layout candidate:
  - `Assets/MyTryGetFramework/Runtime/Core/`
  - `Assets/MyTryGetFramework/Runtime/Unity/`
  - `Assets/MyTryGetFramework/Tests/EditMode/`

**Problem**

The project currently has no framework entry Module. If the first real Implementation starts from `MonoBehaviour` objects in the scene, Unity lifecycle ordering becomes the implicit Interface: callers must know `Awake`, `Start`, scene presence, object persistence, and play-mode reload behavior. That Interface is Shallow because the caller learns almost as much Unity detail as the Implementation contains.

**Solution**

Create a Deep plain C# framework kernel as the primary Module. Unity should enter through one small `FrameworkProxy` Adapter whose job is only to create the kernel, pump lifecycle ticks, and dispose it. The seam is between Unity frame callbacks and the kernel lifecycle. The kernel owns startup/shutdown ordering; Unity only supplies time and hosting.

This follows the research direction from BigCat and hsenl: use Unity Proxy ergonomics without letting Unity become the root abstraction.

**Benefits**

- **Locality**: startup/shutdown bugs concentrate in the kernel lifecycle instead of being spread across scene objects.
- **Leverage**: Unity callers get one hosting Interface while the Implementation can later add module ordering, diagnostics, and disposal rules.
- **Testability**: edit-mode tests can instantiate the kernel without loading scenes or entering play mode.
- **Unity coupling control**: `UnityEngine` stays in the Adapter assembly; the Core assembly can use `noEngineReferences`.

## 2. Explicit module registry instead of global static lookup

**Files**

- Future layout candidate:
  - `Assets/MyTryGetFramework/Runtime/Core/Modules/`
  - `Assets/MyTryGetFramework/Runtime/Core/Lifecycle/`

**Problem**

The reference frameworks show a useful tension: TEngine's `ModuleSystem` is simple and practical, but global static access and reflection naming conventions make the Interface larger than it first appears. Callers must know naming conventions, initialization timing, singleton state, update priority, and shutdown behavior. Deletion test: deleting a pass-through static locator would not remove complexity; it would reappear in every caller that needs lifecycle and dependency ordering.

**Solution**

Use an explicit module registry owned by the framework kernel. Keep the public Interface small: register a Module instance/factory, resolve by declared Interface, initialize in deterministic order, update only Modules that opt into tick Interfaces, shut down in reverse order. Avoid type-name reflection as the default path.

This candidate should be a Deep Module: simple registration and lifecycle calls outside, all ordering and disposal rules inside.

**Benefits**

- **Locality**: lifecycle ordering and module ownership live in one Implementation.
- **Leverage**: a small registry Interface unlocks deterministic init/update/shutdown for every subsystem.
- **Testability**: tests can install fake Modules and assert ordering without Unity objects.
- **Small interfaces**: feature Modules depend on declared Interfaces, not global framework state.

## 3. Runtime/editor assembly split as a first-class constraint

**Files**

- Existing editor-only code: `MyTryGetFramework/Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`
- Existing runtime template code: `MyTryGetFramework/Assets/TutorialInfo/Scripts/Readme.cs`
- Future layout candidate:
  - `Assets/MyTryGetFramework/Runtime/MyTryGetFramework.Runtime.asmdef`
  - `Assets/MyTryGetFramework/Runtime.Unity/MyTryGetFramework.Runtime.Unity.asmdef`
  - `Assets/MyTryGetFramework/Editor/MyTryGetFramework.Editor.asmdef`
  - `Assets/MyTryGetFramework/Tests/MyTryGetFramework.Tests.asmdef`

**Problem**

There are currently no asmdefs, so Unity's default assembly hides dependency direction. The tutorial code is correctly folder-separated (`Editor/ReadmeEditor.cs` imports `UnityEditor`), but without asmdefs there is no assembly-level seam preventing future runtime files from importing editor-only APIs or preventing Core from taking Unity dependencies.

**Solution**

Adopt assembly layout before adding real framework Modules. Use a pure Core runtime assembly with no engine references, a Unity Adapter runtime assembly with engine references, an Editor assembly that references runtime but is editor-only, and a test assembly that references Core first. Treat asmdef references as the architectural Interface for dependency direction.

**Benefits**

- **Locality**: compile-time errors catch editor/runtime leakage where it happens.
- **Leverage**: every future Module inherits clean dependency direction without repeating policy in code reviews.
- **Testability**: Core tests run without Unity scene setup; Unity Adapter tests stay isolated.
- **Runtime/editor separation**: avoids hsenl-style editor code hidden behind `#if UNITY_EDITOR` inside runtime files.

## 4. Resource seam before choosing Resources, Addressables, or YooAsset

**Files**

- Existing package context: `MyTryGetFramework/Packages/manifest.json`
- Future layout candidate:
  - `Assets/MyTryGetFramework/Runtime/Core/Assets/`
  - `Assets/MyTryGetFramework/Runtime/Unity/Assets/`

**Problem**

The project has Unity packages that could support several asset paths, but no framework resource Interface yet. If UI/game Modules call `Resources`, Addressables, or YooAsset directly, the asset system choice becomes distributed knowledge. That is a Shallow design: each caller must understand load handles, async behavior, error modes, and release rules.

**Solution**

Define a small resource seam early, but keep the Implementation minimal. The Core-facing Interface should express framework-level asset intent and ownership rules; Unity-specific loading is an Adapter. Start with one Adapter only if needed, but design the seam around the deletion test: deleting the resource Module should cause load/release complexity to reappear across UI, procedure, and gameplay callers.

**Benefits**

- **Locality**: load/release policy, async semantics, and error modes live in one Implementation.
- **Leverage**: UI and procedure Modules can use assets without knowing the underlying Unity package.
- **Testability**: tests can use an in-memory Adapter to validate lifecycle and failure paths.
- **Unity coupling control**: asset package dependencies stay behind the Unity Adapter seam.

## 5. Startup procedure as a narrow Module, not a general gameplay state machine

**Files**

- Current build entry: `MyTryGetFramework/ProjectSettings/EditorBuildSettings.asset`
- Future layout candidate:
  - `Assets/MyTryGetFramework/Runtime/Core/Procedure/`
  - `Assets/MyTryGetFramework/Runtime/Unity/Bootstrap/`

**Problem**

The reference research recommends Procedure/FSM for startup visibility, but also warns against copying TEngine's hot-update/download complexity. In this project, there is currently only `SampleScene.unity`, so a broad procedure system would be Shallow: lots of Interface surface before there is real Implementation depth.

**Solution**

Make startup procedure a narrow Module focused on boot phases only: initialize framework, initialize resources, preload required assets, enter first scene/game state. Keep gameplay state machines out of this seam until the domain needs them.

**Benefits**

- **Locality**: startup order and failure handling stay in one place.
- **Leverage**: the project gains visible boot flow without inheriting hot-update/download architecture.
- **Testability**: procedure tests can run phase transitions with fake Modules and fake assets.
- **Small interfaces**: avoids exposing a full FSM Interface to every gameplay caller prematurely.

## 6. UI host seam around canvas/layer/window ownership

**Files**

- Existing package context: `MyTryGetFramework/Packages/manifest.json` includes `com.unity.ugui`
- Future layout candidate:
  - `Assets/MyTryGetFramework/Runtime/UI/`
  - `Assets/MyTryGetFramework/Runtime/Unity/UI/`

**Problem**

UGUI is available, but no UI architecture exists yet. The reference frameworks show the common trap: UI Managers quickly absorb prefab loading, canvas discovery, window stack, layer depth, single/multiple instance rules, and error handling. If callers open prefabs or find canvas roots directly, UI policy spreads across gameplay code.

**Solution**

Introduce UI only when the first real UI appears, and make it a Deep host Module: callers request windows by framework-level identity; the Implementation owns canvas roots, layers, instantiation, reuse, and close rules. Resource loading should go through the resource seam, not directly through Unity APIs.

**Benefits**

- **Locality**: all canvas/layer/window ownership rules live behind one Interface.
- **Leverage**: gameplay callers avoid prefab, transform, and sorting-order details.
- **Testability**: Core UI policy can be tested with fake window descriptors; Unity Adapter tests cover prefab binding separately.
- **Unity coupling control**: UGUI types stay in the Unity/UI Adapter, not in Core gameplay Modules.

## 7. Tests as consumers of interfaces, not extracted pure helpers

**Files**

- Future layout candidate:
  - `Assets/MyTryGetFramework/Tests/EditMode/`
  - `Assets/MyTryGetFramework/Tests/PlayMode/`

**Problem**

There are currently no tests. The risk is extracting pure helper functions only to make testing easy while the real bugs remain in lifecycle wiring, Unity hosting, and module ordering. That produces Shallow helper Modules and poor Locality.

**Solution**

Make tests consume the same Interfaces as production callers: framework kernel lifecycle, module registry, resource seam, startup procedure, and UI host seam. Use edit-mode tests for Core Modules and a small number of play-mode tests for Unity Adapters.

**Benefits**

- **Locality**: tests fail at the seam where architectural contracts live.
- **Leverage**: each Interface becomes both production entry point and test surface.
- **Testability**: avoids scene-heavy tests for Core behavior while still validating Unity Adapter integration.
- **Assembly layout**: test asmdefs force Core/Unity separation to stay honest.

## Recommended candidate path

If you want the smallest coherent path, take candidates 1, 2, and 3 first: Core kernel, explicit module registry, and asmdef separation. Candidates 4-6 should be pulled in only when the first resource/UI/startup use case appears. Candidate 7 should begin with the kernel and registry so tests shape the Interfaces from day one.

Which of these would you like to explore?
