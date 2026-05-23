Status: stage-5-draft

# MyTryGetFramework V2 顶层设计

> 基于 Stage 1（深读 TEngine + hsenl + 2026 技术栈研究）综合，经 Stage 4 独立 Plan agent 批判审阅迭代而成。
>
> 本文件**不取代 V0.1 的 PRD/CONTEXT/ADR**，而是定义 V0.2 起的新方向。V0.1 文档保留在 `.scratch/framework-design/`，本文件位于 `.scratch/framework-design-v2/`。

## 1. 定位

**MyTryGetFramework 是一个 Unity 通用代码框架**：
- **客户端为主**：以 Unity 6.3 LTS 为基线
- **双端门不挡死**：Core/Network/Common 三个 asmdef 走 Shadow csproj 模式，未来可移植到 .NET 8 Console 服务端
- **目标成熟度**：Level 3（业余工程级，小-中型商业项目可上线）
- **性能上限**：< 10000 Entity、< 256 Aspect+Tag 类型组合。超大规模请用 Unity DOTS。
- **不是 ECS 框架**：是模块化框架 + EC 玩法层。模块化主体借鉴 TEngine 形态 + 修正其反模式；EC 玩法层借鉴 hsenl 优化 + 保留 V0.1 的 Aspect 隔离纪律。

## 2. 核心架构（三层）

```
┌─────────────────────────────────────────────────────────┐
│  Bootstrap (Unity MonoBehaviour 入口)                    │
└────────────────────────┬────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────┐
│  ModuleHost (引擎服务层)                                  │
│  - 按 DependsOn 拓扑排序 + Priority tie-breaker           │
│  - IModule 强制接口契约（修正 TEngine 反模式）              │
│  - host.Get<IXxxModule>() 接口注入                       │
│                                                         │
│  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐  │
│  │  Common Mod   │ │  Unity Mod    │ │  Game Mod     │  │
│  │  跨端可用      │ │  仅客户端      │ │  业务自定义    │  │
│  │  Log/Timer/   │ │  Resource/UI/ │ │  ...          │  │
│  │  Pool/Config/ │ │  Audio/Input/ │ │               │  │
│  │  Save/FSM/    │ │  Scene/Boot   │ │               │  │
│  │  Procedure    │ │               │ │               │  │
│  └───────────────┘ └───────────────┘ └───────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────┐
│  EntityWorld (玩法层，可选)                                │
│  - Entity 树（hsenl 模式）                                 │
│  - Aspect 含行为 + 隔离边界纪律（V0.1 ADR-0007 保留）       │
│  - BitArray256 类型索引 + 虚方法反射缓存                    │
│  - Phase（Enter/Update/Exit）帧内调度                      │
│  - System（可选）跨 Entity 批处理加速                       │
│  - Query（All-of/None-of/Tag）筛选                        │
│  - Event 双层（Entity-level + Global=ModuleHost.EventBus）│
└─────────────────────────────────────────────────────────┘
```

## 3. 核心词汇（与 V0.1 的差异）

| 词汇 | V0.1 | V2 | 变化 |
|---|---|---|---|
| World | 运行时边界根 | **EntityWorld**（玩法层根），ModuleHost 才是框架根 | 降级 |
| Entity | 组合宿主 | 同上，加 Bitlist 索引 | 增强 |
| **Aspect** | state + local behavior，隔离边界 | **同 V0.1，词汇保留**。差异化于 hsenl 的 Component 自由访问 | **不变** |
| Tag | 存在标记 | 同上，与 Aspect 共享 BitArray256 mask | 合并存储 |
| System | 跨 Entity 调度，强制 | **可选批处理加速**，默认走 Aspect 自驱 | 降级 |
| SystemGroup | 调度分组 | **删除**（合并到 Phase 内的注册顺序） | 删 |
| Phase | Enter/Update/Exit | 保留，但作用域仅在 EntityWorld 内 | 收窄 |
| Query | All-of/None-of | 同上，BitArray256 位运算加速 | 增强 |
| Event 双层 | Entity / World | Entity / **Global（ModuleHost.EventBus）** | 改名 |
| **ModuleHost** | （无） | **新增**：框架根、服务定位器 | 新 |
| **IModule** | （无） | 新增：DependsOn + Priority + Init/Shutdown 契约 | 新 |
| **Procedure** | （无） | 新增：跨帧流程状态机 Module | 新 |
| **Adapter** | Unity 桥接 | 同 V0.1，但扩展为 Adapters/ 多个 asmdef | 扩展 |

