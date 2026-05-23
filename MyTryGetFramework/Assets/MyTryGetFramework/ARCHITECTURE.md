# MyTryGetFramework V0.3 — 架构骨架

> V0.2 落地 ModuleHost 基础设施 + Common 三件套，V0.3 拆 World 为 EntityWorld（IModule）。
> 原 V0.1 内容见 git 历史。

## 程序集布局（V0.3）

```
Assets/MyTryGetFramework/
├── Runtime/
│   ├── Core/                              # 纯 C# 核心 (noEngineReferences: true)
│   │   ├── MyTryGetFramework.Core.asmdef
│   │   ├── Module/                        # V0.2：框架根 + 服务定位 + 生命周期
│   │   │   ├── IModule.cs                 # Module 契约（Priority / DependsOn / OnInit / Shutdown）
│   │   │   ├── IUpdateModule.cs           # 帧 Update 接口
│   │   │   ├── ILateUpdateModule.cs       # 帧 LateUpdate 接口
│   │   │   ├── IEventBus.cs               # 全局事件总线接口
│   │   │   ├── IModuleHost.cs             # 框架根接口
│   │   │   ├── ModuleHost.cs              # 默认实现（Kahn 拓扑排序 + 错误路径加固）
│   │   │   └── ModuleExceptions.cs        # 类型化异常（5 种）
│   │   ├── Common/                        # V0.2：跨端 Common Module
│   │   │   ├── ILogModule.cs / ConsoleLogModule.cs / LogLevel.cs
│   │   │   ├── ITimerModule.cs / TimerModule.cs / TimerHandle.cs
│   │   │   └── IPoolModule.cs / PoolModule.cs / IObjectPool.cs
│   │   ├── Entity/                        # V0.3：玩法层根 + Entity 体系
│   │   │   ├── IEntityWorld.cs            # 玩法层根接口（IModule + IUpdateModule）
│   │   │   ├── EntityWorld.cs             # 替代 V0.1 World（Priority=-100）
│   │   │   ├── Entity.cs                  # 组合宿主（Aspect + Tag + Ownership）
│   │   │   ├── EntityId.cs / Handle.cs / Tag.cs / Phase.cs / Aspect.cs
│   │   ├── SystemBase.cs                  # System 基类
│   │   ├── SystemGroup.cs                 # Phase 内的弱分组
│   │   ├── Query.cs                       # All-of + None-of 过滤器
│   │   ├── EntityEventDispatcher.cs       # Entity 级事件实现
│   │   ├── IEntityEventDispatcher.cs
│   │   ├── IWorldEventBus.cs              # V0.1 兼容别名（V0.4 计划合并到 IEventBus）
│   │   └── WorldEventBus.cs               # WorldEventBus 默认实现
│   └── Unity/                             # Unity Adapter (noEngineReferences: false)
│       ├── MyTryGetFramework.Unity.asmdef
│       └── WorldProxy.cs                  # MonoBehaviour 入口（持有 EntityWorld）
└── Tests/
    └── EditMode/                          # 纯 C# 测试 (noEngineReferences: true)
        ├── MyTryGetFramework.Tests.asmdef
        ├── ModuleHostIteration0Tests.cs   # 12 用例
        ├── ModuleHostIteration1Tests.cs   # 14 用例
        ├── ModuleHostIteration2Tests.cs   # 8 用例
        ├── ModuleHostErrorPathTests.cs    # 11 用例
        ├── ModuleHostEndToEndTests.cs     # 2 端到端
        ├── LogModuleTests.cs              # 14 用例
        ├── TimerModuleTests.cs            # 24 用例
        ├── PoolModuleTests.cs             # 21 用例
        ├── WorldLifecycleTests.cs / EntityAspectTests.cs / TagTests.cs
        ├── QueryTests.cs / SystemSchedulingTests.cs / OwnershipTests.cs
        └── EventTests.cs                  # V0.1 测试已迁移到 EntityWorld
```

## 双端门（V0.2 落地）

```
ServerProject/MyTryGetFramework.Core/
└── MyTryGetFramework.Core.csproj          # netstandard2.1，反向引用 Core/**/*.cs
```

`dotnet build` 验证 Core 不含 UnityEngine。CI 守好"双端可用"承诺。

## 核心设计决策（ADR 索引）

| ADR | 决策 | Status |
|-----|------|--------|
| 0001-0005 | V0.1 EC + Ownership + Aspect | V0.3 继续有效 |
| 0006 | SystemGroup 纯调度 | Superseded by ADR-0011 |
| 0007 | Aspect 隔离纪律（禁止访问其他 Aspect/Entity/World） | V0.3 继续有效 |
| 0008 | V0.1 System 注册 | Superseded by ADR-0011 |
| 0009 | Query All-of + None-of；Any-of 推迟 | V0.3 继续有效 |
| 0010 | Entity-level + World-level Event；Cross-System 推迟 | V0.4 计划合并到 IEventBus |
| 0011 | ModuleHost + IModule 契约（V2 主体） | V0.2 落地 |
| 0012 | Shadow csproj 双端编译 | V0.2 落地 |
| 0013 | 保留 Aspect 隔离纪律到 V2 | V0.2 重申 |
| 0014 | Network/HotReload Adapter 抽象 | V0.5 计划 |

## 依赖方向

```
Tests → Core ← Unity
       (Core 不依赖 Unity)
       (Core 通过 Shadow csproj 在 .NET 8+ 编过)
```

## ModuleHost 使用模式

```csharp
var host = new ModuleHost();
host.Register<ILogModule>(new ConsoleLogModule());
host.Register<ITimerModule>(new TimerModule());
host.Register<IPoolModule>(new PoolModule());
host.Register<IEntityWorld>(new EntityWorld("Game"));
host.Register<IGameLoopModule>(new MyGameLoop());

host.Initialize();           // 拓扑序 OnInit
host.Update(dt, unscaledDt); // 拓扑序 Update（IUpdateModule）
host.LateUpdate(dt, ud);     // 拓扑序 LateUpdate（ILateUpdateModule）
host.Shutdown();             // 逆序 Shutdown
```

## V0.1 直接 API（兼容路径）

```csharp
// 不挂 ModuleHost 时仍可直接驱动 EntityWorld
var world = new EntityWorld("Test");
world.RegisterSystem(new MySystem(), Phase.Update);
world.Start();
world.Update(0.016f, 0.016f);
world.Shutdown();
```

## 运行测试

Unity Editor: Window → General → Test Runner → EditMode → Run All

CLI: `Unity -batchmode -runTests -testPlatform EditMode -projectPath ./MyTryGetFramework`

跨端编译验证：`cd ServerProject/MyTryGetFramework.Core && dotnet build`

## 下一步（V0.3 路线图）

按 `.scratch/framework-design-v2/design.md` §12 + V0.3 迭代 0 review 建议：
1. **Aspect 反射缓存**（透明优化，10000 Entity 性能改善）
2. **BitArray256 类型索引**（Query.Matches 位运算加速）
3. **ResourceModule + LubanConfigModule**
4. **FSM + Procedure 模块**
5. **完整 demo**（登录 Procedure + 主菜单 UI）
