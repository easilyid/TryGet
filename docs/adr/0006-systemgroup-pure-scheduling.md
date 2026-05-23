# Keep SystemGroup as a pure scheduling group

## Status

Superseded by ADR-0011 (V2 redesign). SystemGroup 在 V0.3 被重写为「Phase 内的弱分组」，不再作为独立模块系统的层级（其原职责已由 ModuleHost + DependsOn 拓扑接管）。

## Original Context

MyTryGetFramework V0.1 uses SystemGroup only to organize execution order and responsibility. It does not become a domain boundary or a module system, because that would add another layer of architecture before the core composition model has been validated.

