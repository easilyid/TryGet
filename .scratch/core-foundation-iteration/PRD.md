Status: ready-for-agent

# Core Foundation Iteration PRD

## Problem Statement

MyTryGetFramework 的路线 C 已经把框架收敛为纯客户端服务框架：Core 负责 ModuleSystem、EventModule、TGTask、Timer、Pool、Source Generator 等底层能力，UI、资源真实实现、场景、音频、热更新继续后置。

当前底层实现已经不再是空白：ModuleSystem 已有服务接口注册、依赖拓扑排序、多阶段 FramePhase 派发与 Shutdown 错误聚合；EventModule 已有类型安全事件、EventScope、派发中增删延迟 flush 与异常隔离；TGTask 已有池化、version 防过期、取消模型和 phase-aware scheduler；Timer、Pool、Source Generator 也已有可运行实现与测试。

真正的问题是：这些底层能力是在多轮路线迁移、参考框架对照和增量硬化中形成的，契约边界、回归测试、诊断可见性和文档同步还没有被一次完整的底层迭代收口。后续 UI、资源、场景、音频等上层模块会大量依赖这些能力；如果底层契约没有被稳定地锁住，上层会在异步取消、事件生命周期、Module 依赖、Timer 长帧补偿、Pool 复用、生成器注册漂移等地方反复返工。

## Solution

本迭代把 Core 底层作为一组可独立验证的深模块来收口。目标不是重写现有实现，而是基于现有 MyTryGetFramework 代码和 ReferenceFramework 对照，把核心运行时契约固定下来，补齐能证明契约的测试、诊断和文档。

迭代完成后，开发者可以把 ModuleSystem、EventModule、TGTaskScheduler、TimerModule、PoolModule、ModuleRegistry、EventHandlerRegistry 和 Source Generator 当作稳定地基使用。上层模块只需要依赖这些明确的外部行为，不需要知道底层队列、池化、延迟 flush、生成器 manifest 或 Unity dll 同步细节。

ReferenceFramework 的作用是提供取舍依据，不直接照搬：

- DGame / TEngine 的 ModuleSystem 证明客户端框架需要统一模块入口，但其静态全局容器、反射按命名猜实现、隐式依赖顺序不进入 TryGet Core。
- DGame / TEngine 的 EventDispatcher 和 GameEvent Analyzer 证明事件需要编译期约束与派发中增删保护，但 TryGet 保持 struct 事件与 EventModule 类型安全模型，不回退到 int/string event id。
- DGame 的 GameTimer 证明 Timer 需要暂停、恢复、循环次数和长帧补偿语义；TryGet 保持跨端纯 C# TimerModule 和 TGTaskScheduler 协作，不引入 Unity 依赖。
- DGame / TEngine 的 MemoryCollector、ObjectPool 与调试窗口证明 Pool 需要诊断与双释放保护；TryGet 保持泛型对象池契约和 DEBUG/UNITY_ASSERTIONS 防护。
- DGame / TEngine 的 Source Generator / Analyzer 证明编译期生成与诊断能降低事件和注册错误；TryGet 保持 `[Module]` / `[EventHandler]` 自动注册和 `Generators~/` source-of-truth 约束。

## User Stories

