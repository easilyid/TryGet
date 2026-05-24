# MyTryGetFramework V2 — 商业 Unity 框架基础架构设计

> **撰写日期**：2026/05/24
> **状态**：草案，等待用户 review
> **作用**：在 `V2-direction-pivot.md`（做减法）和 `ADR-0016`（Adapter 退场）的基础上，**做加法**——定义 V0.6+ 商业框架基础架构应有的形态。
> **范围**：架构设计本体，不是路线图（路线图在 `ARCHITECTURE.md`）、不是 ADR（ADR 是单点决策）。
> **依据**：4 框架对标（ET / Fantasy / Hsenl / TEngine）+ BigCat 设计思想 + 2025-2026 现代基础设施调研。

---

## 0. 设计方法与原则

### 0.1 三条铁律

1. **Core 跨端有效性**：Core 必须在 `dotnet console` 中跑通完整业务流程（Bootstrap、Entity、System、Event、异步、状态机、配置、序列化、KV 存储），不依赖 `UnityEngine.*`。
2. **Adapter 边界明确**：任何"需要看屏幕、听声音、读 `PlayerPrefs`、连 InputSystem"的能力**统统在 Samples/ 或独立 Adapter 程序集**。
3. **零反射可选**：核心 API 不依赖反射。Source Generator 是 V0.9 引入的优化层，不改变 API。

### 0.2 与 V0.5.5 现状的衔接

| 现状（V0.5.5 后 Core） | V2 后保留 / 重构 / 抛弃 |
|---|---|
| `ModuleHost / IModule / DependsOn / Priority` | 保留（V0.9 增 Source Generator 注册） |
| `IEventBus / IWorldEventBus / EntityEventDispatcher` | 保留（双层结构与 hsenl/ET 谱系一致） |
| `EntityWorld / Entity / Aspect / Tag / Phase / Handle` | 保留（V2 增 ADR-0017 说明与 ET 纯数据 Component 路线的分歧） |
| `SystemBase / SystemGroup / Query` | 保留（Query 仍仅 All-of + None-of，Any-of 仍延后） |
| `BitArray256 / TypeIndex<T>` | 保留（V0.9 评估升级为 BitListBucket 动态长度） |
| `ILogModule / ITimerModule / IPoolModule / IProcedureModule` | 保留（V0.7 把 ILogModule 改名为 ILogger） |
| `IResourceModule + MemoryResourceModule` | **V0.8 重构** → `IAssetSource + ISerializer` |
| `ISaveModule + MemorySaveModule` | **V0.8 重构** → `IKVStore` |
| `IConfigModule + MemoryConfigModule` | **V0.8 重构** → `IConfigSource + ConfigLoader<T>` |
| `ILocalizationModule + MemoryLocalizationModule` | **V0.8 评估降级** 到 `Optional/`，不算 Core |

V0.5.5 已经把 Audio/Input/UI/Scene/PlayerPrefsSave 整套迁到 `Samples/Unity/Adapters/`（见 ADR-0016），本设计文档不再重复决策这部分。

---

## 1. 4 框架对标矩阵（决定 V2 取舍的 10 个维度）

> 信源：`V2-direction-pivot.md` §2 + deepwiki ET/Fantasy 二次问答 + Explore agent 本地源码扫描（hsenl/BigCat）。

| # | 维度 | ET | Fantasy | Hsenl | TEngine | V2 取舍 |
|---|---|---|---|---|---|---|
| 1 | **启动模型** | Loader + Hotfix dll + ENABLE_VIEW | `RuntimeInitializeOnLoad` (Unity) + `ModuleInitializer` (Net) 双 Entry | `Framework` 纯 C# + `FrameworkProxy` MonoBehaviour | `RootModule` MonoBehaviour + Procedure FSM | **`IEntry` 双端 + Samples/Net/Program + Samples/Unity/TryGetMonoEntry** |
| 2 | **ECS 数据/行为** | Component 纯数据 + Event class（IAwakeSystem/IUpdateSystem 扩展方法挂行为） | 同 ET | `Component` 仍持生命周期方法（接近 Aspect 但更轻） | 无 ECS | **保留 Aspect-with-methods（差异化）+ V0.9 引入"纯数据 Component"作二级方案** |
| 3 | **System 注册** | 手动 + Event class 反射扫描 | **Source Generator 全家桶**（零反射、AOT 友好） | `EventSystem` Attribute 驱动反射 | 手动 `host.Register` | **V0.6-V0.8 手动；V0.9 加 SourceGen 可选路径** |
| 4 | **异步原语** | `ETTask`（pool + ctx 传递，ET9 起取消 CancellationToken） | `FTask` 自研，与 ET 同思路 | `HTask` struct + `AsyncHTaskMethodBuilder` + Version 防过期 | UniTask | **自研 `ITask`，参考 hsenl HTask 的 struct + Version 防过期 + ETTask 的 ctx 传递** |
| 5 | **网络抽象** | Actor + MailBox + Location Server + Fiber | Single Session + `NetworkProtocolType` 枚举切 TCP/KCP/WS/HTTP | `Channel`（TCP/KCP）+ `Plug` 中间件管道 | 不在 Core | **接口在 Core（V1+）；Actor / Location 延后到 V2+ 业务层** |
| 6 | **序列化** | Protobuf | Protobuf + MemoryPack 混用 | MemoryPack | LitJson | **`ISerializer` 抽象 + MemoryPack 推荐 Adapter** |
| 7 | **配置** | Luban | 自研 ConfigTool + Source Generator | Luban Editor 集成 | ScriptableObject + LitJson（无 Luban） | **`IConfigSource + ConfigLoader<T>` 抽象 + Luban Adapter** |
| 8 | **BitMask 类型索引** | `TypeIndex` + `EntityComponentChangeSystem` | `TypeIndex` 同 ET | `ComponentTypeCacher : Bitlist`（继承关系矩阵）+ `Bitlist` 动态位图 | 无 | **强化现有 `BitArray256`：V0.9 评估升级为 `BitListBucket`（动态长度）+ 类继承位图（借鉴 hsenl）** |
| 9 | **双端编译** | `ET.Model` / `ET.Hotfix` 物理共享 dll（csproj include 反向引用源文件） | Platform.Unity.Entry vs Platform.Net.Entry + 条件编译宏 | Network 完全独立 asmdef + `noEngineReferences: true` | 单端（Unity 写进 Core） | **保留 Shadow csproj（ADR-0012）+ V0.7 双 Entry 显式化** |
| 10 | **热更 / 资源** | HybridCLR + Luban + Addressables | HybridCLR | HybridCLR + YooAsset | HybridCLR + YooAsset + Luban + UniTask | **Core 不含；接口（`IHotfixLoader / IAssetSource`）在 Core，实现在 Samples/Unity/Adapters/ V1.1+** |

