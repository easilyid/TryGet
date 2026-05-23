# MyTryGetFramework V0.1 架构深度审阅

> 审阅范围：CONTEXT.md、6 篇 ADR、PRD、candidates.md、research.md、18 个实现 issue、三个参考框架（BigCat / TEngine / hsenl），以及业界 ECS 框架（Entitas、Arch、Unity DOTS / Entities、Flecs）的设计模式。

---

## 一、当前架构设计总体评价

### 1.1 做得非常好的方面

**领域语言一致性极强。** CONTEXT.md 建立了一套完整、无歧义的核心词汇表，并且这套词汇在 ADR、PRD、candidates.md、research.md 和全部 18 个 issue 中**零漂移**。每个概念都有明确的 _Avoid_ 列表，这在工业级框架设计中极为少见。与 TEngine 和 hsenl 的自由命名风格形成鲜明对比。

**ADR 与 PRD 之间的追溯链完整。** 6 篇 ADR 覆盖了全部核心决策，PRD 的 66 条 User Story 可以回溯到 ADR 和 CONTEXT.md 中的具体词汇。Issue 的 Blocked-by 依赖关系形成了有向无环图，确保了实现顺序的合理性。

**Scope 控制极其严格。** PRD 的 Out of Scope 列表明确排除了 UI、资源、Procedure/FSM、网络、热重载、配置、编辑器工具、完整模块系统、自定义 Phase graph 等。这比 TEngine（一个 V1 就同时包含模块系统、热更新、YooAsset、Luban、Procedure FSM、UI）和 hsenl（一个 Runtime 包含行为树、数值、物理、时间线等）的做法克制得多。

**Adapter 边界定义清晰。** ADR-0002 和 Issue-18 将 Adapter 明确定义为连接外部运行时的桥接单元，且规定核心运行时不依赖 Unity。这与 BigCat 的 "Core -> Unity bridge" 方向一致，优于 TEngine（GameEntry : MonoBehaviour 是根入口）和 hsenl（FrameworkProxy + 大量 Unity Singleton）的做法。

### 1.2 需要关注的风险和改进点

以下是审阅中发现的需要深入讨论的设计问题，按重要性排序。

---

## 二、核心设计深度分析

### 2.1 Aspect = State + Local Behavior — 一个关键的变种决策

**这是什么。** MyTryGetFramework 的 Aspect 不是传统 ECS 的纯数据 Component，而是 "Entity-owned state plus local behavior"。ADR-0002 明确说 "Aspect 拥有其自身的状态和局部行为"，PRD User Story 8 和 14 进一步强调 "Aspect 封装自身状态和局部行为" 以及 "局部 Aspect 行为留在 Aspect 内部"。

**业界对比。**

| 框架 | Component/Aspect 定义 | 行为位置 |
|---|---|---|
| **Unity DOTS/Entities** | `IComponentData` — 纯数据 struct，无行为 | 全部在 System 中 |
| **Arch ECS** | Component = 普通 C# class/struct，纯数据 | 全部在 Query lambda / System 中 |
| **Entitas** | Component = 纯数据 class，code-gen 驱动 | 全部在 IExecuteSystem 等 System 中 |
| **BigCat** | GameUnit + GameComp，GameComp 带行为 | 混合：Comp 有行为，Mgr 有调度 |
| **TEngine** | Module（非 ECS）| Module 内自治 |
| **hsenl** | Entity + Comp（EC 风格），Comp 带行为 | 混合：Comp 行为 + System 调度 |
| **MyTryGetFramework** | Aspect = state + local behavior | Aspect 局部行为 + System 跨 Entity 调度 |

