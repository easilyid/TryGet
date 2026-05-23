# Define Aspect behavior scope

## Status

Accepted

## Context

MyTryGetFramework's Aspect = state + local behavior (ADR-0002). The architecture review (architecture-review.md §2.1) identified that "local behavior" needs precise boundaries to prevent Aspect from becoming an implicit System.

Evidence from reference frameworks:
- **BigCat**: Explicitly separates GameUnit (data-only GComponent, line 33: "避免在组件中定义任何与具体业务挂钩的方法") from Scene (SComponent with update scheduling). GComponent only allows "data structure maintenance methods."
- **hsenl**: EC pattern where Components carry behavior but rely on Systems for cross-entity coordination.
- **Industry ECS** (Unity DOTS, Arch, Entitas): All limit Component to pure data; behavior lives exclusively in Systems.

The risk: if Aspect behavior is unbounded, Systems become redundant and the framework degenerates into a traditional OOP hierarchy with extra steps.

## Decision

Aspect local behavior is scoped as follows:

**Allowed:**
- Methods that operate exclusively on the Aspect's own fields (validation, clamping, state transitions, computed properties).
- Publishing Entity-level Events through an injected dispatch interface (the Aspect does not hold a direct reference to the Entity or World).
- Lifecycle callbacks invoked by the framework (OnAttach, OnDetach).

**Forbidden:**
- Accessing other Aspects on the same or different Entity.
- Accessing the World, other Entities, Systems, or external services directly.
- Holding references to other Aspects, Entities, or World.
- Performing cross-Entity logic (that belongs in System).

**Boundary rule:**
If a method requires knowledge beyond the Aspect's own fields, it belongs in a System.

## Consequences

- Aspect remains a "smart data object" with encapsulation, not a god-object.
- System is the sole location for cross-Entity coordination.
- Entity-level Event publication requires a lightweight dispatch mechanism injected into Aspect lifecycle (not a direct World reference).
- This is stricter than BigCat's SComponent (which has full update scheduling) but less strict than Unity DOTS (which forbids all behavior). The middle ground matches the project's EC/ECS hybrid intent.