### 1.1 四个最有启示的独创点

1. **Hsenl 的 `Plug` 中间件管道**（`Network\Common\Channel\IPlug.cs` 系列）：网络层加密、流量监控、异常处理作为可插拔中间件，不污染主路径。**V0.9 引入到 Core 作为通用扩展点 `IPluginHost`**。
2. **Hsenl 的 `HTask + Version 防过期`**（`Runtime\Internal\Universal\Core\HTask\HTask.cs`）：struct 实例持 `_body` 弱引用 + `_version` 防止状态机已 Reset 后误触发。**V0.6 直接借鉴**。
3. **Fantasy 的 `[ModuleInitializer]` + Source Generator 双 Entry**：编译期生成 `__AssemblyManifest.g.cs`，Unity 用 `RuntimeInitializeOnLoadMethod` 触发、Net 用 `ModuleInitializer` 触发，**同一份 Manifest 双入口执行**。V0.9 引入。
4. **TEngine 的 `GameEventMgr` 自动解绑机制**（`Assets/TEngine/Runtime/Core/GameEvent/GameEventMgr.cs:33-49`）：每个订阅者持自己的 `GameEventMgr` 局部实例，UI 销毁时一次 `Clear()` 批量解绑所有 handler，**零 GC + 防止 UI 失效后的回调悬挂**。**V0.7 ILogger / IEventBus 升级时一起评估引入 `IEventScope` 概念**。

### 1.2 三个被拒绝的设计

1. **ET Actor / Fiber / Location Server**：MMO 级复杂度，**V2 不进 Core**，留 V2+ 业务层。
2. **TEngine `GameModule` 静态 facade**：污染跨端纪律，违反"Core 跨端有效性"原则。
3. **Fantasy 全反射依赖**：Fantasy 设计目标是 Native AOT；V2 暂不强求 Native AOT，所以 Source Generator 延后到 V0.9，先用手动注册保持简单。

---

## 2. V2 架构分层（含 Core / Adapter / Sample 三层）

```
┌─────────────────────────────────────────────────────────────┐
│  Samples / Adapter 层（V1.1+ 独立程序集）                   │
│  ──────────────────────────────────────────────────────     │
│  Samples/Unity/Adapters/   Audio | Input | UI | Scene |     │
│                            PlayerPrefsSave                  │
│  Samples/Unity/Bootstrap/  TryGetMonoEntry                  │
│  Samples/Net/Bootstrap/    Program.cs                       │
│  Adapters/HybridCLR/       HybridCLRLoader (V1.1+)          │
│  Adapters/YooAsset/        YooAssetSource (V1.1+)           │
│  Adapters/Luban/           LubanConfigSource (V1.1+)        │
│  Adapters/MemoryPack/      MemoryPackSerializer (V0.8)      │
│  Adapters/Network/         KcpChannel / TcpChannel (V1+)    │
└─────────────────────────────────────────────────────────────┘
                          ↑ depends on
┌─────────────────────────────────────────────────────────────┐
│  Adapter 桥接层（Unity 端薄壳，asmdef noEngineRefs=false）  │
│  ──────────────────────────────────────────────────────     │
│  Runtime/Unity/                                             │
│    WorldProxy.cs               MonoBehaviour 主循环驱动     │
│    UnityClock.cs (V0.7)        IClock 的 Unity Time 实现    │
│    UnityLoggerSink.cs (V0.7)   ILogger Sink 写 Debug.Log    │
└─────────────────────────────────────────────────────────────┘
                          ↑ depends on
┌─────────────────────────────────────────────────────────────┐
│  Core 框架基础架构（跨端，asmdef noEngineRefs=true）        │
│  ──────────────────────────────────────────────────────     │
│  Runtime/Core/                                              │
│                                                             │
│  Bootstrap          IEntry / IBootstrap / BootstrapContext  │
│  Async (V0.6)       ITask / ITaskCompletionSource /         │
│                     TaskPool / TaskScheduler                │
│  Module             IModule / IModuleHost / IUpdateModule / │
│                     ILateUpdateModule / ModuleHost          │
│  生命周期           Phase（Entity 帧内）+ Procedure（跨帧） │
│  Entity 体系        IEntityWorld / EntityWorld / Entity /   │
│                     Aspect / Tag / Handle / EntityId        │
│  System 调度        SystemBase / SystemGroup / Query        │
│  事件总线           IEventBus / IWorldEventBus /            │
│                     EntityEventDispatcher                   │
│  状态机             IProcedure / IProcedureModule           │
│  服务接口（V0.7+）  ILogger / IClock                        │
│  服务接口（V0.8）   IKVStore / IConfigSource /              │
│                     IAssetSource / ISerializer              │
│  类型索引           BitArray256 / TypeIndex<T> /            │
│                     TypeRegistry                            │
│  扩展点（V0.9）     IPlugin / IPluginHost                   │
└─────────────────────────────────────────────────────────────┘
```

