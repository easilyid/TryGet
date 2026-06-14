# MyTryGetFramework

MyTryGetFramework 是一个纯客户端的 Unity 游戏服务框架上下文（V2.0 路线 C，ADR-0020）。框架围绕**服务生命周期管理**和**游戏流程编排**建模：用 `Module` 表达可被框架托管的服务单元，用 `ModuleSystem` 作为框架根容器，用 `Procedure`（栈式）编排游戏流程，用 `EventModule` 做类型安全的模块间通信，用 `TGTask` 提供零外部依赖的异步原语。Core 保持纯 C#（不引用 UnityEngine），由 Shadow csproj 持续验证跨端可编译性。

> **历史说明**：V0.1–V1.0 期间本上下文曾以变种 ECS（World/Entity/Aspect/System）建模。ADR-0020 起整套移除自制 ECS、服务端抽象、IPlugin 横切系统。下方词汇表已替换为路线 C 的真实领域语言；旧 ECS 词汇仅作历史保留，不再使用。

## Language (核心词汇)

**Module**:
被 `ModuleSystem` 托管的服务单元。通过服务接口（如 `ILogger`）注册，拥有 `OnInit` / `Shutdown` 生命周期、`Priority`（同级排序）和 `DependsOn`（依赖声明）。
_Avoid_: System, Manager, Service（裸用）

**ModuleSystem**:
框架根容器。负责 `Module` 的注册、依赖拓扑排序、生命周期驱动（Initialize / Shutdown）和多阶段帧派发，并持有全局 `EventModule`。
_Avoid_: Container, World, Kernel, ModuleHost（V0.x 旧名，已重命名）

**FramePhase**:
帧内固定的更新阶段，按 EarlyUpdate → FixedUpdate → Update → LateUpdate → EndOfFrame 顺序组织。`Module` 通过实现对应的帧接口（`IUpdateModule` / `IEarlyUpdateModule` / `IFixedUpdateModule` / `ILateUpdateModule` / `IEndOfFrameModule`）参与某阶段。
_Avoid_: Tick, Stage, Step

**Procedure**:
游戏流程的一个状态节点，拥有 `OnEnter` / `OnExit` / `OnPause` / `OnResume` / `OnUpdate` 生命周期。由 `ProcedureModule` 以**栈**方式编排（吸收 BigCat SceneMgr.stack）。
_Avoid_: State（裸用）, Scene, Screen

**ProcedureModule**:
流程状态机服务，以栈语义编排 `Procedure`：`Push`（暂停当前、入栈新节点）、`Pop`（出栈、恢复下层）、`Replace`（替换栈顶）、`Start` / `Stop`。
_Avoid_: FSM（裸用）, Router, Navigator

**EventModule**:
全局类型安全事件总线。事件类型约束为 `struct`，通过 `Publish<T>` / `Subscribe<T>` / `Unsubscribe<T>` 收发。由 `ModuleSystem` 持有。
_Avoid_: Message, Signal, Dispatcher, EventBus（V0.x 旧名，已重命名）

**EventScope**:
事件订阅作用域。`Dispose` 时批量解绑该作用域内注册的所有 handler，用于把订阅生命周期绑定到 `Module` / `Procedure` 等宿主，防止泄漏。
_Avoid_: Subscription, Token, Owner

**TGTask**:
自研异步原语（`readonly struct`，池化、version 防过期、单 continuation、单线程模型）。框架内一切异步操作用 `TGTask` 而非 `System.Threading.Tasks.Task`。
_Avoid_: Task（裸指 BCL Task）, Coroutine, Promise

**TGTaskScheduler**:
异步调度服务，把"下一帧 / 延迟 N 秒 / 等待 N 帧"转化为可 await 的 `TGTask`。提供 `Yield` / `Delay` / `WaitForFrames`，各含 `TGCancelToken` 重载（可取消）。
_Avoid_: Timer（裸用）, Dispatcher

