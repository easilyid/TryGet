# ModuleHost + IModule 契约（V2 模块化主体）

## Status

Accepted

## Context

V0.1 以 `World` 作为运行时边界根，仅承担 EC 玩法层职责。V2 定位（见 `.scratch/framework-design-v2/design.md`）扩展为"Unity 通用代码框架"，需要承载 Log/Timer/Pool/Resource/UI/Audio/Input/Save/Config/Network/HotReload 等引擎服务，单一 `World` 不足以表达"框架根 + 服务定位 + 生命周期"。

参考框架对比：
- **TEngine ModuleSystem**：用 `GameModule.Get<T>()` 集中拿服务，方向正确，但反模式严重——`UIModule` 等具体类直接当门面、Module 之间靠构造函数 `new` 出对方、依赖隐式（靠注册顺序）、依赖反射猜接口。
- **hsenl**：没有模块系统，所有服务挂 World 单例上，跨端复用差。
- **Unity DOTS / Arch / Entitas**：纯 ECS，无引擎服务层概念。

V2 需要的是 TEngine 的形态（统一服务定位 + 生命周期托管），但要去掉其反模式：契约必须显式、依赖必须可见、测试必须可注入 Mock。

## Decision

引入 `ModuleHost` 作为框架根，所有引擎服务（含 V0.1 的 EntityWorld 调度）统一通过 `IModule` 契约组织。

**核心接口（详见 design.md §5）：**

```csharp
public interface IModule
{
    int Priority { get; }
    IReadOnlyList<Type> DependsOn { get; }
    void OnInit(IModuleHost host);
    void Shutdown();
}

public interface IUpdateModule : IModule { void Update(float dt, float unscaledDt); }
public interface ILateUpdateModule : IModule { void LateUpdate(float dt); }

public interface IModuleHost
{
    T Get<T>() where T : class, IModule;
    bool TryGet<T>(out T module) where T : class, IModule;
    void Register<T>(T module) where T : class, IModule;
    IEventBus EventBus { get; }
}
```

**强制纪律：**

1. **接口先行**：每个 Module 必须先有 `I{Name}Module` 接口，再有实现。`host.Get<UIModule>()` 这种直拿具体类的写法禁止（修正 TEngine `UIModule` 反模式）。
2. **接口注入**：Module 之间通过 `host.Get<IXxxModule>()` 取依赖，禁止在构造函数内 `new` 其他 Module。
3. **依赖显式**：依赖关系通过 `DependsOn` 返回 `Type[]` 声明，ModuleHost 在 `Register` 阶段做拓扑排序，`Priority` 仅作同层 tie-breaker。
4. **循环依赖立即抛**：拓扑排序检测到环，`Register` 阶段抛 `ModuleCircularDependencyException`，不允许跑起来再炸。
5. **生命周期成对**：启动按拓扑序调用 `OnInit`，关闭按逆拓扑序调用 `Shutdown`。
6. **测试可替换**：测试场景下 `host.Register<IFooModule>(new FakeFooModule())` 可以注入 Mock 替代真实 Module，无需启动 Unity。

**与 V0.1 的衔接：**

V0.1 的 `World` 被拆为 `ModuleHost`（框架根）+ `EntityWorld`（玩法层根，本身也是一个 Module）。`IWorldEventBus` 改名为 `IEventBus`，迁移到 `ModuleHost.EventBus`。Aspect 的隔离纪律不受影响（见 ADR-0007、ADR-0013）。

## Consequences

**好处：**
- 服务定位、依赖管理、生命周期编排集中到一处，业务代码不再到处单例。
- 接口契约 + 拓扑依赖，让 Module 替换、Mock、独立测试都成为常规操作，而不是"重写整个框架"。
- 修正了 TEngine `UIModule.Get()` / 隐式注册顺序两个广为传播的反模式，避免新人 copy-paste 时踩同样的坑。
- `ModuleHost` 是双端可用的纯 C# 概念，与 ADR-0012 的 Shadow csproj 配合可在 .NET Console 直接复用。

**成本：**
- 多一层抽象，"加一个新功能"的成本从"写个类"变成"先写接口再写实现再注册"。小项目体感偏重。
- 拓扑排序 + 依赖检测要写并测试，V0.2 的 Gate Criteria 不可省。
- `IModule` 接口签名一旦稳定，破坏性修改成本高，必须按"关键纪律 #10"走 minor 版本 + CHANGELOG。
- 与 V0.1 的部分文档（ADR-0008 System 注册）冲突，需配套重写或废止。