### 2.1 依赖方向不变量

```
Samples / Adapter 层  ───→  Adapter 桥接层  ───→  Core 基础架构
Tests/EditMode        ───→                       Core 基础架构
Tests/PlayMode        ───→  Adapter 桥接层  ───→  Core 基础架构
ServerProject/...     ───→                       Core 基础架构 (Shadow csproj)
```

**任何反向依赖（Core → Adapter）= 设计漏洞**，必须在 ADR 中明确豁免才允许。

---

## 3. 关键模块契约（V2 新增 / 重构）

### 3.1 异步原语 `ITask`（V0.6，最关键缺口）

**问题**：Core 当前完全没有异步原语，所有 Procedure / System 都是同步推进。商业框架必须有异步能力（资源加载、网络请求、UI 动效、跨帧等待）。

**对标选择**：
- ❌ 用 UniTask：Unity 锁死，无法纯 dotnet 跑
- ❌ 用 .NET `ValueTask`：单线程心智差，`CancellationToken` 污染调用链
- ✅ 自研 `ITask`：参考 hsenl `HTask` 的 struct + Version 防过期 + ETTask 的 ctx 传递

**API 草图**（V0.6 PRD 详细化）：

```csharp
[AsyncMethodBuilder(typeof(AsyncITaskMethodBuilder))]
[StructLayout(LayoutKind.Auto)]
public readonly partial struct ITask
{
    private readonly ITaskBody _body;
    private readonly int _version;          // 防 Pool 回收后误触发

    public Awaiter GetAwaiter();
    public bool IsCompleted { get; }
    public void Forget();                   // fire-and-forget 显式标记

    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        public bool IsCompleted { get; }
        public void GetResult();
        public void OnCompleted(Action continuation);
        public void UnsafeOnCompleted(Action continuation);
    }
}

public readonly partial struct ITask<T> { /* 同上，带泛型返回 */ }

public interface ITaskCompletionSource
{
    ITask Task { get; }
    void SetResult();
    void SetException(Exception e);
    void SetCanceled();
}

public static class TaskPool
{
    public static int MaxPoolSize { get; set; } = 64;
    public static (int active, int pooled) GetCacheInfo<T>();
}

public interface ITaskScheduler : IModule, IUpdateModule
{
    // 把延迟到下一帧 / 指定时间后执行的 continuation 串到 PlayerLoop
    ITask Yield();
    ITask Delay(float seconds);
    ITask WaitForFrames(int frameCount);
}
```

**核心实现要点**：
- `ITaskBody` 是 class（在 pool 中复用），`ITask` 是 struct（按值传递）。
- `_version` 防止 struct 被复制后又 await 已回收的 body。
- `AsyncITaskMethodBuilder` 实现 C# async 状态机协议，让 `async ITask MyMethod() { await ... }` 编译通过。
- `TaskPool` 限定上限，超过上限的 ITaskBody 不池化（防内存膨胀）。
- 单线程模型：`OnCompleted` 直接同步执行 continuation 或入队 `TaskScheduler`（与 PlayerLoop 同帧）；**不引入 ThreadPool**。

**与 Procedure 衔接**（V0.6 后期）：

```csharp
public interface IAsyncProcedure : IProcedure
{
    ITask OnEnterAsync();   // 取代 OnEnter，等返回再判定状态
    ITask OnExitAsync();
}
```

### 3.2 双端启动抽象 `IEntry`（V0.7）

**问题**：当前 V0.5.5 的 `WorldProxy.cs` 是 Unity MonoBehaviour 启动样例，但纯 dotnet 没有对应入口；用户也无法用同一份 Bootstrap 代码切换 Unity / Net。