**TGCancelSource / TGCancelToken**:
TGTask 的自研取消原语（ADR-0021）。`TGCancelSource`（class，池化）由 owner（Module / Procedure / 未来 UI / Scene）持有，`Cancel()` 一次取消其 `Token` 派生的全部 pending 异步操作（1:N，owner-scope）；`TGCancelToken`（`readonly struct`）是零分配的取消句柄，传给 `scheduler.Delay(.., token)` 等。取消语义为异常型（await 处抛 `OperationCanceledException`）；单操作可用 `TGTask.Abort()` 句柄式取消。**不使用** `System.Threading.CancellationToken`（保持单线程零锁、零 GC，契合 TGTask 哲学）。
_Avoid_: CancellationToken（.NET BCL，路线 C 不用其线程安全模型）, CancelFlag

**Source Generator (自动注册)**:
编译期生成注册代码的机制。`[Module]` 特性 → 自动注册到 `ModuleRegistry`；`[EventHandler]` 特性 → 自动订阅到 `EventModule`（经 `EventHandlerRegistry`）。零运行时反射。
_Avoid_: Reflection（运行时反射，路线 C 不用）, Scan

**GameLauncher**:
标准化启动入口。`CreateHost` 创建 `ModuleSystem` 并预注册 Core 基础三件套（`ILogger` / `IClock` / `ITGTaskScheduler`），再 ApplyAll 应用 Source Generator 累积的自动注册。
_Avoid_: Entry（裸用）, Main, Startup（裸用）, Bootstrap（V0.x 旧名，已重命名）

**INetClient**:
客户端网络连接契约（V2.0 简化版，无服务端模型）。Core 仅持接口，具体协议实现（TCP/KCP/WebSocket）留给 Adapter（V2.7）。
_Avoid_: Socket, Connection（裸用）, INetServer（已移除）

**IClock**:
时间查询服务，提供 DeltaTime / ElapsedTime / FrameCount，解耦 Unity 的 `Time`。
_Avoid_: Time, Timer

**数据源契约 (IKVStore / IConfigSource / IAssetSource / ISerializer)**:
Core 只持接口、不含真实实现（除内存 mock）。真实实现由 Unity 侧 Adapter 注入（如 YooAsset 实现 `IAssetSource`）。
_Avoid_: 把具体实现（PlayerPrefs / YooAsset / JSON 库）写进 Core

**Adapter**:
连接框架 Core 契约与外部运行环境（Unity / 网络协议 / 序列化库）的桥接单元，**不在 Core**。
_Avoid_: Wrapper, Bridge, Proxy

## Relationships (关系)

- 一个 **ModuleSystem** 托管零个或多个 **Module**。
- 一个 **Module** 通过其服务接口注册到唯一的 **ModuleSystem**。
- 一个 **Module** 可声明 `DependsOn` 依赖其他 **Module**；**ModuleSystem** 据此做拓扑排序决定 `OnInit` 顺序，`Shutdown` 按逆序。
- 一个 **Module** 可实现一个或多个帧接口，从而参与对应的 **FramePhase**。
- **ModuleSystem** 持有唯一的 **EventModule**，供所有 **Module** 与业务代码共享。
- 一个 **EventScope** 持有一组解绑动作，`Dispose` 时统一解绑；通常绑定到一个 **Module** 或 **Procedure** 的生命周期。
- 一个 **ProcedureModule** 以栈方式持有零个或多个 **Procedure**；同一时刻栈顶 **Procedure** 为活动节点，其余被暂停。
- 一个 **Procedure** 在被 `Push` 覆盖时 `OnPause`，在上层 `Pop` 后 `OnResume`。
- **TGTaskScheduler** 是一个实现 `IUpdateModule` 的 **Module**，每帧驱动到期的 **TGTask** continuation。
- **GameLauncher** 创建 **ModuleSystem** 并预注册基础 **Module**，业务再追加自己的 **Module** 后调用 `Initialize`。
- 数据源契约（**IKVStore** 等）与 **INetClient** 的真实实现由 **Adapter** 提供，不进入 Core。

## Example dialogue (对话示例)

> **Dev (开发者):** "玩家进入暂停菜单时，要把战斗流程销毁再重建吗？"
> **Domain expert (领域专家):** "不 — 战斗是一个 **Procedure**。暂停菜单用 `ProcedureModule.Push("PauseMenu")`，战斗 **Procedure** 收到 `OnPause` 但保留在栈里；关闭菜单 `Pop` 后战斗 `OnResume` 原样恢复。状态不丢。"