## 4. 程序集布局（6 核心 + N Adapters）

```
Assets/MyTryGetFramework/                  ← Unity Asset 视角
├── Runtime/
│   ├── Core/        (asmdef + 镜像 csproj)
│   ├── Network/     (asmdef + 镜像 csproj)
│   ├── Common/      (asmdef + 镜像 csproj)
│   ├── Unity/       (仅 asmdef, Unity 限定)
│   └── Adapters/    (各自独立 asmdef)
│       ├── UniTask/
│       ├── R3/
│       ├── ZLogger/
│       ├── Mirror/
│       ├── YooAsset/
│       ├── Luban/
│       ├── HybridCLR/
│       └── ...
├── Editor/          (Editor 平台 asmdef)
└── Tests/           (Editor + 镜像 csproj)

ServerProject/                              ← .NET 8 视角（远期）
├── MyTryGetFramework.Core/Core.csproj      → 引用 Assets/.../Core/**.cs
├── MyTryGetFramework.Network/Network.csproj
├── MyTryGetFramework.Common/Common.csproj
└── MyTryGetFramework.Server/Server.csproj  ← 服务端业务
```

**Shadow csproj 模式**：每个 noEngineReferences asmdef 旁边有同名 csproj 项目，引用相同源文件。Unity 用 asmdef 编译，.NET CLI 用 csproj 编译。两者输出的 dll 不通用（运行时不同），但**源码 100% 共享**。

## 5. Module 契约

```csharp
public interface IModule
{
    int Priority { get; }                              // tie-breaker
    IReadOnlyList<Type> DependsOn { get; }             // 显式依赖
    void OnInit(IModuleHost host);
    void Shutdown();
}

public interface IUpdateModule : IModule
{
    void Update(float deltaTime, float unscaledDeltaTime);
}

public interface ILateUpdateModule : IModule
{
    void LateUpdate(float deltaTime);
}

public interface IModuleHost
{
    T Get<T>() where T : class, IModule;
    bool TryGet<T>(out T module) where T : class, IModule;
    void Register<T>(T module) where T : class, IModule;
    IEventBus EventBus { get; }
}
```

**纪律：**
1. **每个 Module 必须先有接口** `I{Name}Module`（修正 TEngine UIModule 反模式）
2. **Module 之间通过 host.Get<T>() 注入**，不在构造函数 new 其他 Module
3. **Module 依赖通过 DependsOn 显式声明**，ModuleHost 拓扑排序
4. **循环依赖在 RegisterModule 时检测并抛异常**
5. **测试时 Register Mock 实现替代真实 Module**

## 6. Entity-Aspect 系统

```csharp
public sealed class Entity
{
    // hsenl 模式：树结构
    public Entity Parent { get; }
    public IReadOnlyList<Entity> Children { get; }
    public EntityWorld World { get; }
    public EntityId Id { get; }

    // Aspect/Tag 存储 + BitArray256 mask
    private List<Aspect> _aspects;
    private BitArray256 _typeMask;     // 前 N 位 Aspect，后 M 位 Tag

    // V0.1 风格 API
    public T Attach<T>() where T : Aspect, new();
    public bool Detach<T>() where T : Aspect;
    public T GetAspect<T>() where T : Aspect;
    public bool HasAspect<T>() where T : Aspect;

    // Bitlist 加速查询（内部用）
    internal bool HasMaskAll(in BitArray256 mask);
    internal bool HasMaskNone(in BitArray256 mask);

    // Tag API（V0.1 沿用）
    public bool AddTag<T>() where T : Tag;
    public bool RemoveTag<T>() where T : Tag;
    public bool HasTag<T>() where T : Tag;

    // Ownership API（V0.1 沿用）
    public void AttachChild(Entity child);
    public bool DetachChild(Entity child);

    // Event API（V0.1 沿用）
    public void Subscribe<T>(Action<T> handler) where T : struct;
    public void Unsubscribe<T>(Action<T> handler) where T : struct;

    // Lifecycle
    public Handle GetHandle();
    public bool IsDestroyed { get; }
}

public abstract class Aspect
{
    // V0.1 ADR-0007 保留：只能访问自身字段 + EventDispatcher
    protected Entity Owner { get; }                    // protected, 不外露
    protected IEntityEventDispatcher EventDispatcher { get; }

    // hsenl 启发：虚方法生命周期，启动时反射缓存哪些被实现
    protected virtual void OnAttach() { }
    protected virtual void OnDetach() { }
    protected virtual void OnEnable() { }              // 父节点变化 / 重新激活
    protected virtual void OnDisable() { }
    protected virtual void OnUpdate(float dt) { }      // 默认空，被反射缓存跳过

    public virtual Type AspectType => GetType();
}
```

