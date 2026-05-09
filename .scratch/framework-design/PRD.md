Status: ready-for-agent

# MyTryGetFramework V0.1 Core Runtime PRD

## Problem Statement

MyTryGetFramework needs a precise V0.1 product definition for its core runtime skeleton before implementation begins. The project already has a domain language and ADRs that commit to a composition-centered model, but without a PRD the first implementation slices could drift into Unity-specific Scene, Component, Manager, UI, resource, Procedure/FSM, networking, hot-reload, configuration, or editor-tooling concerns.

The user needs a PRD that keeps V0.1 focused on the minimum runtime concepts required to validate the framework's composition model: World, Entity, Aspect, System, Phase, SystemGroup, Query, Event, Tag, and Adapter.

## Solution

Define MyTryGetFramework V0.1 as a small, testable core runtime skeleton built around World / Entity / Aspect composition and System-driven scheduling. The runtime should make Entity identity, Aspect ownership, Query matching, Phase execution, SystemGroup ordering, layered Event delivery, presence-only Tag checks, and Adapter boundaries explicit enough that future Unity integration can attach to the model without becoming the model.

The V0.1 product should prioritize a plain runtime model whose behavior can be tested without UI, resource systems, Procedure/FSM, networking, hot-reload, configuration tables, or editor tools. Unity-facing work is represented only by the Adapter concept and should remain outside the core runtime semantics.

## User Stories