> **Dev:** "我想在所有 **Module** 更新之前先跑一段输入采集，放哪？"
> **Domain expert:** "让那个 **Module** 实现 `IEarlyUpdateModule`，它就在 **FramePhase** 的 EarlyUpdate 阶段被 **ModuleSystem** 驱动，先于普通 Update。"

## Flagged ambiguities (标记的歧义项)

- "System" 在路线 C **不再是核心概念**（自制 ECS 已移除）。需要表达可托管服务时用 **Module**。
- "Task" 默认指自研的 **TGTask**；如确指 BCL `System.Threading.Tasks.Task` 必须显式写全名。框架内异步一律用 TGTask。
- "Scene" 仅用于未来 Unity 场景加载语义（V2.5 `ISceneModule`），不用来表达流程；游戏流程一律用 **Procedure**。
- "Event" 指 **EventModule** 的 `struct` 事件；订阅生命周期归 **EventScope** 管理。不要引入裸 int/string eventId 的弱类型派发。
- "Unity integration"（YooAsset / AudioSource / SceneManager / PlayerPrefs）属于 **Adapter** 职责，不应出现在 Core 的 Module 契约或实现中。
- "注册"默认指 **Source Generator** 编译期自动注册（`[Module]` / `[EventHandler]`）或显式 `host.Register<T>()`；路线 C **不使用运行时反射扫描**。
- "Token" 在事件域是 **EventScope** 要避免的命名（订阅作用域用 `EventScope`）；在异步取消域，`TGCancelToken` 是取消句柄的正式命名（ADR-0021）。两者分属不同领域，不冲突。

## Design constraints (设计约束 — 源自 ADR-0011 / ADR-0012 / ADR-0020)

**Core 纯 C# 边界（ADR-0012 / ADR-0020）:**
- `Runtime/Core/` 不得 `using UnityEngine` 或任何 Unity / 第三方协议库。
- 由 `ServerProject/` 的 Shadow csproj（netstandard2.1，反向引用 Core 源）`dotnet build` 持续验证。
- 任何"需要看屏幕、听声音、读 PlayerPrefs、连网络协议"的能力都属 Adapter，不在 Core。

**ModuleSystem 契约（ADR-0011）:**
- 注册必须用服务接口、不能用框架基础接口（`IModule` / 各帧接口 / `IEventModule`）或具体类；同一接口重复注册抛异常。
- `Initialize` 做拓扑排序 + 依次 `OnInit`，中途失败倒序 `Shutdown` 回滚；`Shutdown` 按 `OnInit` 逆序、单 Module 异常不中断其余、最终聚合抛出。
- 帧派发方法（EarlyUpdate / FixedUpdate / Update / LateUpdate / EndOfFrame）仅在 `IsInitialized` 后有效。

**路线 C 红线（ADR-0020）:**
- 不恢复自制 ECS（Entity / Aspect / Tag / Query / System / SystemGroup / IPureComponent）。
- 不恢复服务端抽象（INetServer / ConnectionId / ITickLoop）。
- 不恢复 IPlugin / IPlugPoint / IPluginHost 横切系统。
- 不为未来双端预留复杂系统（双端推迟 V3.0+）。
- 命名表达真实职责，不为兼容旧设计保留错误命名。

## V2.0 scope (V2.0 范围)

V2.0 路线 C 收敛覆盖：服务骨架（**ModuleSystem** / **Module** / **GameLauncher**）、全局事件（**EventModule** / **EventScope**）、异步原语（**TGTask** 全家桶 / **TGTaskScheduler**）、流程编排（**ProcedureModule** 栈模式）、基础服务（**ILogger** / **IClock** / **ITimerModule** / **IPoolModule**）、数据源契约（**IKVStore** / **IConfigSource** / **IAssetSource** / **ISerializer**）、**Source Generator** 自动注册（`[Module]` / `[EventHandler]`）、简化 **INetClient**。其中 **FramePhase** 多阶段 Update（原属 V2.2）已提前落地。

UI、资源加载真实实现、场景管理、音频、客户端网络 Adapter、热更新都**有意不在 V2.0 范围**，按 V2.1–V2.8 路线图逐步补齐（见 `docs/strategy/V2-roadmap-tasks.md`）。双端扩展推迟到 V3.0+。