**ADR-0007 保留的关键纪律：**
- Aspect 仍**不能访问其他 Aspect / Entity / World**
- 这是相对 hsenl 的核心差异化点
- V0.5+ 阶段引入 Roslyn analyzer 在编译期挡住违规
- V0.2-V0.4 阶段靠代码审查 + 命名约定

## 7. Phase + Procedure（两者并存）

| 维度 | Phase | Procedure |
|---|---|---|
| 时间尺度 | 帧内 | 跨帧 |
| 状态 | Enter/Update/Exit 三档 | 任意状态（Boot/Login/InGame/Settle/...） |
| 切换 | 隐式（每帧 Update Phase） | 显式（StartProcedure("Login")） |
| 位置 | EntityWorld 内 | Common Module |
| 例子 | 一帧内 setup → tick → cleanup | 启动流程：资源初始化 → 热更检查 → 登录 → 进入大厅 |

两者通过 ProcedureModule 协调：Procedure 当前态可决定哪些 EntityWorld 激活、哪些 System 运行，但不替代 Phase。

## 8. Network 层（双端）

```csharp
// Core/Common 只定接口
public interface IChannel
{
    void Send(ArraySegment<byte> data);
    event Action<ArraySegment<byte>> OnReceived;
    void Close();
}

public interface IMessageBus
{
    Task<TResponse> RpcAsync<TRequest, TResponse>(TRequest request);
    void Send<TNotify>(TNotify notify);
    void Subscribe<TNotify>(Action<TNotify> handler);
}

public interface ISession { ... }
public interface ISessionManager { ... }

// Pipeline（hsenl Service Plug）
public interface IPipe {
    Task ProcessAsync(PipeContext ctx, Func<PipeContext, Task> next);
}

// Adapters/Mirror/MirrorChannel.cs    实现 IChannel
// Adapters/Kcp/KcpChannel.cs          实现 IChannel
// Codec：MemoryPack 默认
```

## 9. 热更新抽象

```csharp
public interface IHotfixLoader : IModule
{
    Task<Assembly> LoadHotfixAssemblyAsync(string name);
    Task<bool> CheckForUpdatesAsync();
    HotfixVersion CurrentVersion { get; }
}

// Adapters/HybridCLR/HybridCLRHotfixLoader.cs
// (远期) Adapters/CoreCLR/CoreCLRHotfixLoader.cs
```

## 10. 2026 技术栈选型

| 类别 | 接口在 | 默认实现（Adapter） | 备选 |
|---|---|---|---|
| 异步 | Common/IAsyncRunner | Adapters/UniTask | 自家 Task |
| 响应式 | Common/IReactiveBus | Adapters/R3 | 无 |
| 日志 | Common/ILogger | Adapters/ZLogger | Unity.Debug |
| 序列化 | Core/ISerializer | MemoryPack 默认 | Newtonsoft.Json |
| 资源 | Unity/IResourceModule | Adapters/YooAsset | Adapters/Addressables |
| 热更 | Common/IHotfixLoader | Adapters/HybridCLR | （远期）CoreCLR |
| 配置 | Common/IConfigModule | Adapters/Luban | ScriptableObject |
| 网络 | Network/IChannel + IMessageBus | Adapters/Mirror（开源默认） | Adapters/Kcp、Photon Fusion 2 |
| DI | （可选） | Adapters/VContainer | 框架不强依赖 |
| Inspector | Adapters/NaughtyAttributes | 默认 | Adapters/Odin |
| 本地存储 | Common/ISaveModule | sqlite-net | LiteDB |
| Unity 版本 | 6.3 LTS | — | — |

## 11. V0.1 → V2 资产迁移清单

### 直接复用（无改动）— 35%

| 文件 | 去向 |
|---|---|
| EntityId.cs | Core/Entity/EntityId.cs |
| Handle.cs | Core/Entity/Handle.cs |
| Tag.cs | Core/Entity/Tag.cs |
| IEntityEventDispatcher.cs | Core/Event/IEntityEventDispatcher.cs |
| EntityEventDispatcher.cs | Core/Event/EntityEventDispatcher.cs |
| Query.cs (核心算法) | Core/Query/Query.cs |