**对标选择**：Fantasy 的 `Platform.Unity.Entry` + `Platform.Net.Entry` 双 Entry，配合 SourceGen 让两端跑同一份 `Initialize()`。V0.7 先做手动版本（不依赖 SourceGen）。

**API 草图**：

```csharp
public interface IEntry
{
    void RegisterModules(IModuleHost host);
    void ConfigureProcedures(IProcedureModule procedure);
    void OnAfterInitialize(IModuleHost host);
}

public static class Bootstrap
{
    public static IModuleHost Run(IEntry entry, BootstrapContext ctx)
    {
        var host = new ModuleHost();
        entry.RegisterModules(host);
        host.Initialize();
        if (host.TryGet<IProcedureModule>(out var p))
            entry.ConfigureProcedures(p);
        entry.OnAfterInitialize(host);
        return host;
    }
}

public sealed class BootstrapContext
{
    public IClock Clock { get; init; }
    public ILogger Logger { get; init; }
    public bool IsHeadless { get; init; }  // dotnet console = true, Unity = false
}
```

**Unity 端**：`Samples/Unity/TryGetMonoEntry.cs` 是 MonoBehaviour，`Awake` 内 `Bootstrap.Run(new MyEntry(), ctx)`，`Update` 调 `host.Update(Time.deltaTime, Time.unscaledDeltaTime)`。

**Net 端**：`Samples/Net/Program.cs` 的 `Main` 内 `Bootstrap.Run(new MyEntry(), ctx)`，主循环手写 `while (running) host.Update(dt, dt); Thread.Sleep(16);`。

### 3.3 `ILogger` / `IClock`（V0.7 服务抽象升级）

**问题**：
- `ILogModule` 名字过于"Module 化"，对外用户写日志要 `host.Get<ILogModule>().Info(...)`，啰嗦。
- `Time.deltaTime` 是 Unity 强依赖，Core 当前没有时钟抽象，无法在 Net 端获取统一时间。

**API 草图**：

```csharp
public interface ILogger : IModule
{
    void Trace(string msg, params object[] args);
    void Debug(string msg, params object[] args);
    void Info (string msg, params object[] args);
    void Warn (string msg, params object[] args);
    void Error(string msg, Exception e = null, params object[] args);

    LogLevel MinLevel { get; set; }
    void AddSink(ILogSink sink);
}

public interface ILogSink
{
    void Write(LogLevel level, string message, Exception ex);
}

public interface IClock : IModule
{
    float DeltaTime { get; }            // 当前帧 dt（受时间缩放影响）
    float UnscaledDeltaTime { get; }    // 当前帧 dt（不受时间缩放影响）
    float Time { get; }                 // 启动到现在的秒数
    float UnscaledTime { get; }
    long TicksUtcNow { get; }           // DateTime.UtcNow.Ticks
    float TimeScale { get; set; }
}
```

**Unity 端实现**：`UnityClock : IClock` 内部读 `UnityEngine.Time.*`。
**Net 端实现**：`SystemClock : IClock` 内部读 `Stopwatch` + `DateTime.UtcNow.Ticks`，外部主循环把 dt 传进来。

### 3.4 KV / 配置 / 资源 / 序列化（V0.8 重构）

**问题**：现有 `ISaveModule`（KV 存储）、`IConfigModule`（配置查询）、`IResourceModule`（资源加载）三个接口形态过于 Unity 化，且强耦合 Memory 实现。V0.8 重构为更通用的"数据源 + 加载器"模式。

**重构对照表**：

| V0.5.5 接口 | V0.8 接口 | 设计理由 |
|---|---|---|
| `ISaveModule.GetString/Int/Bool` | `IKVStore.Get<T>(key) / Set<T>(key, value)` | KV 是基础抽象，业务"存档"语义太特化 |
| `IConfigModule.Register/Get` | `IConfigSource + ConfigLoader<T>` | 对标 Luban：类型化配置类 + 二进制加载 |
| `IResourceModule.Load<T>` | `IAssetSource.Load<T> + ISerializer` | 解耦"资源源"与"反序列化逻辑" |

**API 草图**：

```csharp
public interface IKVStore : IModule
{
    bool Has(string key);
    T Get<T>(string key, T defaultValue = default);
    void Set<T>(string key, T value);
    void Remove(string key);
    void Flush();   // 显式持久化（Memory 实现 no-op，Adapter 实现写盘）
}

public interface IConfigSource : IModule
{
    bool TryGetTable(string tableName, out byte[] bytes);   // 二进制访问
    bool TryGetJson(string tableName, out string json);     // JSON 访问（开发模式）
}

public sealed class ConfigLoader<T> where T : IConfigTable, new()
{
    public ConfigLoader(IConfigSource source, ISerializer serializer);
    public T Load(string tableName);
    public bool TryReload(string tableName, out T newTable);  // 热重载
}

public interface IConfigTable
{
    void Deserialize(byte[] bytes);
}

public interface IAssetSource : IModule
{
    ITask<T> LoadAsync<T>(string address);  // V0.6 后引入 ITask
    void Release<T>(string address, T asset);
    bool IsReady(string address);
}

public interface ISerializer : IModule
{
    byte[] Serialize<T>(T value);
    T Deserialize<T>(ReadOnlySpan<byte> bytes);
    void Register<T>();  // Source Generator 注册（V0.9）；或运行时注册（V0.8 默认）
}
```

