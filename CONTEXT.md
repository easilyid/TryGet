# MyTryGetFramework

MyTryGetFramework 是一个围绕组合建模的 Unity 游戏框架上下文。V0.1 只覆盖核心运行时骨架，用 `World` / `Entity` / `Aspect` 表达运行边界、组合宿主和能力切片，以变种 ECS 的方式组织运行时语义。

## Language (核心词汇)

**World**:
一个独立的游戏世界实例，承载一组 `Entity`、`System` 和生命周期。
_Avoid_: Scene, Node, Domain

**Entity**:
`World` 内承载身份、层级和生命周期的组合宿主。
_Avoid_: GameObject, Actor, Object

**Aspect**:
挂载到 `Entity` 上、封装自身状态和局部行为的组合能力切片。
_Avoid_: Component, Trait, Part

**System**:
查询 `Aspect` 组合并执行跨 `Entity` 调度规则的运行单元。
_Avoid_: Manager, Module, Processor

**Phase**:
`World` 内固定的生命周期阶段，按进入 (Enter)、更新 (Update)、退出 (Exit) 组织调度。
_Avoid_: State, Step, Mode

**SystemGroup**:
`World` 内用于组织 `System` 执行顺序的调度分组。
_Avoid_: Layer, Category, Bucket

**Adapter**:
连接框架核心模型与外部运行环境或技术设施的桥接单元。
_Avoid_: Wrapper, Bridge, Proxy

**Query**:
`System` 用来按 `Aspect` 组合筛选 `Entity` 的条件表达。
_Avoid_: SQL, Script, Predicate

**Event**:
分层传播的通知概念，分为 `Entity` 内通知、`World` 内事件和跨 `System` 事件。
_Avoid_: Message, Signal, Bus

**Tag**:
只表达存在与否的轻量标记，不承载状态。
_Avoid_: Flag, Marker, Label

**EntityId**:
在单个 `World` 内唯一的实体标识。
_Avoid_: GlobalId, UUID

**Handle**:
指向 `Entity` 的弱引用句柄，需要时再解析目标。
_Avoid_: StrongRef, Pointer

**Ownership**:
`Entity` 对子 `Entity` 的所有权关系。
_Avoid_: Inheritance, Containment

**Reference**:
`Entity` 之间的弱引用关系，不参与所有权树。
_Avoid_: Ownership, Parent

**Attach**:
把 `Entity` 纳入父 `Entity` 的所有权树。
_Avoid_: Bind, Link

**Detach**:
把 `Entity` 从父 `Entity` 的所有权树中移出。
_Avoid_: Unbind, Release

**Scene**:
仅用于 Unity 场景或资源侧语义，不作为核心运行域。
_Avoid_: World, Runtime

## Relationships (关系)

- 一个 **World** 包含零个或多个 **Entity**。
- 一个 **Entity** 只能归属于一个确定的 **World**。
- 一个 **Entity** 拥有 (own) 零个或多个 **Aspect**。
- 一个 **Aspect** 拥有其自身的状态和局部行为。
- 一个 **System** 使用 **Query** 来筛选目标 **Entity**。
- 一个 **System** 通过匹配 **Entity** 的 **Aspect** 组合来对其进行操作。
- 一个 **System** 拥有跨 `Entity` 的调度规则，而不是拥有单 `Entity` 的能力状态。
- 一个 **Phase** 负责组织 **World** 内部的执行顺序。
- 一个 **SystemGroup** 将 **System** 分组，以管理执行顺序和职责。
- 一个 **Event** 可以存在于 **Entity** 级别、**World** 级别，或是跨 **System** 级别。
- 一个 **Tag** 只是一个表示存在的标记，不包含状态。
- 一个 **Adapter** 将 **World**、**Entity** 或 **Aspect** 概念与 Unity 或其他外部运行时连接起来。
- 一个 **Entity** 最多只能有一个父 `Entity`。
- 默认情况下，子 **Entity** 跟随其父 `Entity` 的生命周期。
- 子 **Entity** 可以被显式地解除绑定 (Detach)，并继续独立存在。
- 一个 **Entity** 可以通过 **Handle** 或弱 **Reference** 引用其他 **Entity**，但这不参与所有权 (Ownership) 树。
- 一个 **EntityId** 仅在一个 **World** 内部是唯一的。
- 一个 **World** 负责协调各个 **System**，但不会取代 **Entity** 的组合。

## Example dialogue (对话示例)

> **Dev (开发者):** "当玩家进入战斗时，我们需要为此创建一个 Unity Scene 对象吗？"
> **Domain expert (领域专家):** "不 — 战斗是由一个 **World** 来表示的。玩家是一个 **Entity**，移动和状态等能力被建模为 **Aspect**，战斗的逻辑滴答（Tick）由 **System** 来处理，而 Unity 对象则是通过 **Adapter** 连接进来的。"

## Flagged ambiguities (标记的歧义项)

- 除非明确说明，否则 "Scene" 保留给 Unity 的场景加载语义；请使用 **World** 来表示框架的运行时边界。
- 在核心领域语言中应避免使用 "Component"，因为它与 Unity components 和传统的 ECS 术语冲突；请使用 **Aspect** 表示框架的组合单元。
- "Behavior" (行为) 必须明确区分是局部的 **Aspect** 行为，还是跨 `Entity` 的 **System** 行为。
- "Unity integration" (Unity 集成) 属于 **Adapter** 的职责，不应出现在核心的 **Aspect** 或 **System** 中。
- "Reference" (引用) 绝不代表所有权；仅使用 **Ownership** 来表示父子树状关系。
- "Tag" (标签) 绝不承载状态；如果存在状态，它应该属于一个 **Aspect**。
- "Query" (查询) 保持在 System 端的过滤模型层面，不能变成一种脚本语言。

## V0.1 scope (V0.1 范围)

V0.1 仅涵盖核心运行时骨架：**World**, **Entity**, **Aspect**, **System**, **Adapter**, **Phase**, **SystemGroup**，以及使这些术语更精确所需的最小配套词汇。编辑器工具、资源管线、热重载、网络和完整的配置系统都**有意地不在本次范围内** (out of scope)。