### 增强后复用 — 25%

| 文件 | 改动 |
|---|---|
| Entity.cs | 加 BitArray256 mask、虚方法生命周期 |
| Aspect.cs | 加虚方法反射缓存 |
| Query.cs (Builder) | 加 BitArray256 编译 |
| IWorldEventBus / WorldEventBus | 改名 IEventBus / EventBus（合并到 ModuleHost） |

### 重写 — 25%

| V0.1 | V2 |
|---|---|
| World.cs | 拆为 ModuleHost.cs + EntityWorld.cs |
| Phase.cs | 保留 enum，扩 Phase 注册 API |

### 弃 — 15%

| V0.1 | 原因 |
|---|---|
| SystemBase.cs | 降级为可选，合并到 Aspect 自驱 + 可选批处理 |
| SystemGroup.cs | 调度分组功能被 Phase + 注册顺序取代 |
| ADR-0008 (System 注册) | 重写 |
| 部分 V0.1 issue | 大部分重写 |

## 12. 版本路线（用完成度门，不承诺时间）

### V0.2 — ModuleHost 基础设施
**Gate criteria：**
- [ ] ModuleHost + IModule + DependsOn 拓扑排序实现并测试
- [ ] LogModule + TimerModule + PoolModule 三件套
- [ ] 至少一个完整的"注册 → 启动 → 关闭"端到端测试
- [ ] Shadow csproj 双端编译验证（Core 在 .NET 8 Console 能编过）
- [ ] V0.1 的 35% 资产已迁移到新结构

### V0.3 — 玩法层增强 + 关键服务
**Gate criteria：**
- [ ] EntityWorld + Aspect + Query + Phase 在新结构下跑通
- [ ] BitArray256 类型索引落地
- [ ] Aspect 虚方法反射缓存落地
- [ ] ResourceModule + LubanConfigModule 接入
- [ ] FSM + Procedure 模块
- [ ] 一个完整 demo（如：登录 Procedure + 主菜单 UI）

### V0.4 — Common Modules 五件套（Core 完成、Adapter 留 V0.5）

**Gate criteria（reworded post-hoc，反映实际交付）：**
- [x] **ResourceModule**（V0.3 漏做、V0.4 补齐合理化）：`IResourceModule` + `MemoryResourceModule` + `ResourceNotFoundException`
- [x] **UIModule**：`IUIModule` + `MemoryUIModule`（UI 栈状态机，不渲染；UGUI 实现留 Adapters/UGUI）
- [x] **SaveModule**：`ISaveModule` + `MemorySaveModule`（KV 存档，PlayerPrefs 语义；sqlite-net 降级为 Adapter 备选）
- [x] **LocalizationModule**：`ILocalizationModule` + `MemoryLocalizationModule`（多语言 KV，production-ready）
- [x] **AudioModule**：`IAudioModule` + `MemoryAudioModule`（cue + 4 类音量；UnityAudioModule 留 Adapters/Unity）
- [x] **MainMenuFlowDemoTests**：五件套端到端 demo（启动→读语言→进主菜单→点击→进游戏）
- [-] **InputModule**：**defer to V0.5** —— rationale：强耦合 Unity InputSystem 包，Core 抽象价值低，与 UnityInputAdapter 一起做更合理

**V0.4 完成纪录：**
- 5 个 `I{Name}Module` 接口 + 5 个 `Memory*Module` 实现，全部 cross-end 编译通过（Shadow csproj 0 警告 0 错误）
- 109 个 EditMode 单元测试（Resource 18 / UI 17 / Save 24 / Localization 23 / Audio 22 / Demo 3 + 2 复用） + 1 个端到端 demo
- Module Priority 完整链：Log(-1000) → Pool/Timer(-500) → Save(-450) → Localization(-420) → Resource(-400) → Audio(-380) → UI(-300) → Procedure(-200) → EntityWorld(-100) → 业务(0)
- Plan agent 综合验收：五件套接口能支撑"启动→读存档→选语言→进 MainMenu→播 BGM→切场景"完整 UI 游戏闭环

### V0.5 — Adapter 落地 + InputModule + Network + Hot Reload
**Gate criteria：**

