Status: ready-for-agent

# MyTryGetFramework V0.1 核心运行时 PRD (产品需求文档)

## Problem Statement (问题陈述)

MyTryGetFramework 需要在核心运行时骨架开始实现之前，有一份极其明确的 V0.1 产品定义。项目已经拥有了领域语言 (Domain Language) 和 ADR (架构决策记录)，它们承诺采用以组合 (composition) 为中心的模型。但是，如果没有 PRD，首批代码切片很容易偏离方向，混入 Unity 特定的 Scene、Component、Manager、UI、资源、Procedure/FSM、网络、热重载、配置或编辑器工具等概念。

用户需要一份 PRD，将 V0.1 严格限制在验证框架组合模型所需的最小运行时概念上：`World` (世界)、`Entity` (实体)、`Aspect` (切面)、`System` (系统)、`Phase` (阶段)、`SystemGroup` (系统组)、`Query` (查询)、`Event` (事件)、`Tag` (标签) 和 `Adapter` (适配器)。

## Solution (解决方案)

将 MyTryGetFramework V0.1 定义为一个微小、可测试的核心运行时骨架，围绕 `World` / `Entity` / `Aspect` 组合以及基于 `System` 驱动的调度来构建。运行时应当使 `Entity` 身份、`Aspect` 所有权、`Query` 匹配、`Phase` 执行、`SystemGroup` 排序、分层的 `Event` 传递、仅表示存在的 `Tag` 检查以及 `Adapter` 边界都变得极其明确，以便未来的 Unity 集成可以附着在这个模型上，而不会**成为**这个模型本身。

V0.1 产品应优先考虑纯粹的运行时模型，其行为可以在不依赖 UI、资源系统、Procedure/FSM、网络、热重载、配置表或编辑器工具的情况下进行测试。与 Unity 交互的工作仅由 `Adapter` 概念表示，并且必须保持在核心运行时语义之外。

## User Stories (用户故事)