1. As a framework user, I want to create a World, so that I can host an independent runtime instance.
2. As a framework user, I want a World to contain Entities, so that runtime objects have a clear ownership boundary.
3. As a framework user, I want an Entity to belong to exactly one World, so that Entity identity and lifecycle are unambiguous.
4. As a framework user, I want each Entity to have an EntityId unique within its World, so that Systems and queries can refer to Entities safely inside that World.
5. As a framework user, I want EntityId uniqueness to be scoped to a single World, so that multiple Worlds can run without requiring global identifiers.
6. As a framework user, I want to create and destroy Entities through the World, so that lifecycle rules remain centralized.
7. As a framework user, I want an Entity to own Aspects, so that capabilities are composed rather than inherited.
8. As a framework user, I want an Aspect to encapsulate its own state and local behavior, so that per-Entity capability state does not leak into Systems.
9. As a framework user, I want an Entity to attach an Aspect, so that I can add capabilities dynamically.
10. As a framework user, I want an Entity to detach an Aspect, so that I can remove capabilities without destroying the Entity.
11. As a framework user, I want an Entity to prevent duplicate ownership of the same Aspect kind when the runtime model requires uniqueness, so that Query results are predictable.
12. As a framework user, I want an Entity to expose whether it has a given Aspect, so that Systems can make composition-based decisions.
13. As a framework user, I want an Entity to provide access to its owned Aspects through a small interface, so that callers do not manipulate internal storage.
14. As an Aspect author, I want local Aspect behavior to stay inside the Aspect, so that Systems do not become bags of per-Entity state.
15. As a System author, I want to define a Query over Aspect composition, so that a System can select matching Entities.
16. As a System author, I want Query matching to be based on Aspect presence, so that the runtime stays aligned with the composition model.
17. As a System author, I want Query semantics to avoid becoming a scripting layer, so that V0.1 remains a core runtime skeleton.
18. As a System author, I want to run logic over Entities matched by a Query, so that cross-Entity scheduling rules are expressed in Systems.
19. As a framework user, I want Systems to own cross-Entity scheduling rules, so that Aspect state and System orchestration stay separate.
20. As a framework user, I want a System to avoid owning per-Entity capability state, so that Entity composition remains the source of runtime state.
21. As a framework user, I want the World to coordinate Systems, so that execution has a single runtime boundary.
22. As a framework user, I want World coordination not to replace Entity composition, so that the composition model remains primary.
23. As a framework user, I want a minimal Phase model, so that lifecycle execution is clear without custom phase graphs.
24. As a framework user, I want the Phase model to include Enter, Update, and Exit, so that lifecycle ordering is understandable in V0.1.
25. As a System author, I want Systems to run in a Phase, so that their execution timing is explicit.
26. As a framework user, I want a World to enter its Enter Phase, so that setup Systems can run before updates.
27. As a framework user, I want a World to run its Update Phase, so that runtime Systems can execute repeatedly.
28. As a framework user, I want a World to run its Exit Phase, so that teardown Systems can execute deterministically.
29. As a framework user, I want Phase behavior to remain fixed in V0.1, so that the first release does not overfit custom lifecycle taxonomies.
30. As a framework user, I want SystemGroups to organize System execution order, so that scheduling is explicit and testable.
31. As a framework user, I want SystemGroup to remain a pure scheduling group, so that it does not become a domain boundary or module system.
32. As a System author, I want Systems within a SystemGroup to run in deterministic order, so that behavior can be reasoned about and tested.
33. As a framework user, I want multiple SystemGroups to express responsibility and order, so that World execution can scale without introducing a broader module architecture.
34. As a framework user, I want SystemGroup semantics to stay independent from UI, resources, networking, or editor tooling, so that it remains part of the core runtime only.
35. As a framework user, I want Events to exist as a layered notification concept, so that runtime notifications can be discussed precisely.
36. As a framework user, I want Entity-level Events, so that an Entity and its Aspects can communicate about local changes.
37. As a framework user, I want World-level Events, so that runtime-wide notifications stay inside the World boundary.
38. As a System author, I want cross-System Events, so that Systems can coordinate without direct coupling.
39. As a framework user, I want Event semantics not to imply a full messaging platform, so that V0.1 remains minimal.
40. As a framework user, I want Events to avoid being called messages or signals in the core language, so that terminology stays aligned with the domain glossary.
41. As a framework user, I want Tags to represent presence only, so that lightweight markers do not become hidden state containers.
42. As a System author, I want Query matching to include Tag presence where appropriate, so that marker-style composition can be selected without creating empty stateful Aspects.
43. As a framework user, I want Tags to be distinct from Aspect state, so that any stateful capability remains an Aspect.
44. As a framework user, I want Tags to be addable and removable from an Entity, so that presence-only classification can change at runtime.
45. As a framework user, I want Query and Tag behavior to be testable without Unity, so that the core runtime remains portable.
46. As a framework user, I want an Adapter concept, so that external runtimes and facilities can connect to World, Entity, or Aspect concepts.
47. As a Unity integrator, I want Adapter boundaries to keep Unity Scene semantics outside the core World model, so that Unity scene loading does not define runtime identity.
48. As a Unity integrator, I want Adapter boundaries to keep Unity Component semantics outside the core Aspect model, so that framework composition does not conflict with Unity components.
49. As a framework user, I want core Aspects and Systems not to depend on Unity lifecycle APIs, so that the core model can be tested and reasoned about independently.
50. As a framework user, I want Adapter responsibilities to be explicit, so that future Unity integration does not leak into core Aspects or Systems.
51. As a framework user, I want Entity Ownership to support a parent-child tree, so that child Entity lifecycle can follow parent lifecycle by default.
52. As a framework user, I want each Entity to have at most one parent Entity, so that Ownership remains a tree rather than a graph.
53. As a framework user, I want child Entities to follow parent lifecycle by default, so that owned runtime structures are cleaned up predictably.
54. As a framework user, I want an Entity to be detachable from its parent, so that a child Entity can continue existing independently when explicitly detached.
55. As a framework user, I want Entity Reference to be separate from Ownership, so that weak relationships do not affect lifecycle.
56. As a framework user, I want Handle resolution to be explicit, so that weak Entity references can fail safely if the target no longer exists.
57. As a framework user, I want References not to change parent-child Ownership, so that relationship semantics stay clear.
58. As a System author, I want Query results to respect Entity lifecycle, so that destroyed Entities are not scheduled.
59. As a System author, I want Systems to operate only on Entities in the same World, so that runtime boundaries are preserved.
60. As a framework user, I want World shutdown to make Entity and System lifecycle consequences explicit, so that cleanup behavior is deterministic.
61. As a test author, I want to instantiate the core runtime without Unity objects, so that V0.1 behavior can be covered by fast tests.
62. As a test author, I want tests to exercise public runtime behavior rather than internal storage, so that refactors do not break tests unnecessarily.
63. As a maintainer, I want V0.1 interfaces to be small and stable, so that later UI, resources, Procedure/FSM, networking, hot-reload, configuration, and editor tools can be added through separate decisions.
64. As a maintainer, I want the PRD to use the project's domain language, so that implementation issues do not drift into avoided terminology.
65. As an agent implementing V0.1, I want explicit out-of-scope boundaries, so that I do not accidentally build broader framework features.
66. As an agent implementing V0.1, I want a ready-for-agent PRD, so that implementation issues can be sliced from it without another triage pass.

## Implementation Decisions

