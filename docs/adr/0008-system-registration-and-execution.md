# Define System registration and execution contract

## Status

Superseded by ADR-0011 (V2 redesign). V0.3 起 System 由 EntityWorld（本身一个 IModule）拥有，注册与执行按 IModule 拓扑序 + Phase 内的 SystemGroup 双层语义。本 ADR 描述的 V0.1 直注册模型不再生效。

## Context

The architecture review (§2.5) identified that System registration, execution signature, and state ownership were undefined before implementation. Issue-13 requires Systems to run over Query-selected Entities but does not specify how Systems join a World.

Evidence from reference frameworks:
- **TEngine**: `ModuleSystem` uses interface-type dictionary + priority integer for ordering. Simple but global-static and reflection-based.
- **BigCat**: `Node.StartModules()` with explicit registration and deterministic start/stop ordering. Modules register explicitly; lifecycle is owned by the container.
- **Entitas**: `Systems.Add(new MySystem())` — fully explicit, no reflection, no attributes. Systems added to a Feature (group) manually.
- **Unity DOTS**: `[UpdateInGroup]` attribute-based registration with compile-time system ordering. Powerful but requires code generation infrastructure.

V0.1 must balance simplicity (no code-gen, no reflection scanning) with determinism (predictable execution order).

## Decision

### Registration
- Systems are registered **explicitly** to a World instance via API call (e.g., `world.RegisterSystem<T>(system, group)`).
- Each System declares which **SystemGroup** it belongs to at registration time.
- Registration happens during the **Enter Phase** (or before World starts). Runtime dynamic add/remove of Systems is not supported in V0.1.
- No reflection scanning, no attribute-based discovery, no naming conventions.

### Execution Contract
- Within a SystemGroup, Systems execute in **registration order** (first registered = first executed).
- System receives Query-matched Entities as an enumerable/collection — it processes them however it needs (foreach or batch).
- System execution signature: `void Execute(IReadOnlyList<Entity> entities)` or equivalent — receives the full matched set per invocation.
- Systems are invoked once per Phase tick for their assigned Phase (Update by default).

### State Ownership
- Systems **may** hold cross-Entity state (timers, counters, caches, configuration) as instance fields.
- Systems **must not** hold per-Entity capability state (that belongs in Aspect).
- Rationale: Cross-Entity state is necessary for coordination logic (e.g., spawn timers, collision pair caches). The prohibition is on per-Entity state, not all state.

### Phase Binding
- A System is bound to exactly one Phase (Enter, Update, or Exit) at registration time.
- Enter-phase Systems run once during World startup.
- Update-phase Systems run every tick.
- Exit-phase Systems run once during World shutdown.

## Consequences

- Explicit registration provides full determinism and testability without infrastructure overhead.
- Registration order within a group is the execution order — simple, predictable, no priority integers needed.
- Systems can be tested by instantiation + direct `Execute()` calls with mock Entity lists.
- Future versions may add attribute-based convenience registration, but V0.1 remains explicit-only.
- The batch execution signature (receiving full entity list) allows both per-entity iteration and global algorithms (e.g., spatial queries).