**实现路线**：
- V0.8 Core 提供 Memory 实现（`MemoryKVStore` / `JsonConfigSource` / `MemoryAssetSource` / `JsonSerializer`）作为开发期默认。
- V1.1+ Adapter 层提供生产实现（`PlayerPrefsKVStore` / `LubanConfigSource` / `YooAssetSource` / `MemoryPackSerializer`）。

### 3.5 `IPlugin / IPluginHost`（V0.9，借鉴 hsenl Plug）

**问题**：横切关注点（日志、性能监控、加密、错误处理）目前必须在每个 Module 内手写，或者用 EventBus 跨切，但 EventBus 缺少"管道"语义。

**对标**：hsenl 的 `IPlug` 中间件管道在 Channel 层实现得很优雅（`Network\Common\Channel\IPlug.cs`），V0.9 把这个思路抽象成 Core 通用扩展点。

**API 草图**：

```csharp
public interface IPlugin
{
    string Name { get; }
    int Priority { get; }
    void OnAttach(IPluginHost host);
    void OnDetach();
}

public interface IPluginHost : IModule
{
    void Install(IPlugin plugin);
    void Uninstall(string name);
    IReadOnlyList<IPlugin> ActivePlugins { get; }
}

// 用法示例：把 Logger / Profiler / Auth 插到 ModuleHost 启动流
host.Get<IPluginHost>().Install(new ProfilerPlugin());
host.Get<IPluginHost>().Install(new AuthPlugin());
```

**Plug 不取代 Module**：Module 是"服务提供者"（有生命周期），Plug 是"切面"（订阅 Module 生命周期事件并在中间插入逻辑）。

---

## 4. ECS 模型设计取舍

### 4.1 问题陈述

| 视角 | 现状（V0.5.5）| ET / Fantasy 路线 |
|---|---|---|
| `Aspect` 形态 | 数据 + 行为（受 ADR-0007 隔离纪律：仅操作自身字段 + 发 Entity-Event） | 纯数据 `Component` + 行为外置（`IAwakeSystem<T>.Run(T)` 扩展方法挂行为） |
| 测试覆盖 | 80+ Aspect 测试，已稳定 | N/A |
| 心智模型 | 小 OOP 类（属性 + 验证 + 状态转换） | "Data over here, Behavior over there"（纯 DOD） |

### 4.2 V2 决定（提案）

**保留 `Aspect-with-methods` 作为 TryGet 的差异化设计选择**，理由：

1. 当前 80+ Aspect 测试投资大，破坏性重构成本高于收益。
2. ADR-0007 已经把 Aspect 的行为范围约束到"只操作自身字段 + 发 Entity-Event"，**实际等同于"小 OOP 状态机"**，不是 ET 反对的"OOP 滥用"。
3. 业务开发者更习惯 Aspect-with-methods 的心智模型（封装 / 验证 / 状态转换在一处）。
4. 性能敏感场景（大量 Entity + 频繁 Query）可以走 V0.9 新增的"纯数据 Component"二级方案。

**待办**：
- **写 ADR-0017**：明确"为何 TryGet 与 ET 谱系在 ECS 模型上分歧"——把上面 4 条理由沉淀。
- **V0.9 评估**：引入 `IPureComponent`（无方法、struct-friendly）作为性能敏感场景的二级方案，与 `Aspect` 并存。
- **V0.9 评估**：引入 `IComponentSystem<T>` 扩展方法接口（类似 ET `IUpdateSystem<T>`），让纯数据 Component 也能挂行为。

### 4.3 Query 模型不变

V0.9 继续仅支持 All-of + None-of 谓词（ADR-0009）。Any-of 仍延后，理由：
- ET / Fantasy 实战中 Any-of 用得很少（< 5% Query 场景）。
- BitArray256 实现 All-of / None-of 是位运算 O(1)，Any-of 需要额外的"任一位置位"判断，会拖慢热路径。
- 业务确实需要 Any-of 时可拆成多个 Query 串联。

---

## 5. 双端策略

### 5.1 现状（V0.5.5）

- `ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj` 反向引用 Unity 项目下的 `Runtime/Core/**/*.cs`（**不复制**，靠通配自动覆盖）。
- 编译验证：`cd ServerProject/MyTryGetFramework.Core && dotnet build`。任何 `using UnityEngine` 在 Core 树会立刻让此 csproj 编译失败。

### 5.2 V0.7 增强：双 Entry 显式化

- 新增 `Samples/Net/Program.cs`：dotnet console 主入口，跑 Boot → Login → InGame 三 Procedure 样例（不依赖 Unity）。
- 新增 `Samples/Unity/TryGetMonoEntry.cs`：Unity MonoBehaviour 入口（取代当前 `WorldProxy.cs` 的"启动样例"角色，`WorldProxy.cs` 保留为"如何把 EntityWorld 挂到 PlayerLoop"的纯技术示例）。
- 两端共享 `Samples/Shared/MmoEntry.cs`：实现 `IEntry`，注册同一份 Module 集合 + 配置同一份 Procedure。