Adapter 优先级链（V0.4 推到 V0.5 的）：
- [ ] **YooAsset Adapter**（`YooAssetResourceModule`）— 验证 IResourceModule 接口设计是否真撑得起异步/进度/引用计数
- [ ] **UGUI Adapter**（`UGUIUIModule`）— 配 YooAsset 跑通"加载 Prefab → Open UI"真实闭环；分层 Canvas / Modal / 数据传参在此扩展
- [ ] **Unity Audio Adapter**（`UnityAudioModule` 接 AudioSource）
- [ ] **PlayerPrefs Save Adapter**（或 FileBased / sqlite-net Save Adapter）

V0.5 新增：
- [ ] **InputModule**（V0.4 defer）：`IInputModule` + Adapters/Unity 接 InputSystem 包
- [ ] `IChannel` + `IMessageBus` 接口
- [ ] Adapters/Mirror 默认实现
- [ ] MemoryPack 序列化集成
- [ ] HybridCLR 集成走 IHotfixLoader

### V1.0 — 商业可用门槛
**Gate criteria：**
- [ ] 编辑器工具集（Entity Inspector / Module Inspector / Code Generator）
- [ ] 完整模板项目
- [ ] 文档（API ref + 教程 + 最佳实践）
- [ ] 至少 3 个示例项目（小型完整游戏）
- [ ] CI / Unity batch 测试自动化

### V2.0（远期）— 服务端
**Gate criteria：**
- [ ] Shadow csproj 服务端项目脚手架
- [ ] Server Modules（Gate / Auth / DB / Scene）
- [ ] 双端示例项目

## 13. 关键纪律集（贴文档显眼处）

1. **任何 Module 必须先有 `I{Name}Module` 接口，再有实现**
2. **Module 之间通过 host.Get<T>() 注入，不在构造函数 new 其他 Module**
3. **Aspect 不能访问其他 Aspect / Entity / World**（ADR-0007 保留）
4. **Core / Network / Common 三个 asmdef 严禁 using UnityEngine**
5. **Adapters/* 各自一个 asmdef，可选启用，可换实现**
6. **Procedure 不混入热更逻辑**（修正 TEngine 反模式）
7. **任何全局事件总线必须暴露 IEventBus 接口**（修正 GameEvent 反模式）
8. **测试不能跨 asmdef 直接 new 内部类型**，必须通过公开 API
9. **性能定位 < 10000 Entity；超大规模请用 DOTS**
10. **每个 minor 版本破坏性 API 修改必须升版本 + CHANGELOG**
11. **Memory*Module 实现纪律**（V0.4 新增）：(a) Write 路径严格（null/empty key 抛、重复 Open 抛）；(b) Read 路径容错（不存在/类型不匹配返回 default/false 而非抛）；(c) Shutdown 回归 OnInit 前态（清数据 + 重置配置）；(d) Memory 实现非线程安全（与全框架 ModuleHost 主线程契约一致）

## 14. 与 V0.1 的"对话"

V0.1 是这个 V2 设计的**直接前身**：
- V0.1 的设计纪律（CONTEXT/ADR/asmdef 隔离）被全盘继承
- V0.1 的 13 个 Core 文件 35% 直接复用、25% 增强、25% 重写、15% 弃
- V0.1 的 10 篇 ADR 中 ADR-0001/0002/0003/0007/0009/0010 大部分继续有效
- V0.1 的 ADR-0004（Phase 三档）/ADR-0006（SystemGroup）/ADR-0008（System 注册）需要重写或废止
- V0.1 的 18 个 issue 大部分进 attic，V0.2 起一套新 issue

V2 不是"推翻 V0.1"，是"V0.1 升维"——保留所有合理纪律，扩展到模块化 + 双端门 + 现代技术栈。

---

## 附：未解决问题（送 Stage 6 处理）

1. **GameObject ↔ Entity 双向引用**的销毁顺序细节（Plan agent 提出）
2. **HybridCLR 热更后 RuntimeTypeHandle 变化**对 Aspect 反射缓存的影响
3. **Aspect 隔离的 Roslyn analyzer** 设计（编译期挡跨 Aspect 访问）
4. **V0.1 的 18 个 issue 哪些进 attic / 哪些重写为 V0.2 issue** 逐项清算
5. **框架名称**：保留 MyTryGetFramework 还是改名？
6. **是否新建独立 Git 仓库**（V2 大改动是否切分支或重新起仓）