1. As a framework maintainer, I want ModuleSystem registration rules to be locked by tests, so that Core cannot drift back to concrete-class or framework-base-interface registration.
2. As a framework maintainer, I want Module dependencies to be initialized by explicit topology, so that Module startup is deterministic and not dependent on registration order.
3. As a framework maintainer, I want Initialize failure to roll back initialized Modules, so that partially started hosts do not leak live services.
4. As a framework maintainer, I want Shutdown to continue after individual Module failures and aggregate them, so that cleanup is best-effort but errors remain visible.
5. As a framework maintainer, I want all FramePhase methods to be guarded by IsInitialized, so that frame dispatch cannot run against a cold host.
6. As a module author, I want Priority to act only as a same-layer tie-breaker, so that dependency declarations always win over incidental priority values.
7. As a module author, I want ModuleSystem diagnostics to explain missing dependencies and cycles, so that dependency mistakes are actionable.
8. As a module author, I want GameLauncher to consistently pre-register ILogger, IClock, and ITGTaskScheduler, so that all Core hosts start from the same minimum baseline.
9. As a module author, I want generated Module registrations to use the same Register contract as manual registrations, so that generator and runtime paths cannot disagree.
10. As a gameplay programmer, I want EventModule events to be strongly typed structs, so that event payloads are checked by the compiler.
11. As a gameplay programmer, I want handler exceptions to be isolated and reported, so that one failing handler does not prevent other subscribers from seeing the event.
12. As a gameplay programmer, I want Subscribe and Unsubscribe during Publish to be deterministic, so that event lifecycle code is safe inside handlers.
13. As a gameplay programmer, I want EventScope to dispose subscriptions in bulk, so that Module and Procedure lifetimes can own their event handlers without leaks.
14. As a gameplay programmer, I want EventModule to protect against unbounded nested Publish, so that event cycles fail with a useful error instead of stack overflow.
15. As a framework maintainer, I want EventHandlerRegistry snapshots to include every generated handler, so that diagnostics can show what was actually registered.
16. As an async API author, I want TGTask to remain a lightweight Core primitive, so that framework async APIs do not depend on BCL Task or Unity coroutine semantics.
17. As an async API author, I want TGTask body pooling and version checks to be regression-tested, so that reused bodies cannot be observed through stale task handles.
18. As an async API author, I want TGCancelSource and TGCancelToken to support owner-scope cancellation, so that a Module, Procedure, future UI window, or future Scene can cancel its pending operations.
19. As an async API author, I want TGTask.Abort to be clearly limited to manual tasks, so that external code cannot cancel another async method's builder-created return task.
20. As an async API author, I want scheduler cancellation registrations to be disposed when tasks complete, so that long-lived owners do not accumulate stale cancellation callbacks.
21. As an async API author, I want WhenAll and WhenAny behavior to be locked, so that combinators do not leak task bodies or swallow errors unexpectedly.
22. As a module author, I want TGTaskScheduler to resume operations in a chosen FramePhase, so that async code can coordinate with ModuleSystem phase order.
23. As a module author, I want TGTaskScheduler to support scaled and unscaled time, so that gameplay delay and real-time delay have separate semantics.
24. As a module author, I want TimerModule to expose one-shot, repeat, pause, resume, cancel, and pending count behavior, so that simple time-based Core services do not need Unity timers.
25. As a module author, I want repeat timers to define long-frame catch-up behavior, so that missed intervals are handled intentionally.
26. As a module author, I want timer callbacks throwing exceptions to be reported after same-frame timers run, so that one callback failure does not hide later due timers.
27. As an async API author, I want TimerModule async extensions to align with TGTask cancellation policy or be deliberately scoped, so that Timer and TGTask do not create two incompatible delay models.
28. As a performance-sensitive developer, I want PoolModule to expose hit, miss, active, idle, and peak diagnostics, so that pool behavior can be inspected without a debugger.
29. As a performance-sensitive developer, I want PoolModule to detect double Return in debug configurations, so that object reuse corruption fails early.
30. As a performance-sensitive developer, I want PoolModule Shutdown and DestroyPool behavior to be explicit, so that pools do not outlive their owning ModuleSystem accidentally.
31. As a source-generator author, I want `[Module]` diagnostics to catch abstract/static types, non-interface service types, and missing service interface implementations, so that bad registration inputs fail at compile time.
32. As a source-generator author, I want `[EventHandler]` diagnostics to catch non-static handlers, wrong signatures, and non-struct event parameters, so that bad event inputs fail at compile time.
33. As a source-generator author, I want generated manifests to call ModuleRegistry and EventHandlerRegistry by their current runtime names, so that runtime renames cannot silently desync generator output.
34. As a package maintainer, I want `Generators~/` to remain the generator source of truth and the runtime dll to remain a build artifact, so that Unity's Roslyn analyzer constraint is satisfied without losing source reviewability.
35. As a package maintainer, I want the generator build script to test, build Release, and sync the dll, so that source and committed analyzer artifact stay consistent.
36. As a Core maintainer, I want Shadow csproj builds and tests to cover this iteration, so that Runtime/Core remains pure C# and Unity-free.
37. As a Core maintainer, I want docs to use the route C glossary, so that future agents do not reintroduce System, World, EventBus, runtime reflection scan, or UI concerns into Core.
38. As a future UI implementer, I want this iteration to leave UI out of scope but stabilize TGTask, EventScope, Timer, Pool, and Source Generator contracts, so that the UI iteration can build on those contracts without redesigning them.

## Implementation Decisions

