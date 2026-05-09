# MyTryGetFramework

MyTryGetFramework 是一个围绕组合建模的 Unity 游戏框架上下文。V0.1 只覆盖核心运行时骨架，用 World / Entity / Aspect 表达运行边界、组合宿主和能力切片，以变种 ECS 的方式组织运行时语义。

## Language

**World**:
一个独立的游戏世界实例，承载一组 Entity、System 和生命周期。
_Avoid_: Scene, Node, Domain

**Entity**:
World 内承载身份、层级和生命周期的组合宿主。
_Avoid_: GameObject, Actor, Object

**Aspect**:
挂载到 Entity 上、封装自身状态和局部行为的组合能力切片。
_Avoid_: Component, Trait, Part

**System**:
查询 Aspect 组合并执行跨 Entity 调度规则的运行单元。
_Avoid_: Manager, Module, Processor

**Phase**:
World 内固定的生命周期阶段，按进入、更新、退出组织调度。
_Avoid_: State, Step, Mode

**SystemGroup**:
World 内用于组织 System 执行顺序的调度分组。
_Avoid_: Layer, Category, Bucket

**Adapter**:
连接框架核心模型与外部运行环境或技术设施的桥接单元。
_Avoid_: Wrapper, Bridge, Proxy

**Query**:
System 用来按 Aspect 组合筛选 Entity 的条件表达。
_Avoid_: SQL, Script, Predicate

**Event**:
分层传播的通知概念，分为 Entity 内通知、World 内事件和跨 System 事件。
_Avoid_: Message, Signal, Bus

**Tag**:
只表达存在与否的轻量标记，不承载状态。
_Avoid_: Flag, Marker, Label

**EntityId**:
在单个 World 内唯一的实体标识。
_Avoid_: GlobalId, UUID

**Handle**:
指向 Entity 的弱引用句柄，需要时再解析目标。
_Avoid_: StrongRef, Pointer

**Ownership**:
Entity 对子 Entity 的所有权关系。
_Avoid_: Inheritance, Containment

**Reference**:
Entity 之间的弱引用关系，不参与所有权树。
_Avoid_: Ownership, Parent

**Attach**:
把 Entity 纳入父 Entity 的所有权树。
_Avoid_: Bind, Link

**Detach**:
把 Entity 从父 Entity 的所有权树中移出。
_Avoid_: Unbind, Release

**Scene**:
仅用于 Unity 场景或资源侧语义，不作为核心运行域。
_Avoid_: World, Runtime

## Relationships

- A **World** contains zero or more **Entities**.
- An **Entity** belongs to exactly one **World**.
- An **Entity** owns zero or more **Aspects**.
- An **Aspect** owns its own state and local behavior.
- A **System** uses **Query** to select target **Entities**.
- A **System** operates on **Entities** by matching their **Aspect** composition.
- A **System** owns cross-Entity scheduling rules, not per-Entity capability state.
- A **Phase** orders execution inside a **World**.
- A **SystemGroup** groups **Systems** for execution order and responsibility.
- An **Event** can exist at the **Entity**, **World**, or cross-**System** level.
- A **Tag** is a presence-only marker, not state.
- An **Adapter** connects **World**, **Entity**, or **Aspect** concepts to Unity or another external runtime.
- An **Entity** may have at most one parent Entity.
- A child **Entity** follows its parent Entity's lifecycle by default.
- A child **Entity** may be detached explicitly and continue to exist.
- An **Entity** can reference other **Entities** through **Handle** or weak **Reference** without joining the ownership tree.
- An **EntityId** is unique only within a **World**.
- A **World** coordinates **Systems** but does not replace **Entity** composition.

## Example dialogue

> **Dev:** "When a player enters battle, do we create a Unity Scene object for it?"
> **Domain expert:** "No — battle is represented by a **World**. The player is an **Entity**, movement and status are modeled as **Aspects**, battle ticks are handled by **Systems**, and Unity objects are connected through **Adapters**."

## Flagged ambiguities

- "Scene" is reserved for Unity scene loading unless explicitly qualified; use **World** for the framework runtime boundary.
- "Component" is avoided for core domain language because it conflicts with Unity components and traditional ECS terminology; use **Aspect** for the framework composition unit.
- "Behavior" must be qualified as either local **Aspect** behavior or cross-Entity **System** behavior.
- "Unity integration" belongs in **Adapters**, not in core **Aspects** or **Systems**.
- "Reference" never means ownership; use **Ownership** only for the parent-child tree.
- "Tag" never carries state; if state exists, it belongs in an **Aspect**.
- "Query" stays in the System-side filtering model and does not become a scripting layer.

## V0.1 scope

V0.1 only covers the core runtime skeleton: **World**, **Entity**, **Aspect**, **System**, **Adapter**, **Phase**, **SystemGroup**, and the minimum supporting vocabulary needed to make those terms precise. Editor tooling, resource pipelines, hot reload, networking, and full configuration systems are intentionally out of scope.
