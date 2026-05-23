# Scope Event layers for V0.1

## Status

Accepted

## Context

The architecture review (§2.4) identified that three-layer Event (Entity-level, World-level, Cross-System) risks over-engineering in V0.1. The review noted that Cross-System Event's distinction from World-level Event was unclear — both involve System-to-System communication within a World boundary.

Evidence from industry:
- **Unity DOTS**: No built-in event system. Uses one-shot Components or Tag patterns.
- **Entitas**: Reactive System (monitors Component changes). No explicit event bus.
- **Flecs**: Observer pattern for entity/component lifecycle events.
- **BigCat**: WorkerEvent by type dispatch — single-layer, type-keyed.
- **TEngine**: Generated GameEvent — single-layer.

None of the reference frameworks or industry ECS frameworks implement three separate event layers in their core.

## Decision

V0.1 implements **two** Event layers:

1. **Entity-level Event**: Entity publishes, its own Aspects observe. Used for local state change notification within an Entity's composition.
2. **World-level Event**: Published to the World scope, Systems and other subscribers within the same World observe. Used for cross-Entity coordination and System-to-System communication.

**Deferred to post-V0.1:**
- **Cross-System Event (Issue-17)**: Deferred. World-level Event already provides the channel for System-to-System communication. If a distinct Cross-System mechanism is needed (e.g., for ordering guarantees or filtered delivery), it will be designed based on real usage patterns from V0.1.

## Consequences

- Issue-17 is deferred to post-V0.1 scope. Issues-15 and 16 remain in scope.
- The Event API surface is smaller and more testable.
- World-level Event serves double duty for both "broadcast to the world" and "System-to-System coordination" in V0.1.
- If Cross-System Event is later needed, it can be added as a specialized World-level Event subscription with filtering, rather than a separate mechanism.