- Build V0.1 around the domain concepts World, Entity, Aspect, System, Phase, SystemGroup, Query, Event, Tag, and Adapter only.
- Treat World as the independent runtime boundary that owns Entity lifecycle, System execution, Phase progression, and SystemGroup scheduling.
- Treat Entity as the composition host for identity, lifecycle, Ownership, Reference, Tags, and owned Aspects.
- Treat Aspect as Entity-owned state plus local behavior, not as an anemic data-only component and not as a Unity Component.
- Treat System as the owner of cross-Entity scheduling rules over Query-selected Entities.
- Keep the Phase model fixed to Enter, Update, and Exit for V0.1.
- Keep SystemGroup as a pure scheduling group for ordering and responsibility only; it must not become a domain boundary, module system, or service registry.
- Define Query as a composition filter over Entity Aspect and Tag presence; it must not become a SQL-like, script-like, or general predicate language.
- Define Event as a layered notification concept with Entity, World, and cross-System levels; it must not become a broad messaging platform in V0.1.
- Define Tag as a presence-only marker. Any marker that needs state must be represented as an Aspect instead.
- Define Adapter as the boundary for Unity or other external runtime integration. Adapter code may connect to World, Entity, or Aspect concepts, but core runtime semantics should not depend on Unity Scene, MonoBehaviour, Unity Component, editor APIs, resources, UI, or networking.
- Preserve the ADR split between Aspect, System, and Adapter responsibilities: Aspect owns local state and behavior; System owns cross-Entity rules; Adapter owns external runtime integration.
- Include EntityId scoped to a single World, not a global identifier.
- Include Handle or weak Reference semantics for Entity relationships that do not participate in Ownership.
- Include parent-child Ownership where an Entity may have at most one parent, child lifecycle follows parent lifecycle by default, and explicit detach allows continued existence.
- Favor deep modules with small interfaces: the World lifecycle surface, Entity composition surface, Query matcher, System scheduler, Event layer, and Adapter boundary should each hide internal storage and ordering details.
- Do not include implementation details, source file paths, or code snippets in this PRD because the repository structure may change during implementation planning.

## Testing Decisions

- Tests should validate external behavior through the same runtime interfaces production callers use, not internal collections, private ordering structures, or implementation details.
- World lifecycle tests should cover Entity creation/destruction, World boundary enforcement, Phase progression, and shutdown behavior.
- Entity composition tests should cover Aspect attach/detach, Tag add/remove, EntityId uniqueness within a World, Ownership attach/detach, and weak Reference or Handle resolution.
- Query tests should cover matching by Aspect presence, Tag presence, combinations of required composition, and exclusion of destroyed or out-of-World Entities.
- System scheduling tests should cover deterministic execution through Phase and SystemGroup order.
- Event tests should cover the externally visible behavior of Entity-level, World-level, and cross-System notifications without asserting on the internal dispatch mechanism.
- Adapter boundary tests should use fake or minimal adapters to verify that external runtime integration can drive or observe core runtime concepts without making the core depend on Unity APIs.
- Good tests should make the ADRs executable: Aspect/System/Adapter responsibility separation, minimal three-stage Phase, Query/Event/Tag vocabulary, and SystemGroup as pure scheduling should all be protected by behavior tests.
- There is no established prior test suite in this repo yet, so V0.1 tests should become the first consumer-facing test examples for the runtime model.

## Out of Scope

- UI systems, window hosts, canvases, layers, UGUI integration, or UI resource loading.
- Resource systems, asset loading facades, Addressables, Resources, YooAsset, asset manifests, or resource release policy.
- Procedure/FSM startup flows or gameplay state machines.
- Networking, RPC, sockets, channels, sessions, server/client boundaries, or multiplayer concerns.
- Hot-reload, hot-update, HybridCLR, dynamic assembly loading, or runtime code replacement.
- Configuration tables, generated config code, Luban-style pipelines, schema import, or data table tooling.
- Editor tools, inspectors, code generators, validation windows, menu items, or UnityEditor integration.
- Full module systems, service locators, global managers, dependency injection containers, or reflection-based module discovery.
- Custom Phase graphs, gameplay-specific stage taxonomies, or user-defined lifecycle pipelines beyond Enter, Update, and Exit.
- A full scripting language, SQL-like query layer, or broad messaging/event-bus platform.
- Unity Scene semantics as a replacement for World, Unity Component semantics as a replacement for Aspect, or Manager/Module/Processor terminology in the core domain.

## Further Notes

This PRD intentionally narrows V0.1 to the smallest coherent runtime skeleton that can validate the composition model. It follows the current domain glossary and ADRs as the source of truth. `.scratch/framework-design/candidates.md` remains useful historical context, but does not override CONTEXT.md or docs/adr decisions.

Future work should be split into implementation issues after this PRD, with each issue preserving the same domain language and out-of-scope boundaries.