### 5.3 V1.0 物理共享代码示例

- 新建 `Samples/Shared/TryGet.Shared.csproj`（或 asmdef + Shadow csproj）。
- 服务端 `MmoServer.csproj` 引用 `TryGet.Shared` + `TryGet.Core`。
- Unity 客户端 `MmoClient` Unity 工程引用 `TryGet.Shared`（asmdef）+ `TryGet.Core`（asmdef）。
- 共享：Entity 定义、Aspect 定义、网络消息、配置类。
- 不共享：Bootstrap、Adapter 实现。

### 5.4 条件编译 vs 程序集隔离 — 选哪个？

- **选程序集隔离（V2 默认）**：Core asmdef `noEngineReferences: true`；Adapter asmdef `noEngineReferences: false`。**不用 `#if UNITY_EDITOR` / `#if UNITY_2021_3_OR_NEWER`** 这类条件编译。
- 理由：条件编译让代码变成"双面"，对未来引入 Source Generator 不友好；程序集隔离让边界天然成立（编译失败 = 立刻发现）。
- 例外：Adapter 层内可以用 `#if UNITY_EDITOR` 隔离 Editor-only 代码。

---

## 6. 任务拆解（V0.6 → V1.0+）

> 每个 Iter 的"DoD"（Definition of Done）必须包含：(1) 接口 + 实现 + 测试三件套；(2) `dotnet build` Shadow csproj 通过；(3) 不引入 `UnityEngine.*` 到 Core；(4) CHANGELOG 条目。

### 6.1 V0.6 — `ITask` 异步原语

| Iter | 任务 | DoD |
|---|---|---|
| 0 | PRD `docs/design/V0.6-ITask.md` | 用户 review 通过 |
| 1 | `ITask` + `ITask<T>` + `Awaiter` + `AsyncITaskMethodBuilder` 骨架 | `async ITask MyMethod() { await Task1; }` 编译通过 |
| 2 | `ITaskCompletionSource` + `ITaskBody` 状态机 | `var tcs = new ITaskCompletionSource(); var t = tcs.Task; tcs.SetResult();` 工作 |
| 3 | `TaskPool` 状态机池化 + `_version` 防过期 | 100 万次 await 后 alloc < 1MB |
| 4 | `ITaskScheduler.Yield/Delay/WaitForFrames` + 与 `ITimerModule` 衔接 | `await scheduler.Delay(1f)` 在 1s 后 continuation |
| 5 | `IAsyncProcedure` + `ProcedureModule` 异步支持 | Procedure 可以 `OnEnterAsync()` 异步加载资源 |
| 6 | 测试 30+：基本 await / 异常 / pool / cancel / 嵌套 | 全绿 |
| 7 | CHANGELOG `[V0.6]` 条目 | merge |

### 6.2 V0.7 — Bootstrap + `ILogger` + `IClock`

| Iter | 任务 | DoD |
|---|---|---|
| 1 | `IEntry` + `Bootstrap.Run()` + `BootstrapContext` | 单测验证启动顺序 |
| 2 | `ILogger` + `ILogSink` 接口 + `ConsoleLoggerSink` | `ILogModule` 标记 `Obsolete`，路径迁移 |
| 3 | `IClock` 接口 + `SystemClock`（Net 实现） + `UnityClock`（Unity Adapter） | 时间一致性测试 |
| 4 | `Samples/Net/Program.cs` 跑 Boot→Login→InGame | `dotnet run` 控制台输出三 Procedure 转移 |
| 5 | `Samples/Unity/TryGetMonoEntry.cs` 取代 `WorldProxy.cs` 启动样例 | Unity PlayMode 跑通同样 Procedure |
| 6 | 测试 20+ | 全绿 |
| 7 | CHANGELOG `[V0.7]` 条目 | merge |

### 6.3 V0.8 — KV / Config / Asset / Serializer 重构

| Iter | 任务 | DoD |
|---|---|---|
| 1 | `IKVStore` + `MemoryKVStore`；`ISaveModule` 标记 `Obsolete` | Memory 实现 + 迁移指南 |
| 2 | `IConfigSource` + `JsonConfigSource` + `ConfigLoader<T>` | 加载 + 热重载测试 |
| 3 | `IAssetSource` + `MemoryAssetSource`；`IResourceModule` 标记 `Obsolete` | 与 `ITask` 集成 |
| 4 | `ISerializer` + `JsonSerializer` | 双向 round-trip 测试 |
| 5 | 兼容性：`ISaveModule / IConfigModule / IResourceModule` 桥接 `IKVStore / IConfigSource / IAssetSource`（Obsolete 期共存一个 minor 版本） | 现有测试不破 |
| 6 | 测试 25+ | 全绿 |
| 7 | CHANGELOG `[V0.8]` 条目 | merge |

### 6.4 V0.9 — Source Generator + Plug 系统

