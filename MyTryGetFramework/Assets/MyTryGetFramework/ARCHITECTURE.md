# MyTryGetFramework V0.1 — 架构骨架

## 程序集布局

```
Assets/MyTryGetFramework/
├── Runtime/
│   ├── Core/                          # 纯 C# 核心 (noEngineReferences: true)
│   │   ├── MyTryGetFramework.Core.asmdef
│   │   ├── World.cs                   # 独立游戏世界实例
│   │   ├── Entity.cs                  # 组合宿主（Aspect + Tag + Ownership）
│   │   ├── EntityId.cs                # World 内唯一实体标识
│   │   ├── Handle.cs                  # 弱引用句柄
│   │   ├── Aspect.cs                  # 能力切片基类（state + local behavior）
│   │   ├── Tag.cs                     # 存在标记基类
│   │   ├── SystemBase.cs              # 跨 Entity 调度规则基类
│   │   ├── SystemGroup.cs             # 纯调度分组
│   │   ├── Phase.cs                   # 生命周期阶段 (Enter/Update/Exit)
│   │   ├── Query.cs                   # 组合过滤器 (All-of + None-of)
│   │   ├── IEntityEventDispatcher.cs  # Entity-level 事件发布接口
│   │   ├── IWorldEventBus.cs          # World-level 事件总线接口
│   │   ├── IWorldAdapter.cs           # Adapter 接口
│   │   ├── EntityEventDispatcher.cs   # Entity 事件实现 (internal)
│   │   └── WorldEventBus.cs           # World 事件实现 (internal)
│   └── Unity/                         # Unity Adapter (noEngineReferences: false)
│       ├── MyTryGetFramework.Unity.asmdef
│       └── WorldProxy.cs              # MonoBehaviour 入口
└── Tests/
    └── EditMode/                      # 纯 C# 测试 (noEngineReferences: true)
        ├── MyTryGetFramework.Tests.asmdef
        ├── WorldLifecycleTests.cs
        ├── EntityAspectTests.cs
        ├── TagTests.cs
        ├── QueryTests.cs
        ├── SystemSchedulingTests.cs
        ├── OwnershipTests.cs
        ├── EventTests.cs
        └── FakeAdapterTests.cs
```

## 核心设计决策

| ADR | 决策 |
|-----|------|
| 0007 | Aspect 行为限定为操作自身字段；禁止访问其他 Aspect/Entity/World |
| 0008 | System 显式注册到 World；执行顺序 = 注册顺序；可持有跨 Entity 状态 |
| 0009 | Query 支持 All-of + None-of；Any-of 推迟 |
| 0010 | V0.1 实现 Entity-level + World-level Event；Cross-System 推迟 |

## 依赖方向

```
Tests → Core ← Unity
       (Core 不依赖 Unity)
```

## 运行测试

在 Unity Editor 中：
1. Window → General → Test Runner
2. 选择 EditMode tab
3. Run All

或 CLI：
```bash
Unity -batchmode -runTests -testPlatform EditMode -projectPath ./MyTryGetFramework
```

## 下一步实现顺序

按 Issue 依赖图的关键路径：
1. **Issue-01**: World + Entity 创建 ✅ (骨架已包含)
2. **Issue-02**: EntityId 唯一性 ✅ (骨架已包含)
3. **Issue-04**: Aspect attach/detach ✅ (骨架已包含)
4. **Issue-05**: Aspect 唯一性约束 ✅ (骨架已包含)
5. **Issue-10**: Query by Aspect (All-of + None-of) ✅ (骨架已包含)
6. **Issue-13**: System over Query → 细化 Execute 语义
7. **Issue-18**: Fake Adapter ✅ (骨架已包含)

并行可做：
- Issue-03 (Entity destroy) ✅
- Issue-06 (Tag) ✅
- Issue-07/08 (Ownership + cascade) ✅
- Issue-09 (Handle) ✅
- Issue-15/16 (Event) ✅
