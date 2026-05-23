# Define Query matching semantics for V0.1

## Status

Accepted

## Context

The architecture review (§2.2) identified that Query only specified "presence" matching without defining the standard matching predicates. Issue-10 covers Aspect presence matching but only implies All-of semantics. None-of (exclusion) is critical for common game patterns (e.g., "all Entities with Health but without Dead Tag").

Evidence from industry:
- **Unity DOTS**: `WithAll<T>()`, `WithAny<T>()`, `WithNone<T>()` — three predicates.
- **Entitas**: `Matcher.AllOf().AnyOf().NoneOf()` — three predicates.
- **Arch ECS**: `QueryDescription().WithAll<T>().WithAny<T>().WithNone<T>()` — three predicates.
- **Flecs**: Full query DSL with relations, inheritance, optional — high expressiveness.

All mainstream ECS frameworks support at minimum All-of and None-of. Adding None-of after V0.1 would break the Query builder API.

## Decision

V0.1 Query supports two matching predicates:

- **All-of**: Entity must possess all specified Aspects/Tags. This is the default inclusion filter.
- **None-of**: Entity must not possess any of the specified Aspects/Tags. This is the exclusion filter.

**Deferred to post-V0.1:**
- **Any-of**: Entity possesses at least one of the specified Aspects/Tags. Less common in practice; can be composed via multiple Queries when needed.

Query builder API should be designed so Any-of can be added later without breaking changes.

## Consequences

- Query API includes both inclusion (All-of) and exclusion (None-of) from day one.
- Common patterns like "match Health but not Dead" work out of the box.
- Issue-10 acceptance criteria should be updated to include None-of matching.
- The API surface remains small (two predicates) while covering the majority of real game query patterns.