| Iter | 任务 | DoD |
|---|---|---|
| 1 | `TryGet.SourceGenerator` 独立 csproj 骨架 + 跑通最小 Hello World 生成 | `dotnet build` 触发 Generator |
| 2 | `[Module]` Attribute + 生成 `__AssemblyManifest.g.cs` 自动注册 Module | 手动 + 生成路径行为等价 |
| 3 | `[SystemRegister]` Attribute + 生成 System 注册 | 同上 |
| 4 | `[EventHandler]` Attribute + 生成 EventBus 订阅 | 同上 |
| 5 | `IPlugin / IPluginHost` 接口 + 默认 `PluginHost` 实现（借鉴 hsenl Plug 管道） | 安装 / 卸载 / 顺序测试 |
| 6 | 评估 ECS 二级方案：`IPureComponent` + `IComponentSystem<T>`（写 ADR-0017） | ADR 通过 |
| 7 | 测试 30+ | 全绿 |
| 8 | CHANGELOG `[V0.9]` 条目 | merge |

### 6.5 V1.0 — 真双端样例 + 文档冻结

| Iter | 任务 | DoD |
|---|---|---|
| 1 | `Samples/Shared/TryGet.Shared.csproj` + `MmoEntry.cs`（双端 Entry） | 两端编译通过 |
| 2 | `Samples/Net/MmoServerDemo`（dotnet console 服务端） | 跑 10 客户端连接、广播、断线重连 |
| 3 | `Samples/Unity/MmoClientDemo`（Unity Play 客户端） | 连服务端、收广播、显示 |
| 4 | 共享 `Aspect` / `Entity` / 网络消息 | 一份代码两端工作 |
| 5 | 文档冻结：`ARCHITECTURE.md` V1.0 完整版 + 所有 ADR 状态确认 | review 通过 |
| 6 | CHANGELOG `[V1.0]` 条目 | merge |

### 6.6 V1.1+ — 业务扩展层（独立仓库或 Samples/）

> **不在 Core 范围**，但作为 V1.0 完整闭环的实现示例需要给出。

| 模块 | 优先级 | 说明 |
|---|---|---|
| `Adapters/Network/` (KCP/TCP/WebSocket Channel + 拆粘包 + Plug 加密) | 高 | V1+ 真做时重写 ADR-0014（当前 Superseded） |
| `Adapters/HybridCLR/` (`IHotfixLoader` Unity 实现) | 高 | 接口在 Core，实现 Unity 端 |
| `Adapters/YooAsset/` (`IAssetSource` 实现) | 高 | 替代 V0.8 `MemoryAssetSource` |
| `Adapters/Luban/` (`IConfigSource` 实现 + Editor 生成器) | 高 | 替代 V0.8 `JsonConfigSource` |
| `Adapters/MemoryPack/` (`ISerializer` 实现) | 高 | V0.8 可以直接做（同步） |
| `Samples/Unity/Adapters/` (V0.5.5 已迁的 Audio/Input/UI/Scene/PlayerPrefsSave) | 维护 | 保持工作 |
| `Adapters/Actor/` (Actor + MailBox + Location，参考 ET) | 中 | V2+ 业务层 |

---

## 7. 决策点（需要用户 review）

| # | 决策 | 推荐方案 | 风险 |
|---|---|---|---|
| 1 | **异步原语自研 vs UniTask 包装** | **自研** `ITask`（V0.6） | 实现复杂度高，状态机池化 + Version 防过期需要小心 |
| 2 | **Aspect 保留行为 vs 走 ET 纯数据** | **保留 Aspect-with-methods**（写 ADR-0017 说明分歧），V0.9 增 `IPureComponent` 二级方案 | 与 ET / Fantasy 主流路线分歧，需要文档清晰说明 |
| 3 | **Source Generator 引入时机** | **V0.9**（先用手动注册到 V0.8） | 学习曲线 + 调试难度增加；不引入则手动注册量随业务增长 |
| 4 | **`Plug` 系统是否进 Core** | **V0.9 进 Core**（但 `IPluginHost` Module 可选注册） | 增加 Core 概念数量；与 EventBus 心智重叠 |
| 5 | **`ILogModule` → `ILogger` 命名替换** | **V0.7 替换**（Obsolete 一个 minor 版本） | 迁移成本（外部业务调用点） |
| 6 | **`IEventBus` + `IWorldEventBus` 双层结构** | **保留**（World-scope vs Global-scope 作用域分明，对标 hsenl EventSystem 双 EventManager） | 概念冗余，可能引发"哪个用哪个"的困惑 |
| 7 | **双端策略：条件编译 vs 程序集隔离** | **程序集隔离**（Core asmdef noEngineRefs=true + Shadow csproj） | 强约束，业务团队可能不熟悉 |
| 8 | **网络抽象进 Core 时机** | **V1+ 接口（IChannel / ISession）进 Core；实现在 Adapter** | 接口设计错了要重写 |
| 9 | **Native AOT 友好性** | **V2 不强求 Native AOT**（Source Generator 让接近 AOT 友好但不保证 100%） | Mono 转 AOT 出问题时返工成本高 |
| 10 | **HybridCLR / YooAsset / Luban 是否进 Core 接口层** | **接口在 Core**（`IHotfixLoader / IAssetSource / IConfigSource`），**实现在 Adapter**（V1.1+） | 接口抽象太薄 → Adapter 重复造轮子；太厚 → 锁死实现选择 |
| 11 | **是否引入 `IEventScope`（订阅自动解绑）** | **V0.7 EventBus 升级时引入**（借鉴 TEngine `GameEventMgr`） | 与现有 `Subscribe / Unsubscribe` API 共存的迁移成本 |