1. 作为框架用户，我想创建一个 `World`，以便我可以托管一个独立的运行时实例。
2. 作为框架用户，我希望 `World` 能包含 `Entity`，以便运行时对象具有清晰的所有权边界。
3. 作为框架用户，我希望一个 `Entity` 仅归属于一个确定的 `World`，以便 `Entity` 的身份和生命周期是明确无误的。
4. 作为框架用户，我希望每个 `Entity` 在其所在的 `World` 中具有唯一的 `EntityId`，以便 `System` 和查询能够在那个 `World` 中安全地引用 `Entity`。
5. 作为框架用户，我希望 `EntityId` 的唯一性限定在单个 `World` 内，以便可以同时运行多个 `World` 而无需全局标识符。
6. 作为框架用户，我想通过 `World` 来创建和销毁 `Entity`，以便生命周期规则保持集中。
7. 作为框架用户，我希望一个 `Entity` 能够拥有 (own) `Aspect`，以便能力是通过组合而不是继承来实现的。
8. 作为框架用户，我希望一个 `Aspect` 能够封装其自身的状态和局部行为，以便每个 `Entity` 的能力状态不会泄露到 `System` 中。
9. 作为框架用户，我想让一个 `Entity` 挂载 (attach) 一个 `Aspect`，以便我可以动态添加能力。
10. 作为框架用户，我想让一个 `Entity` 卸载 (detach) 一个 `Aspect`，以便我可以在不销毁 `Entity` 的情况下移除能力。
11. 作为框架用户，当运行时模型要求唯一性时，我希望 `Entity` 能够阻止重复拥有同一种类的 `Aspect`，以便 `Query` 的结果是可预测的。
12. 作为框架用户，我希望 `Entity` 能够暴露它是否拥有特定的 `Aspect`，以便 `System` 可以基于组合做出决策。
13. 作为框架用户，我希望 `Entity` 能通过一个小巧的接口提供对其拥有的 `Aspect` 的访问，以便调用者无法直接操作其内部存储。
14. 作为 `Aspect` 作者，我希望局部的 `Aspect` 行为留在 `Aspect` 内部，以便 `System` 不会变成一堆单 `Entity` 状态的集合。
15. 作为 `System` 作者，我想定义基于 `Aspect` 组合的 `Query`，以便 `System` 能够筛选出匹配的 `Entity`。
16. 作为 `System` 作者，我希望 `Query` 匹配是基于 `Aspect` 的存在与否，以便运行时保持与组合模型对齐。
17. 作为 `System` 作者，我希望 `Query` 语义避免变成一个脚本层，以便 V0.1 保持为一个核心运行时骨架。
18. 作为 `System` 作者，我想对 `Query` 匹配到的 `Entity` 运行逻辑，以便将跨 `Entity` 的调度规则表达在 `System` 中。
19. 作为框架用户，我希望 `System` 拥有跨 `Entity` 的调度规则，以便 `Aspect` 状态和 `System` 的编排能够保持分离。
20. 作为框架用户，我希望 `System` 避免拥有单 `Entity` 的能力状态，以便 `Entity` 的组合仍然是运行时状态的唯一来源。
21. 作为框架用户，我希望 `World` 负责协调各个 `System`，以便执行拥有单一的运行时边界。
22. 作为框架用户，我希望 `World` 的协调不要取代 `Entity` 的组合，以便组合模型保持首要地位。
23. 作为框架用户，我想要一个极简的 `Phase` 模型，以便生命周期的执行清晰明了，无需自定义复杂的阶段图。
24. 作为框架用户，我希望 `Phase` 模型包含 Enter (进入)、Update (更新) 和 Exit (退出)，以便在 V0.1 中生命周期的排序容易理解。
25. 作为 `System` 作者，我希望 `System` 运行在某个 `Phase` 中，以便它们的执行时机是明确的。
26. 作为框架用户，我希望 `World` 进入其 Enter `Phase`，以便 setup (安装/初始化) 的 `System` 可以在更新前运行。
27. 作为框架用户，我希望 `World` 运行其 Update `Phase`，以便运行时的 `System` 可以重复执行。
28. 作为框架用户，我希望 `World` 运行其 Exit `Phase`，以便 teardown (卸载/清理) 的 `System` 可以被确定性地执行。
29. 作为框架用户，我希望 `Phase` 行为在 V0.1 中保持固定，以便第一个版本不会过度迎合自定义的生命周期分类。
30. 作为框架用户，我希望 `SystemGroup` 能够组织 `System` 的执行顺序，以便调度是明确且可测试的。
31. 作为框架用户，我希望 `SystemGroup` 保持为纯粹的调度组，以便它不会变成领域边界或模块系统。
32. 作为 `System` 作者，我希望同一个 `SystemGroup` 内的 `System` 以确定性的顺序运行，以便行为可以被推理和测试。
33. 作为框架用户，我希望多个 `SystemGroup` 来表达职责和顺序，以便 `World` 的执行可以扩展，而不必引入更庞大的模块架构。
34. 作为框架用户，我希望 `SystemGroup` 的语义独立于 UI、资源、网络或编辑器工具，以便它仅属于核心运行时。
35. 作为框架用户，我希望 `Event` 作为分层通知概念存在，以便运行时的通知能够被精确地讨论。
36. 作为框架用户，我想要 Entity 级别的 `Event`，以便 `Entity` 及其 `Aspect` 能够就本地变更进行通信。
37. 作为框架用户，我想要 World 级别的 `Event`，以便整个运行时范围内的通知能够留在 `World` 边界内。
38. 作为 `System` 作者，我想要跨 System 的 `Event`，以便 `System` 可以在没有直接耦合的情况下进行协调。
39. 作为框架用户，我希望 `Event` 的语义不意味着一个完整的消息平台，以便 V0.1 保持精简。
40. 作为框架用户，我希望在核心语言中避免将 `Event` 称为 message (消息) 或 signal (信号)，以便术语与领域词汇表保持一致。
41. 作为框架用户，我希望 `Tag` 仅代表存在与否，以便轻量级的标记不会变成隐藏的状态容器。
42. 作为 `System` 作者，我希望 `Query` 匹配在适当的时候包含 `Tag` 的存在与否，以便可以选择标记式的组合，而不用创建空的带状态 `Aspect`。
43. 作为框架用户，我希望 `Tag` 明显区别于 `Aspect` 状态，以便任何带状态的能力依然是 `Aspect`。
44. 作为框架用户，我希望 `Tag` 可以被添加到一个 `Entity` 或从中移除，以便仅表示存在的分类能在运行时发生变化。
45. 作为框架用户，我希望 `Query` 和 `Tag` 的行为能够在不依赖 Unity 的情况下进行测试，以便核心运行时保持可移植。
46. 作为框架用户，我需要一个 `Adapter` 概念，以便外部运行时和设施可以连接到 `World`、`Entity` 或 `Aspect` 概念上。
47. 作为 Unity 集成者，我希望 `Adapter` 边界将 Unity Scene 语义挡在核心 `World` 模型之外，以便 Unity 场景加载不会定义运行时身份。
48. 作为 Unity 集成者，我希望 `Adapter` 边界将 Unity Component 语义挡在核心 `Aspect` 模型之外，以便框架的组合不会与 Unity 组件发生冲突。
49. 作为框架用户，我希望核心的 `Aspect` 和 `System` 不依赖于 Unity 的生命周期 API，以便核心模型可以被独立测试和推理。
50. 作为框架用户，我希望 `Adapter` 的职责是明确的，以便未来的 Unity 集成不会泄漏到核心的 `Aspect` 或 `System` 中。
51. 作为框架用户，我希望 `Entity` Ownership (所有权) 支持父子树结构，以便子 `Entity` 的生命周期默认可以跟随父级的生命周期。
52. 作为框架用户，我希望每个 `Entity` 最多只能有一个父 `Entity`，以便 Ownership 保持为一棵树而不是一张图。
53. 作为框架用户，我希望子 `Entity` 默认跟随父级的生命周期，以便拥有从属关系的运行时结构能被可预测地清理。
54. 作为框架用户，我希望一个 `Entity` 可以从其父级 Detach (分离)，以便一个子 `Entity` 在被显式分离时可以继续独立存在。
55. 作为框架用户，我希望 `Entity` 的 Reference (引用) 与 Ownership (所有权) 区分开，以便弱关系不会影响生命周期。
56. 作为框架用户，我希望 `Handle` 解析是明确的，以便弱 `Entity` 引用在目标不再存在时能够安全地失败。
57. 作为框架用户，我希望 Reference (引用) 不会改变父子 Ownership，以便关系语义保持清晰。
58. 作为 `System` 作者，我希望 `Query` 结果尊重 `Entity` 生命周期，以便被销毁的 `Entity` 不会被调度执行。
59. 作为 `System` 作者，我希望 `System` 仅对同一个 `World` 中的 `Entity` 进行操作，以便保持运行时边界。
60. 作为框架用户，我希望 `World` 关闭时能明确说明对 `Entity` 和 `System` 生命周期的影响，以便清理行为是具有确定性的。
61. 作为测试作者，我希望能够在没有 Unity 对象的情况下实例化核心运行时，以便 V0.1 的行为可以被快速的测试所覆盖。
62. 作为测试作者，我希望测试通过产品调用者使用的相同公共运行时接口来验证外部行为，而不是验证内部集合、私有排序结构或实现细节。
63. 作为维护者，我希望 V0.1 的接口小巧且稳定，以便日后的 UI、资源、Procedure/FSM、网络、热重载、配置和编辑器工具都可以通过单独的决策加入进来。
64. 作为维护者，我希望 PRD 使用项目的领域语言，以便实现 issue (工单) 不会偏向那些被要求避免的术语。
65. 作为实现 V0.1 的 Agent，我希望有明确的“不在范围内 (out-of-scope)”的边界，以便我不会意外构建出框架外延的功能。
66. 作为实现 V0.1 的 Agent，我希望有一份 ready-for-agent (已准备好交由 AI 处理) 的 PRD，以便可以直接从中切分出实现 issue，而不需要再次进行 triage (分诊) 筛选。