**分析。** MyTryGetFramework 的选择更接近 EC（Entity-Component）模式而非严格的 ECS（Entity-Component-System）模式。EC 模式在 [SanderMertens 的 ECS FAQ](https://github.com/SanderMertens/ecs-faq) 中被明确区分：

> "EC frameworks, as typically found in game engines, are similar to ECS in that they allow for the creation of entities and the composition of components. However, in an EC framework, components are classes that contain both data and behavior."

这不是缺陷 — BigCat 和 hsenl 也采用了类似的混合路径。但它引入了一个需要明确回答的设计张力：

**⚠️ 需要明确的问题：Aspect 的 "local behavior" 边界在哪里？**

- 如果 Aspect 可以包含例如 `Health.TakeDamage(int amount)` 这样的方法，那么 System 何时介入？
- 如果 Aspect 的行为只限于自身状态的 getter/setter/validation（例如 `Health.Clamp()`），那么这更像是封装良好的数据对象，与纯数据 Component + 独立 util 函数等价。
- 如果 Aspect 行为可以引用其他 Aspect（例如 `Attack.Execute()` 需要访问 `Health`），那就打破了 Aspect 的局部性，变成了隐式的 System。

**建议。** 在 CONTEXT.md 或专门 ADR 中增加 "Aspect behavior scope" 的精确定义，例如：
- **允许**：操作 Aspect 自身字段的验证、计算、状态转换方法。
- **禁止**：访问其他 Aspect、其他 Entity、World、或外部服务。
- **边界场景**：发布 Entity-level Event 是否算 "local behavior"？如果是，需要定义 Event 发布的依赖注入机制。

### 2.2 Query 的表达力与实现策略

**当前设计。** PRD 将 Query 定义为 "基于 Entity Aspect 和 Tag 存在的组合过滤器"，明确不成为 SQL、脚本或通用 predicate。Issue-10 和 Issue-11 分别覆盖 Aspect presence matching 和 Tag presence matching。

**业界对比。**

| 框架 | Query 机制 | 表达力 |
|---|---|---|
| **Unity DOTS** | `EntityQuery` + `WithAll<T>` / `WithAny<T>` / `WithNone<T>` | 中等 — 支持包含/排除/可选 |
| **Arch** | `QueryDescription().WithAll<T>().WithAny<T>().WithNone<T>()` | 中等 — 同上 |
| **Entitas** | `Matcher.AllOf().AnyOf().NoneOf()` | 中等 — 同上 |
| **Flecs** | 完整 query DSL，支持关系、继承、optional | 高 |
| **MyTryGetFramework** | "按 Aspect / Tag 存在的组合过滤器" | 低-中等（仅 presence） |

**分析。** 当前设计只提到了 "presence" 匹配（有/无某 Aspect 或 Tag），但没有明确三个常见的匹配谓词：

1. **All-of（全部包含）**：Entity 必须同时拥有指定的所有 Aspect/Tag。
2. **Any-of（任一包含）**：Entity 拥有指定的任一 Aspect/Tag 即可匹配。
3. **None-of（排除）**：Entity 不得拥有指定的 Aspect/Tag。

Issue-10 的 Acceptance Criteria 只提到 "匹配拥有指定 Aspect 的 Entity" 和 "匹配同时拥有多个 Aspect 的 Entity"，这对应 All-of 语义。但 **None-of（排除）** 在实际游戏开发中极为常见（例如 "匹配所有有 Health 但没有 Dead Tag 的 Entity"），如果 V0.1 不包含排除语义，后续加入会影响 Query 接口的稳定性。

**建议。** 在 V0.1 中至少支持 All-of 和 None-of 两种组合谓词。Any-of 可以推迟。在 Issue-10 或新建 ADR 中明确这一点。

### 2.3 Entity Ownership 级联生命周期的边界情况

**当前设计。** CONTEXT.md 规定 "子 Entity 默认跟随父 Entity 的生命周期"，"子 Entity 可以被显式 Detach 并继续独立存在"。Issue-08 覆盖级联生命周期。

**业界对比。**

- **Unity DOTS (LinkedEntityGroup)**：父 Entity 销毁时，LinkedEntityGroup 中所有 Entity 一起销毁。但这是显式 opt-in，不是默认行为。
- **Flecs**：支持 `ChildOf` 关系，父 Entity 销毁时自动级联删除子 Entity。这是默认行为。
- **Entitas**：没有内置父子关系概念，需要用户用 Component 自行管理。

**需要明确的边界情况：**

1. **级联深度**：多层嵌套时（A → B → C），销毁 A 是否级联销毁 B 和 C？如果是，执行顺序是叶子优先（C → B → A）还是根优先（A → B → C）？
2. **销毁过程中的 Detach**：如果在 A 的销毁回调中，某个 System 将 B 从 A 的子树中 Detach，B 是否幸存？
3. **循环防御**：虽然 "最多一个 parent" 阻止了 Ownership 图循环，但 Reference（弱引用）可能创建逻辑上的循环依赖。需要明确 Reference 不参与级联。
4. **World 关闭与级联的交互**：Issue-12 提到 "Exit Phase 后生命周期结果明确"，但 World 关闭时是调用每个顶层 Entity 的销毁（触发级联），还是直接批量清理？

**建议。** 在 Issue-08 中增加以下 Acceptance Criteria：
- 多层级联销毁的执行顺序。
- 级联过程中的 Detach 行为是否生效。
- World 关闭时的清理策略。

### 2.4 Event 的三层设计 — 最复杂的子系统

**当前设计。** CONTEXT.md 定义 Event 为 "分层传播的通知概念"，分为三层：Entity 内通知、World 内事件、跨 System 事件。Issue-15/16/17 分别实现这三层。

**业界对比。**

| 框架 | Event 机制 |
|---|---|
| **Unity DOTS** | 无内置 Event 系统；推荐使用 Component 数据的变化作为隐式事件，或使用 EntityCommandBuffer + Tag/一次性 Component |
| **Arch** | 无内置 Event 系统；用户自行实现 |
| **Entitas** | Reactive System — 监听 Component 的 Added/Removed/Replaced 事件 |
| **BigCat** | WorkerEvent 按类型分发 + RPC |
| **TEngine** | 生成的 GameEvent + Procedure FSM |
| **hsenl** | 程序集扫描事件系统 |

**分析。** 三层 Event 系统是 V0.1 中最复杂的子系统，也是最容易过度设计的区域：

1. **Entity-level Event（Issue-15）**：最直接。Entity 发布事件，其 Aspect 可以观察。但需要明确：谁可以订阅？只有 Aspect 可以订阅吗？System 可以订阅特定 Entity 的事件吗？
2. **World-level Event（Issue-16）**：等价于一个 scoped event bus。需要明确：订阅者是 System 还是任意对象？
3. **Cross-System Event（Issue-17）**：这是最模糊的。"跨 System 事件" 的触发者和消费者都是 System，那它与 World-level Event 的区别是什么？如果区别仅是语义上的，是否真的需要单独的机制？

**⚠️ 关键风险：三层 Event 在 V0.1 可能过早。**

考虑到 PRD 明确说 "V0.1 中 Event 不成为庞大的消息平台"，但同时又规划了三个独立的 Event issue（15/16/17），这之间存在张力。Entitas 和 Unity DOTS 的成功经验表明，一个最小的 ECS 可以只用 **Component 变化监听**（Entitas 的 Reactive System）或 **一次性 Tag/Component**（Unity DOTS 的 event component pattern）来覆盖大部分事件需求。

**建议方案。**

- **V0.1 必须有**：Entity-level Event（Issue-15），因为 Aspect 之间的局部通信是组合模型的基础。
- **V0.1 应该有**：World-level Event（Issue-16），因为 System 之间需要松耦合的通信手段。
- **V0.1 可以推迟**：Cross-System Event（Issue-17），除非能给出与 World-level Event 在 API 或行为上的具体区别。否则 World-level Event 已经足够覆盖 System 间通信。

### 2.5 System 的注册与执行模型

**当前设计。** PRD 规定 System 拥有 "跨 Entity 调度规则"，运行在特定 Phase 中，通过 Query 获取目标 Entity。SystemGroup 负责组织执行顺序。

**需要明确的问题：**

1. **System 如何注册到 World？** 目前没有 issue 或 ADR 描述 System 注册机制。是在 World 创建时声明式注册，还是运行时动态添加/移除？
   - Unity DOTS：`[UpdateInGroup(typeof(SimulationSystemGroup))]` 声明式注册
   - Entitas：`Systems.Add(new MySystem())` 手动注册
   - Arch：无内置 System 概念，用户自行调度

2. **System 的状态**：PRD User Story 20 说 "System 不拥有单 Entity 的能力状态"，但 System 是否可以拥有**跨 Entity 的状态**？例如计时器、计数器、缓存。如果可以，如何确保 System 状态不变成隐式的全局状态？

3. **System 的执行签名**：System 收到 Query 匹配的 Entity 后，是逐个处理（`foreach entity`），还是批量处理（收到整个列表）？批量处理更适合某些全局逻辑（如碰撞检测），逐个处理更符合 ECS 的常见模式。

**建议。** 新建一个 ADR 或在 Issue-13 中增加以下明确定义：
- System 注册机制（声明式 vs 手动式）。
- System 是否允许拥有跨 Entity 状态。
- System 的执行回调签名。

### 2.6 Phase 模型的扩展性预留

**当前设计。** ADR-0004 固定为 Enter / Update / Exit 三阶段。PRD 明确排除自定义 Phase graph。

**业界对比。**

- **Unity DOTS**：三组 SystemGroup（InitializationSystemGroup / SimulationSystemGroup / PresentationSystemGroup）分别对应初始化、模拟、呈现。[参考](https://docs.unity3d.com/Packages/com.unity.entities@0.1/manual/system_update_order.html)
- **Entitas**：Initialize / Execute / Cleanup / TearDown 四种 System 类型。

**分析。** Enter / Update / Exit 与 Unity DOTS 的三组概念类似（Initialization / Simulation / Presentation），但缺少一个常见的阶段：**LateUpdate / Cleanup**。

在游戏开发中，Update 后的清理阶段非常常见：
- 销毁标记为 dead 的 Entity
- 清理一次性 Event Component
- 重置帧级缓存

如果没有 Cleanup 阶段，这些操作必须放在 Update Phase 的最后一个 SystemGroup 中，这会使 SystemGroup 的 "纯调度组" 语义被污染（最后一个 group 变成了隐式的 cleanup 阶段）。

**建议。** 两个选项：
1. 保持三阶段，但在文档中明确 "Update Phase 中可以使用 SystemGroup 来模拟 cleanup 时序"。
2. 考虑增加一个 `Cleanup` 子阶段（作为 Update 内的隐式末尾），不打破三阶段模型的外部 API。

---

## 三、参考框架深度对比分析

### 3.1 BigCat — 最值得借鉴的架构方向

**架构核心：** Node / Worker / EventLoop / Module 多层模型，核心是纯 C#。

**对 MyTryGetFramework 的深度启示：**

1. **分层启动/关闭的确定性纪律。** BigCat 的 `Node.StartModules()` → `ExportServices` → `StartWorkers` 和逆序关闭模式，是 MyTryGetFramework Phase（Enter / Exit）和 System 注册/销毁可以借鉴的模板。关键不是复制 BigCat 的 API，而是借鉴其 **关闭时严格逆序** 的纪律。

2. **DefaultMainModule 的帧节奏控制。** BigCat 用 `FrameInterval` + `CheckMainLoop` + `BeforeMainLoop` + `AfterMainLoop` 控制主循环，这比 MyTryGetFramework 当前的 Phase 模型更精细。MyTryGetFramework 的 Update Phase 可以借鉴这种思路：在 Update 内部定义 BeforeUpdate / Update / AfterUpdate 的 SystemGroup 惯例。

3. **WorkerEvent 按类型分发。** BigCat 的 `DefaultMainModule` 按类型把 WorkerEvent 分发给已注册处理器。这为 MyTryGetFramework 的 World-level Event 提供了一个简洁的实现参考：Event 按类型注册处理器，World 在 Phase 执行期间派发。

**应该避免：** BigCat 的 RPC/Session、Disruptor 模式、多 Worker 并发模型对 V0.1 过重。

### 3.2 TEngine — 最值得借鉴的用户体验方向

**架构核心：** MonoBehaviour 入口 + ModuleSystem 服务定位器 + Procedure FSM 启动链。

**对 MyTryGetFramework 的深度启示：**

1. **模块优先级排序和确定性 Update。** TEngine 的 `ModuleSystem` 按 Priority 排序更新列表，这与 MyTryGetFramework 的 SystemGroup 确定性调度目标一致。差异在于 TEngine 的优先级是整数，而 MyTryGetFramework 使用 SystemGroup 作为分组。SystemGroup 的好处是：优先级是结构化的（Group 内部是列表，Group 之间是顺序），而不是扁平的整数排序。

2. **反向关闭顺序。** TEngine 在 `ModuleSystem.Shutdown` 中按注册的逆序关闭模块。MyTryGetFramework 的 Exit Phase 应该保证类似的行为：System 在 Exit 中以注册的逆序执行。

3. **asmdef 的实际分离。** TEngine 的 `TEngine.Runtime.asmdef` 和 `TEngine.Editor.asmdef` 虽然运行时仍然依赖 Unity（`noEngineReferences: false`），但至少实现了编辑器/运行时的程序集分离。MyTryGetFramework 的 candidates.md 第 3 条建议的 asmdef 布局（Core `noEngineReferences: true` + Unity Adapter `noEngineReferences: false`）比 TEngine 更进一步，**这是正确的方向**。

**应该避免：** 隐式类型命名创建（`IThingModule` → `ThingModule`）、全局静态 `GameModule` 包装、HybridCLR/热更/Luban 复杂度。

### 3.3 hsenl — 最值得借鉴的 Unity 接入形态

**架构核心：** FrameworkProxy : MonoBehaviour 驱动纯 C# Framework 核心。

**对 MyTryGetFramework 的深度启示：**

1. **FrameworkProxy 作为唯一 Unity 入口。** hsenl 的 `FrameworkProxy` 模式（Awake 初始化 Framework → Update/LateUpdate 转发 → OnApplicationQuit 销毁）是 MyTryGetFramework Adapter 概念的最直接参考。candidates.md 第 1 条已经采纳了这个方向。

2. **SingletonManager 的显式注册/注销。** hsenl 的 `SingletonManager.Register` / `Unregister` / `UnregisterAll` 模式比 TEngine 的反射命名更显式。但 MyTryGetFramework 应该避免使用全局 Singleton——System 注册应该通过 World，而不是全局管理器。

3. **Network asmdef 的无引擎隔离。** hsenl 的 `HsenlFramework.Network.asmdef` 设置 `noEngineReferences: true`，证明了在同一个 Unity 项目中可以拥有完全不依赖引擎的程序集。这为 MyTryGetFramework 的 Core 程序集提供了先例。

**应该避免：** 运行时文件中的 `#if UNITY_EDITOR` 编辑器代码、大量 Manager/Proxy 表面积、程序集扫描。

---

## 四、与业界 ECS 框架的概念映射

### 4.1 概念映射表

| MyTryGetFramework | Unity DOTS | Entitas | Arch ECS | Flecs |
|---|---|---|---|---|
| **World** | World | Context | World | World |
| **Entity** | Entity | Entity | Entity | Entity |
| **Aspect** (state+behavior) | IComponentData (pure data) | Component (pure data) | Component (data) | Component (data) |
| **System** | SystemBase / ISystem | IExecuteSystem 等 | 无内置（用户自行） | System |
| **Phase** | SystemGroup 三组 | System 类型 (Init/Exec/Cleanup/Teardown) | 无内置 | Phase / Pipeline |
| **SystemGroup** | ComponentSystemGroup | Feature (System 组) | 无内置 | Phase 嵌套 |
| **Query** | EntityQuery | Matcher + Group | QueryDescription | Query / Filter |
| **Event** | 无内置 (Component pattern) | Reactive System | 无内置 | Observer |
| **Tag** | Tag Component (zero-size) | Flag Component | Tag struct | Tag pair |
| **Adapter** | 无直接对应（Hybrid Renderer） | 无直接对应 | 无直接对应 | Module |
| **Handle** | Entity (version+index) | 无直接对应（Entity 有 creationIndex） | EntityReference | Entity (generation) |
| **Ownership** | LinkedEntityGroup | 用户自行管理 | 用户自行管理 | ChildOf 关系 |

### 4.2 关键差异点

1. **Aspect 的 "behavior"。** MyTryGetFramework 是唯一在 V0.1 就明确允许 Component/Aspect 携带行为的设计。所有主流 ECS 框架（Unity DOTS、Arch、Entitas）都将 Component 限制为纯数据。Flecs 也将行为放在 System 和 Observer 中。最接近的设计是 hsenl 和 BigCat 的 EC 模式。

2. **Event 作为一等概念。** Unity DOTS 和 Arch 都没有内置 Event 系统。Entitas 通过 Reactive System（监听 Component 变化）实现类似功能。Flecs 通过 Observer 实现。MyTryGetFramework 在 V0.1 就定义三层 Event，比大多数框架更激进。

3. **Adapter 作为架构概念。** 这是 MyTryGetFramework 独有的一等概念。业界框架通常通过 asmdef / 程序集隔离来实现类似效果，但不将其命名为核心领域概念。这是一个好的设计选择，因为它让 Unity 耦合控制变得可见和可测试。

4. **Ownership 作为内置机制。** 与 Flecs 的 `ChildOf` 关系类似，但比 Unity DOTS 的 `LinkedEntityGroup` 和 Entitas 的用户自管理更集成。这是 MyTryGetFramework 的差异化优势。

---

## 五、Issue 依赖图分析

```
Issue-01 (World + Entity)
├── Issue-02 (EntityId uniqueness)
├── Issue-04 (Aspect attach/detach) ─── Issue-15 (Entity Event)
│   └── Issue-05 (Aspect uniqueness)
│       └── Issue-10 (Query by Aspect) ─── Issue-13 (System over Query)
│           └── Issue-11 (Query by Tag)     ├── Issue-14 (SystemGroup)
│                                           └── Issue-18 (Fake Adapter)
├── Issue-06 (Tag add/remove)
├── Issue-03 (Entity destroy)
│   ├── Issue-07 (Entity Ownership) ─── Issue-08 (Cascade lifecycle)
│   ├── Issue-09 (Handle resolve)
│   └── Issue-12 (Phase Enter/Update/Exit)
│       ├── Issue-13 (System over Query)
│       └── Issue-18 (Fake Adapter)
Issue-15 (Entity Event) → Issue-16 (World Event) → Issue-17 (Cross-System Event)
```

### 关键路径分析

**最长路径：** Issue-01 → 04 → 05 → 10 → 13 → 18（6 步）
**这条路径的含义：** 从 World/Entity 创建到 Adapter 驱动的完整系统执行，需要 6 个 issue 的串行完成。

**并行机会：**
- Issue-02（EntityId）、Issue-06（Tag）、Issue-03（Entity destroy）可以与 Issue-04（Aspect）**并行**开发。
- Issue-07（Ownership）和 Issue-09（Handle）可以与 Issue-05（Aspect uniqueness）**并行**开发。
- Issue-15（Entity Event）可以在 Issue-04 完成后立即开始，与 Query/System 路径**并行**。

### 潜在的依赖缺失

1. **Issue-06（Tag add/remove）没有被 Issue-11（Query by Tag）列为 Blocked-by。** 但 Issue-11 需要 Tag 功能才能测试。建议在 Issue-11 中添加对 Issue-06 的依赖。

2. **Issue-14（SystemGroup）没有 Issue-12（Phase）作为 Blocked-by。** SystemGroup 是在 Phase 内组织 System 执行顺序的概念，应该依赖 Phase 的实现。

---

## 六、风险矩阵

| 风险 | 影响 | 可能性 | 缓解方案 |
|---|---|---|---|
| Aspect behavior 边界不清，导致 System 与 Aspect 职责混乱 | 高 | 中 | 新建 ADR 定义 Aspect behavior scope |
| 三层 Event 过度设计，V0.1 实现周期过长 | 中 | 高 | V0.1 只实现 Entity + World Event；Cross-System 推迟 |
| Query 缺少 None-of（排除）语义，后续加入破坏接口 | 中 | 高 | V0.1 Query API 设计时预留 None-of |
| Entity Ownership 级联销毁边界情况未定义 | 中 | 中 | 在 Issue-08 中补充 acceptance criteria |
| System 注册机制未定义，导致实现时临时决策 | 中 | 高 | 在 Issue-13 前新建注册机制 ADR |
| 没有 Cleanup 阶段，Update 末尾 SystemGroup 承担隐式职责 | 低 | 中 | 文档明确或增加 Cleanup 子阶段 |

---

## 七、建议的行动项

### 优先级 P0（实现前必须明确）

1. **新建 ADR-0007：Aspect Behavior Scope。** 定义 Aspect 局部行为的精确边界——允许什么、禁止什么、边界场景如何处理。
2. **新建 ADR-0008：System Registration and Execution Contract。** 定义 System 如何注册到 World、执行签名、是否允许跨 Entity 状态。
3. **修订 Issue-10：Query 增加 None-of 语义。** 在 Acceptance Criteria 中增加排除匹配。

### 优先级 P1（实现中应尽快明确）

4. **修订 Issue-08：补充级联销毁的边界情况。** 多层级联顺序、销毁过程中的 Detach 行为、World 关闭策略。
5. **修订 Issue-11：添加对 Issue-06 的依赖。** Tag Query 需要 Tag 功能。
6. **修订 Issue-14：添加对 Issue-12 的依赖。** SystemGroup 需要 Phase。
7. **评估 Issue-17（Cross-System Event）是否可推迟到 V0.2。** 如果 World-level Event 足以覆盖 System 间通信，Cross-System Event 在 V0.1 可能过早。

### 优先级 P2（V0.1 完成后）

8. **评估 Phase 模型是否需要 Cleanup 子阶段。**
9. **评估 Query 是否需要 Any-of 语义。**
10. **参考 Entitas 的 Reactive System 模式，评估是否可以用 Component 变化监听替代部分 Event 场景。**

---

## 八、总结

MyTryGetFramework V0.1 的架构设计文档在以下方面表现出色：
- **领域语言的一致性和严格性**在所审阅的框架中最优。
- **Scope 控制**极其克制，优于三个参考框架中任何一个的 V1。
- **Adapter 边界**作为一等概念，确保了 Unity 耦合的可见性和可测试性。
- **Issue 依赖关系**形成了清晰的有向无环图，实现顺序合理。

主要改进机会集中在：
- **Aspect 行为边界**需要更精确的定义。
- **Query 排除语义**和 **System 注册机制**需要在实现前明确。
- **三层 Event 系统**的范围可以在 V0.1 中适当收窄。
- **Entity Ownership 级联**的边界情况需要补充。

与参考框架的对比表明，MyTryGetFramework 选择了一条**混合 EC/ECS 路径**（Aspect 含行为），更接近 BigCat/hsenl 而非纯 ECS（Unity DOTS/Arch/Entitas）。这不是错误，但需要更显式地管理 Aspect 与 System 的职责边界，以避免随着框架成长而出现的职责混乱。

---

*参考来源：[ECS FAQ (SanderMertens)](https://github.com/SanderMertens/ecs-faq)、[Entitas](https://github.com/sschmid/Entitas)、[Arch ECS](https://github.com/genaray/Arch)、[Unity DOTS SystemGroup ordering](https://docs.unity3d.com/Packages/com.unity.entities@0.1/manual/system_update_order.html)、[ECS 概念形式化 (Maxim Zaks)](https://mzaks.medium.com/formalisation-of-concepts-behind-ecs-and-entitas-8efe535d9516)、[ECS back and forth (skypjack)](https://skypjack.github.io/2019-06-25-ecs-baf-part-4/)、[Reddit: ECS parent-child relations](https://www.reddit.com/r/EntityComponentSystem/comments/whzohm/how_to_handle_parentchild_relations_in_a_cache/)、[Unity Forum: ECS Event-driven pattern](https://forum.unity.com/threads/how-to-implement-an-event-driven-pattern-on-ecs.714983/)*