---

## 8. 与 4 框架的最终边界对比

| 维度 | V2 TryGet | ET | Fantasy | Hsenl | TEngine |
|---|---|---|---|---|---|
| Core 跨端有效性 | ✅ 强制 | ✅ 强制 | ✅ 强制 | ✅ 强制 | ❌ 单端 |
| 异步原语 | 自研 `ITask` | `ETTask` | `FTask` | `HTask` | UniTask |
| ECS 数据/行为 | Aspect-with-methods（分歧） | 纯数据 + System | 纯数据 + System | Component-with-lifecycle | 无 ECS |
| Source Generator | V0.9 引入 | 否 | 全家桶 | 否 | 否 |
| 网络 / Actor | V1+ 业务层 | Actor + Fiber | Session + 协议枚举 | Channel + Plug | 不在 Core |
| 资源 / 配置 / 序列化 | 接口在 Core + Adapter | Luban + Addressables | ConfigTool + Protobuf | Luban + MemoryPack | Luban + YooAsset |
| 热更 | 接口在 Core + HybridCLR Adapter | HybridCLR | HybridCLR | HybridCLR | HybridCLR |
| 命名识别度 | "组合建模 + 跨端基础架构" | "MMORPG 双端 + Fiber + Actor" | "Source-Generator 全家桶 + Vibe Coding" | "ET 风格 + 自研 HTask 轻量化" | "Unity 客户端商业组合" |

---

## 9. 与现有文档的关系

| 文档 | 关系 |
|---|---|
| `docs/strategy/V2-direction-pivot.md` | 战略方向（做减法）→ 本文档承接做加法 |
| `docs/adr/0016-adapter-layer-out-of-core-scope.md` | Adapter 退场决策 → 本文档默认前提 |
| `docs/adr/0007-aspect-behavior-scope.md` | Aspect 隔离纪律 → 本文档 §4 保留 |
| `docs/adr/0008-system-registration-and-execution.md` | System 显式注册 → V0.9 SourceGen 后增"可选自动注册"路径 |
| `docs/adr/0011-modulehost-imodule-contract.md` | ModuleHost 契约 → 本文档默认前提 |
| `docs/adr/0012-shadow-csproj-dual-end-compilation.md` | Shadow csproj 双端编译 → 本文档 §5 强化 |
| **新写**：`docs/adr/0017-aspect-vs-pure-component-divergence.md` | V0.9 引入 `IPureComponent` 时同步 |
| **新写**：`docs/design/V0.6-ITask.md` | V0.6 启动时同步 |
| **新写**：`docs/design/V0.7-Bootstrap.md` | V0.7 启动时同步 |
| **更新**：`Assets/MyTryGetFramework/ARCHITECTURE.md` | V0.6 / V0.7 / V0.8 / V0.9 / V1.0 落地后逐步更新 |

---

## 10. Verdict

接受本文档作为 V0.6+ 的"加法" 路线起点。当前已知风险：

1. ITask 自研：实施周期 2-3 周（V0.6），失败回退方案 = 用 UniTask 但只在 Adapter 层，Core 提供 `ITask` 接口由 UniTask 实现（妥协方案）。
2. Aspect 路线分歧：ADR-0017 必须解释清楚分歧理由，否则未来新成员会反复挑战。
3. Source Generator 学习曲线：V0.9 启动前至少出一份 SourceGen 学习指南。
4. 业务团队的 KV/Config/Asset 接口迁移：V0.8 Obsolete 期至少跨一个 minor 版本，给业务团队过渡时间。

---

## 附：信源清单

| 类别 | 信源 |
|---|---|
| 战略前情 | `docs/strategy/V2-direction-pivot.md` |
| Adapter 退场 | `docs/adr/0016-adapter-layer-out-of-core-scope.md` |
| ET 深度 | deepwiki egametang/ET 二次问答（2026/05/24） |
| Fantasy 深度 | deepwiki qq362946/Fantasy 二次问答（2026/05/24） |
| Hsenl 深度 | Explore agent 本地源码扫描（`ReferenceFramework/hsenl/HsenlFramework/`） |
| BigCat 定位 | Explore agent 本地源码扫描（`ReferenceFramework/BigCat/`） |
| TEngine 深度 | Explore agent 本地源码扫描（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/`） + `V2-direction-pivot.md` §2.3 |
| HybridCLR 现状 | https://www.hybridclr.cn/en/docs/intro |
| UniTask vs Awaitable | https://github.com/Cysharp/UniTask + Unity 6 Awaitable docs |
| YooAsset 现状 | https://www.yooasset.com/en-US/docs/FAQ |
| Luban 现状 | https://github.com/focus-creative-games/luban + https://www.datable.cn/en/docs/intro |
| MemoryPack 现状 | https://github.com/Cysharp/MemoryPack |
| Source Generator + AOT | Unity Manual: Roslyn analyzers + VContainer Source Generator |