## Implementation Decisions (实现决策)

- V0.1 的构建仅围绕领域概念：`World`, `Entity`, `Aspect`, `System`, `Phase`, `SystemGroup`, `Query`, `Event`, `Tag` 和 `Adapter`。
- 将 `World` 视为独立的运行时边界，它拥有 `Entity` 生命周期、`System` 执行、`Phase` 推进和 `SystemGroup` 调度的所有权。
- 将 `Entity` 视为身份 (identity)、生命周期、Ownership (所有权)、Reference (引用)、`Tag` 和所拥有的 `Aspect` 的组合宿主。
- 将 `Aspect` 视为 `Entity` 拥有的状态加上局部行为，而不是一个纯数据的组件 (anemic data-only component)，也**绝不是** Unity Component。
- 将 `System` 视为拥有作用于 `Query` 筛选出的 `Entity` 上的跨 `Entity` 调度规则的所有者。
- 在 V0.1 中，将 `Phase` 模型固定为 Enter、Update 和 Exit。
- 保持 `SystemGroup` 作为仅用于排序和职责划分的纯粹调度组；它决不能变成领域边界、模块系统或服务注册表。
- 将 `Query` 定义为基于 `Entity` `Aspect` 和 `Tag` 存在的组合过滤器；它决不能变成类似于 SQL 的、脚本化的或通用的谓词语言。
- 将 `Event` 定义为分层的通知概念，具有 Entity 级、World 级和跨 System 级；在 V0.1 中，它决不能变成一个庞大的消息平台。
- 将 `Tag` 定义为仅表示存在的标记。任何需要状态的标记必须表示为 `Aspect`。
- 将 `Adapter` 定义为连接 Unity 或其他外部运行时的边界。Adapter 代码可以连接到 `World`、`Entity` 或 `Aspect` 概念，但核心运行时语义**不应该**依赖于 Unity Scene、MonoBehaviour、Unity Component、Editor API、资源、UI 或网络。
- 保持 ADR 中对 `Aspect`、`System` 和 `Adapter` 职责的划分：`Aspect` 拥有局部状态和行为；`System` 拥有跨 `Entity` 规则；`Adapter` 拥有外部运行时集成。
- 包含仅限定在单一 `World` 内的 `EntityId`，而不是全局标识符。
- 为不参与 Ownership 的 `Entity` 关系包含 `Handle` 或弱 `Reference` 语义。
- 包含父子 Ownership (所有权)，其中一个 `Entity` 最多可以拥有一个父级，子级生命周期默认跟随父级生命周期，并且显式的 detach (分离) 允许子级继续存在。
- 倾向于具有小巧接口的深模块 (deep modules)：`World` 生命周期接口、`Entity` 组合接口、`Query` 匹配器、`System` 调度器、`Event` 层和 `Adapter` 边界都应该隐藏其内部存储和排序细节。
- 不要在本 PRD 中包含实现细节、源文件路径或代码片段，因为在实现规划期间，存储库结构可能会发生变化。