- Treat this as a contract-hardening iteration over existing Core modules, not a replacement rewrite.
- Preserve the route C glossary: Module, ModuleSystem, FramePhase, EventModule, EventScope, TGTask, TGTaskScheduler, TGCancelSource, TGCancelToken, TimerModule, PoolModule, Source Generator, GameLauncher.
- Preserve the Core pure C# boundary. Runtime/Core must not reference UnityEngine, YooAsset, PlayerPrefs, HybridCLR, UniTask, or other Unity / third-party runtime implementations.
- Preserve ModuleSystem registration discipline: registration uses service interfaces, never concrete Module types, framework base interfaces, or `IEventModule`.
- Preserve dependency semantics: DependsOn is the source of truth for initialization order; Priority is only the tie-breaker among topology-equivalent Modules.
- Preserve failure semantics: Initialize rollback is reverse-order best effort for modules already initialized; Shutdown is reverse-order best effort with aggregated failures.
- Preserve FramePhase order: EarlyUpdate, FixedUpdate, Update, LateUpdate, EndOfFrame.
- Keep GameLauncher as the standard host factory for baseline Core services and generator-applied registrations.
- Keep EventModule type-safe and struct-event based. Do not introduce int/string event id APIs into Core.
- Keep EventScope as the lifecycle mechanism for event subscriptions. Do not rename it to subscription token or owner.
- Keep handler exception reporting separate from dispatch flow: exceptions are visible but do not stop other handlers.
- Keep TGTask as the framework async primitive. Do not replace it with System.Threading.Tasks.Task, Unity coroutine, UniTask, or BCL CancellationToken in Core.
- Keep cancellation self-managed: `TGCancelSource` owns 1:N scope cancellation, `TGTask.Abort()` owns 1:1 manual task cancellation.
- Keep cancellation exceptions out of unobserved exception reporting.
- Keep scheduler queues phase-aware and explicit about scaled/unscaled time.
- Treat TimerModule and TGTaskScheduler as related but distinct services: TimerModule is callback-oriented; TGTaskScheduler is await-oriented. Any bridge must define cancellation and recycling behavior explicitly.
- Keep PoolModule generic and Unity-free. GameObject pooling belongs to a future Adapter / Unity-side iteration.
- Keep Pool diagnostics as external behavior because pool health is an operational concern, not an implementation detail.
- Keep Source Generator incremental, compile-time diagnostic-driven, and zero-runtime-reflection.
- Keep `Generators~/` as the generator source of truth and committed runtime dll as Unity-required artifact. Generator behavior changes must rebuild and sync the dll.
- Do not plan UI, resource real implementations, scene loading, audio, input, hot update, networking adapters, or GameObjectPool in this PRD.

## Testing Decisions

- Tests should assert external behavior and public contracts, not private collection shape, private queue storage, or exact internal algorithm unless a diagnostic contract exposes it.
- ModuleSystem tests should cover registration constraints, dependency topology, missing dependency/cycle diagnostics, Initialize rollback, Shutdown aggregation, phase dispatch guards, and GameLauncher baseline services.
- EventModule tests should cover typed publish/subscribe, duplicate subscription behavior, unsubscribe during publish, subscribe during publish, EventScope disposal, handler exception isolation, nested publish limit, and registry snapshot metadata.
- TGTask tests should cover pooling lifecycle, stale version behavior, single await behavior, Abort restrictions, token cancellation, scheduler phase behavior, scaled/unscaled delay, cancellation registration cleanup, WhenAll/WhenAny, and unobserved exception filtering.
- TimerModule tests should cover one-shot, repeat, pause/resume, cancel, callback exception aggregation, self-cancel/self-pause, scaled/unscaled behavior, long-frame catch-up, and TGTask async extension behavior.
- PoolModule tests should cover prewarm, hit/miss diagnostics, active/idle/peak counters, onReturn reset, DestroyPool, Shutdown cleanup, and debug double-return behavior.
- Source Generator tests should cover legal baselines, diagnostics TG0001-TG0006, metadata count for multiple modules/handlers per assembly, generated runtime registry names, no-output behavior when there are no attributes, and build-script dll sync expectations.
- Every slice should be verifiable through the Tests Shadow csproj when it touches Runtime/Core tests.
- Generator slices should be verifiable through the generator solution tests and, when behavior changes, the generator build script.
- Documentation synchronization should be checked against CONTEXT.md, ADR-0011, ADR-0012, ADR-0020, ADR-0021, ADR-0022, and the generator README.

## Out of Scope

- UI module design, window lifecycle, UI code generation, UI assets, UI interaction components, red dot, GM panel, and scroll view behavior.
- YooAsset or any concrete asset loading implementation.
- Unity SceneManager integration or concrete scene loading.
- Audio, input, localization, save data, config real implementations, hot update, HybridCLR, PlayerPrefs, and platform adapters.
- GameObjectPool or Unity object pooling.
- Server-side abstractions, INetServer, ConnectionId, ITickLoop, IPlugin, PlugPoint, custom ECS, Entity, Aspect, Query, SystemGroup, or World resurrection.
- Runtime reflection scanning for Module or EventHandler discovery.
- A broad performance rewrite of queues, linked lists, heaps, or pools unless a specific contract test exposes a bug.

## Further Notes

- Existing code and tests already cover much of the desired behavior. AFK agents should first run the current tests, inspect failures or missing coverage, and then make the smallest necessary edits.
- ReferenceFramework should be used as evidence for behavior shape and risk areas, not as source to port wholesale.
- If a planned issue discovers the current implementation already satisfies all acceptance criteria, the correct completion is to add or tighten the missing tests/docs rather than churn working code.