## Testing Decisions (测试决策)

- 测试应该通过生产环境调用者使用的相同公共运行时接口来验证外部行为，而不是通过内部集合、私有排序结构或实现细节。
- `World` 生命周期测试应涵盖 `Entity` 创建/销毁、`World` 边界强制执行、`Phase` 推进以及关闭行为。
- `Entity` 组合测试应涵盖 `Aspect` 挂载/卸载、`Tag` 添加/移除、单个 `World` 内的 `EntityId` 唯一性、Ownership 挂载/卸载，以及弱 `Reference` 或 `Handle` 解析。
- `Query` 测试应涵盖通过 `Aspect` 存在与否的匹配、`Tag` 存在与否的匹配、所需组合的并集，以及排除已销毁或不在同一 `World` 中的 `Entity`。
- `System` 调度测试应涵盖通过 `Phase` 和 `SystemGroup` 顺序进行确定性的执行。
- `Event` 测试应涵盖 Entity 级、World 级和跨 System 级通知的外部可见行为，而不应断言其内部派发机制。
- `Adapter` 边界测试应使用伪造的 (fake) 或最小化的 adapter 来验证外部运行时集成可以驱动或观察核心运行时概念，而不会使核心依赖于 Unity API。
- 好的测试应该使 ADR 具有可执行性：`Aspect` / `System` / `Adapter` 职责分离、最小化的三阶段 `Phase`、`Query` / `Event` / `Tag` 词汇表以及作为纯粹调度的 `SystemGroup` 都应受到行为测试的保护。
- 此仓库目前还没有建立先前的测试套件，因此 V0.1 测试应该成为运行时模型的首批面向消费者的测试示例。

## Out of Scope (不在范围内)

- UI 系统、窗口宿主、画布 (canvases)、层 (layers)、UGUI 集成或 UI 资源加载。
- 资源系统、资产加载外观 (asset loading facades)、Addressables、Resources、YooAsset、资产清单 (asset manifests) 或资源释放策略。
- Procedure (流程) / FSM (有限状态机) 启动流或基于玩法的状态机。
- 网络、RPC、套接字、通道、会话、服务器/客户端边界或多人游戏考量。
- 热重载、热更新、HybridCLR、动态程序集加载或运行时代码替换。
- 配置表、生成的配置代码、类似 Luban 的管道、结构导入 (schema import) 或数据表工具。
- 编辑器工具、检查器 (inspectors)、代码生成器、验证窗口、菜单项或 UnityEditor 集成。
- 完整的模块系统、服务定位器 (service locators)、全局管理器 (global managers)、依赖注入容器或基于反射的模块发现。
- 自定义 `Phase` 图形、玩法特定的阶段分类，或超出 Enter、Update 和 Exit 之外的用户定义生命周期管道。
- 完整的脚本语言、类似 SQL 的查询层或广泛的消息/事件总线平台。
- 将 Unity Scene 语义作为 `World` 的替代品，将 Unity Component 语义作为 `Aspect` 的替代品，或在核心域中使用 Manager / Module / Processor 术语。

## Further Notes (补充说明)

本 PRD 有意地将 V0.1 缩小为能够验证组合模型的最小、一致的运行时骨架。它遵循当前的领域词汇表和 ADR 作为事实来源。`.scratch/framework-design/candidates.md` 仍保留为有用的历史上下文，但不覆盖 CONTEXT.md 或 docs/adr 中的决策。

未来的工作应在此 PRD 之后拆分为多个实现 issue (工单)，并且每个 issue 都要保留相同的领域语言和超出范围 (out-of-scope) 的边界。