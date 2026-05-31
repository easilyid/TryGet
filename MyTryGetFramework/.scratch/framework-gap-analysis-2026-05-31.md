# TryGet 框架差距分析与迭代方向

> **分析日期**：2026/05/31  
> **当前版本**：V2.0 Route C 已落地（commit 0bfc33f）  
> **分析范围**：TryGet vs AlicizaX/DGame/TEngine/hsenl/BigCat  
> **目标**：明确架构差距、审计代码合理性、确定下一步迭代方向

---

## 执行摘要

### 当前状态
- **V2.0 已落地**：ModuleHost + SourceGenerator + 三轨自动注册 + 多阶段 Update（EarlyUpdate/FixedUpdate/Update/LateUpdate/EndOfFrame）
- **Core 规模**：~6445 个 C# 文件（包含测试和生成代码）
- **架构定位**：纯客户端 Unity 服务框架，Core 保持纯 C#（不引用 UnityEngine）

### 关键发现
1. **✅ 已提前实现 V2.2 FrameLoop**：ModuleHost 已支持 5 阶段 Update，比路线图提前
2. **⚠️ 缺失 Scope 管理**：AlicizaX 的 ServiceScope/ServiceWorld 三层作用域（App/Scene/Gameplay）未实现
3. **⚠️ TGTaskScheduler 未感知 Phase**：异步调度器只在 Update 阶段运行，无法按 Phase 调度
4. **⚠️ EventBus 有 GC 压力**：每次 Publish 都会装箱 struct event，高频场景有性能隐患
5. **✅ 代码质量良好**：ModuleHost 拓扑排序、错误回滚、生命周期管理设计合理

### 优先级建议
- **P0**：无（V2.2 FrameLoop 已提前实现）
- **P1**：C2 EventBus 零 GC 优化、C3 Phase-aware TGTaskScheduler
- **P2**：Scope 管理（需先明确 Unity 层接入方案）、C4 Procedure Transition Result

---

## 1. 架构差距分析

### 1.1 Scope 管理（AlicizaX ServiceScope/ServiceWorld）

**参考框架实现**：
AlicizaX 使用三层 Scope 架构：
```csharp
// ServiceWorld 管理三个 Scope：App（全局）、Scene（场景级）、Gameplay（玩法级）
ServiceWorld world = new ServiceWorld();
world.App.Register<ILogger>(new ConsoleLogger());           // 全局服务
world.EnsureScene().Register<ISceneLoader>(sceneLoader);    // 场景级服务
world.EnsureGameplay().Register<IBattle>(battleSystem);     // 玩法级服务

// 查询时优先从 preferredScope 查找，再回退到全局
var logger = world.Require<ILogger>(world.Gameplay);  // 先查 Gameplay，再查 Scene，最后查 App
```

**TryGet 当前状态**：
- ModuleHost 是单一全局容器，所有 Module 生命周期相同
- 无 Scope 概念，无法按场景/玩法卸载部分服务
- 所有 Module 在 Initialize 时创建，Shutdown 时全部销毁

**差距影响**：
- 场景切换时无法只卸载场景相关服务（如 SceneLoader、LevelData）
- 玩法结束时无法只清理战斗系统（如 BattleModule、SkillModule）
- 内存管理粒度粗，长时间运行会累积无用服务

**是否符合 ADR-0020**：✅ 符合（纯客户端场景管理，不涉及服务端）

**优先级**：P2（需先明确 Unity 层接入方案，当前 Core 无 Scene 概念）

**建议**：
1. 暂不实现完整 ServiceWorld，等 Unity Adapter 明确后再设计
2. 可先在文档中标注"未来 Scope 扩展点"
3. 当前 ModuleHost 保持单一全局容器，避免过早抽象

---

### 1.2 FrameLoop 多阶段 Update（已提前实现）

**参考框架实现**：
- BigCat UnityEventLoop：EarlyUpdate/Update/LateUpdate/EndOfFrame
- DGame ModuleSystem：只有 Update（单阶段）
- AlicizaX ServiceScope：Tick/LateTick/FixedTick/DrawGizmos

**TryGet 当前状态**：✅ **已实现**
```csharp
// ModuleHost.cs 已支持 5 个阶段
public void EarlyUpdate(float deltaTime, float unscaledDeltaTime) { ... }
public void FixedUpdate(float deltaTime, float unscaledDeltaTime) { ... }
public void Update(float deltaTime, float unscaledDeltaTime) { ... }
public void LateUpdate(float deltaTime, float unscaledDeltaTime) { ... }
public void EndOfFrame(float deltaTime, float unscaledDeltaTime) { ... }

// 对应接口：IEarlyUpdateModule, IFixedUpdateModule, IUpdateModule, ILateUpdateModule, IEndOfFrameModule
// 执行表缓存：_earlyUpdateModules, _fixedUpdateModules, _updateModules, _lateUpdateModules, _endOfFrameModules
```

**结论**：✅ **V2.2 FrameLoop 已提前实现**，比路线图计划提前完成

**遗留问题**：
1. FramePhase 枚举已定义但未被使用（只是文档性质）
2. TGTaskScheduler 只在 Update 阶段运行，无法按 Phase 调度（见 1.3）

---

### 1.3 Phase-aware TGTaskScheduler（C3 候选任务）

**参考框架实现**：
BigCat Timer 支持指定 Phase：
```csharp
// 可以指定 Timer 在哪个 Phase 触发
timer.Schedule(callback, delay, phase: FramePhase.LateUpdate);
```

**TryGet 当前状态**：
```csharp
// TGTaskScheduler 只实现了 IUpdateModule，只在 Update 阶段运行
public sealed class TGTaskScheduler : ITGTaskScheduler, IModule, IUpdateModule
{
    public void Update(float deltaTime, float unscaledDeltaTime) {
        // 所有 Yield/Delay/WaitForFrames 都在这里处理
    }
}
```

**差距影响**：
- 无法实现"在 LateUpdate 后执行"的异步逻辑
- 无法实现"在 FixedUpdate 中等待物理计算"
- 所有异步操作都在 Update 阶段完成，时序控制受限

**是否符合 ADR-0020**：✅ 符合（纯 C# 可测试）

**优先级**：P1（影响异步编程体验）

**建议**：
```csharp
// 扩展 ITGTaskScheduler 接口
public interface ITGTaskScheduler {
    TGTask Yield(FramePhase phase = FramePhase.Update);  // 指定在哪个阶段恢复
    TGTask DelayUntilPhase(FramePhase phase);            // 等到指定阶段
}

// TGTaskScheduler 实现所有 5 个 Update 接口
public sealed class TGTaskScheduler : 
    IEarlyUpdateModule, IFixedUpdateModule, IUpdateModule, ILateUpdateModule, IEndOfFrameModule
{
    // 每个阶段维护独立队列
}
```

---

### 1.4 EventBus 零 GC 优化（C2 候选任务）

**参考框架实现**：
TEngine GameEventMgr 使用 int eventId + object[] args 避免泛型装箱：
```csharp
// 不使用泛型 struct，避免装箱
GameEventMgr.SendEvent(EventId.PlayerDead, player, damage);
```

**TryGet 当前状态**：
```csharp
// EventBus.cs 使用泛型 struct，每次 Publish 都会装箱
public void Publish<T>(T evt) where T : struct {
    // Dictionary<Type, object> 存储时会装箱 HandlerList<T>
    if (!_handlers.TryGetValue(key, out var obj) || !(obj is HandlerList<T> list))
        return;
    list.Publish(evt);  // 这里 evt 本身不装箱，但 Dictionary 的 object 值会装箱
}
```

**差距影响**：
- 高频事件（如 OnDamage、OnMove）每帧触发会产生 GC
- Dictionary<Type, object> 的 boxing 无法避免
- 当前设计牺牲性能换取类型安全

**是否符合 ADR-0020**：✅ 符合（纯 C# 优化）

**优先级**：P1（影响运行时性能）

**建议方案**：
1. **方案 A**：使用 Dictionary<Type, IHandlerList> + 泛型接口避免装箱
2. **方案 B**：Source Generator 生成静态 EventBus<T>，每个事件类型独立容器
3. **方案 C**：保持当前设计，等性能测试证明是瓶颈再优化

推荐先做性能基准测试，再决定优化方案。

---

## 2. 代码审计结果

### 2.1 ModuleHost 设计质量：✅ 优秀

**优点**：
1. **拓扑排序正确**：DependsOn 驱动初始化顺序，Priority 作为 tie-breaker
2. **错误回滚机制**：Initialize 失败时倒序 Shutdown 已初始化部分
3. **Shutdown 容错**：单个 Module 失败不中断其他 Module，最终聚合异常
4. **执行表缓存**：Initialize 时构建 5 个 Update 执行表，运行时零分配
5. **防御性编程**：DependsOn 返回 null 视为空集合，循环依赖检测完整

**代码片段**（ModuleHost.cs:145-154）：
```csharp
catch
{
    // 失败回滚：倒序 Shutdown 已 OnInit 的部分
    for (int i = initializedInOrder.Count - 1; i >= 0; i--)
    {
        try { initializedInOrder[i].Shutdown(); }
        catch { /* 回滚阶段忽略二次异常 */ }
    }
    throw;
}
```

**建议**：保持当前设计，无需重构。

---

### 2.2 EventBus 设计质量：✅ 良好，有优化空间

**优点**：
1. **重入安全**：Publish 期间 Subscribe/Unsubscribe 延迟到 FlushPendingChanges
2. **异常隔离**：单个 handler 抛异常不影响其他 handler
3. **自动清理**：订阅数为 0 时自动移除 HandlerList

**问题**：
1. **GC 压力**：Dictionary<Type, object> 存储 HandlerList<T> 会装箱
2. **GetEventTypes() 每次分配新 List**：应缓存或返回 ReadOnlyCollection

**代码片段**（EventBus.cs:60-63）：
```csharp
public IReadOnlyList<Type> GetEventTypes()
{
    return new List<Type>(_handlers.Keys);  // ❌ 每次调用都分配新 List
}
```

**建议**：
1. 短期：将 GetEventTypes() 改为返回 `_handlers.Keys.ToArray()` 或缓存结果
2. 长期：实现零 GC EventBus（见 1.4）

---

### 2.3 TGTaskScheduler 设计质量：✅ 良好

**优点**：
1. **双 buffer Yield**：swap 机制保证"下一帧"语义
2. **对象池**：TGTaskCompletionSource 使用池化（TGTaskPool）
3. **Shutdown 清理**：取消所有未完成任务，避免 await 永久挂起

**问题**：
1. **只在 Update 运行**：无法支持 Phase-aware 调度（见 1.3）
2. **线性扫描 Delay 队列**：O(N) 复杂度，N > 100 时可能成为瓶颈

**建议**：
1. 短期：监控 _delayed.Count，如果实际场景 < 50，保持当前设计
2. 长期：如果 N > 100，改用 MinHeap 或 SortedList（O(log N) 插入/删除）

---

### 2.4 Procedure Stack 设计质量：✅ 良好

**优点**：
1. **Stack 语义清晰**：Push/Pop/Replace 符合 UI 导航直觉
2. **OnPause/OnResume**：支持暂停/恢复，适合叠层流程
3. **同步 API**：当前只支持同步 Procedure，避免异步复杂度

**问题**：
1. **缺少 Transition Result**：无法返回流程结果（如登录成功/失败）
2. **无异步 Procedure 支持**：IAsyncProcedure 接口存在但未集成

**建议**：
1. 实现 C4 Procedure Transition Result（P2）
2. 评估是否需要异步 Procedure（如资源加载流程）

---

## 3. 迭代方向建议

### 3.1 立即行动（本周）

#### ✅ 更新路线图文档
**任务**：修正 `docs/strategy/V2-roadmap-tasks.md` 中的 V2.2 状态
- 当前文档：V2.2 — ModuleHost 多阶段 Update（计划中）
- 实际状态：✅ 已在 V2.0 提前实现
- 行动：标注"V2.2 已提前实现于 V2.0"，调整后续版本号

#### ✅ 修复 EventBus.GetEventTypes() GC
**任务**：避免每次调用都分配新 List
```csharp
// 方案 1：返回数组（一次分配）
public IReadOnlyList<Type> GetEventTypes() => _handlers.Keys.ToArray();

// 方案 2：缓存结果（需要在 Subscribe/Unsubscribe 时失效）
private Type[] _cachedEventTypes;
private bool _eventTypesDirty = true;
```

**工作量**：1 小时
**风险**：低

---

### 3.2 短期迭代（2 周内）

#### P1-A：Phase-aware TGTaskScheduler（C3）
**目标**：支持在指定 FramePhase 恢复异步操作

**设计草案**：
```csharp
public interface ITGTaskScheduler {
    TGTask Yield(FramePhase phase = FramePhase.Update);
    TGTask DelayUntilPhase(FramePhase phase);
}

public sealed class TGTaskScheduler : 
    IEarlyUpdateModule, IFixedUpdateModule, IUpdateModule, 
    ILateUpdateModule, IEndOfFrameModule
{
    private Dictionary<FramePhase, List<TGTaskCompletionSource>> _yieldQueues;
    
    public void EarlyUpdate(...) => ProcessPhase(FramePhase.EarlyUpdate, ...);
    public void FixedUpdate(...) => ProcessPhase(FramePhase.FixedUpdate, ...);
    // ...
}
```

**验证标准**：
- EditMode 测试：await Yield(FramePhase.LateUpdate) 在 LateUpdate 后恢复
- 性能测试：Phase 调度开销 < 5%

**工作量**：3-5 天
**风险**：中（需要重构 TGTaskScheduler 内部队列）

---

#### P1-B：EventBus 零 GC 基准测试
**目标**：量化当前 EventBus 的 GC 压力

**测试场景**：
1. 每帧 Publish 100 次高频事件（如 OnDamage）
2. 运行 1000 帧，测量总 GC Alloc
3. 对比 TEngine GameEventMgr 的 GC 表现

**决策标准**：
- 如果 GC Alloc < 1MB/1000 帧：保持当前设计
- 如果 GC Alloc > 10MB/1000 帧：启动零 GC 重构

**工作量**：2 天
**风险**：低

---

### 3.3 中期迭代（1 个月内）

#### P2-A：Procedure Transition Result（C4）
**目标**：让 Procedure 可以返回结果

**设计草案**：
```csharp
public interface IProcedure<TResult> : IProcedure {
    TResult Result { get; }
}

public class LoginProcedure : IProcedure<LoginResult> {
    public LoginResult Result { get; private set; }
    
    public void OnEnter() {
        // 登录逻辑
        Result = new LoginResult { Success = true, UserId = 123 };
    }
}

// 使用
procedureModule.Push<LoginProcedure>();
// ... 等待 Pop
var result = procedureModule.GetResult<LoginProcedure, LoginResult>();
```

**工作量**：5-7 天
**风险**：中（需要设计结果存储和查询 API）

---

#### P2-B：Scope 管理设计文档
**目标**：为未来 Scope 扩展预留设计空间

**产物**：
1. `docs/design/scope-management-future.md`
2. 分析 AlicizaX ServiceWorld 的适用性
3. 定义 TryGet 的 Scope 边界（App/Scene/Gameplay 是否足够）
4. 明确 Unity Adapter 接入后的 Scope 创建时机

**工作量**：3 天（纯文档）
**风险**：低

---

### 3.4 长期规划（3 个月内）

#### 候选 C5：Generated Registry Diagnostics
**目标**：让 Source Generator 生成的注册表可诊断

**功能**：
- `AssemblyManifestRegistry.GetAllModules()` 返回所有已注册 Module
- `EventHandlerRegistry.GetHandlersForEvent<T>()` 返回事件的所有 handler
- 启动时检测重复注册、缺失依赖

**工作量**：7-10 天
**优先级**：P2

---

#### 候选 C6：Lightweight Pipeline
**目标**：支持线性业务流程（如启动链、登录链）

**设计参考**：hsenl ProcedureLine
```csharp
public interface IPipeline<TContext> {
    void AddStep(IPipelineStep<TContext> step);
    Task<TContext> Execute(TContext context);
}

// 使用
var loginPipeline = new Pipeline<LoginContext>();
loginPipeline.AddStep(new CheckNetworkStep());
loginPipeline.AddStep(new AuthenticateStep());
loginPipeline.AddStep(new LoadUserDataStep());
var result = await loginPipeline.Execute(new LoginContext());
```

**工作量**：10-14 天
**优先级**：P3（当前 Procedure Stack 可覆盖大部分场景）

---

## 4. 风险与约束

### 4.1 Unity 层接入未明确
**风险**：Scope 管理、MonoDriver 等设计依赖 Unity 层架构
**缓解**：优先实现纯 C# 可验证的功能（Phase-aware Scheduler、EventBus 优化）

### 4.2 性能优化过早
**风险**：EventBus 零 GC 重构可能引入复杂度，但实际场景未必需要
**缓解**：先做基准测试，证明瓶颈后再优化

### 4.3 路线图文档滞后
**风险**：V2.2 已提前实现但文档未更新，可能误导后续开发
**缓解**：立即更新路线图，标注实际状态

---

## 5. 结论与行动项

### 5.1 核心结论
1. ✅ **TryGet V2.0 架构质量良好**，ModuleHost/EventBus/TGTaskScheduler 设计合理
2. ✅ **V2.2 FrameLoop 已提前实现**，比计划提前完成
3. ⚠️ **两个 P1 优化点**：Phase-aware Scheduler、EventBus 零 GC
4. ⚠️ **Scope 管理需等 Unity 层明确**，暂不实施

### 5.2 立即行动（本周）
- [ ] 更新 `docs/strategy/V2-roadmap-tasks.md`，标注 V2.2 已完成
- [ ] 修复 `EventBus.GetEventTypes()` 的 GC 问题
- [ ] 将本分析报告归档到 `docs/analysis/`

### 5.3 下一个 Sprint（2 周）
- [ ] 实现 Phase-aware TGTaskScheduler（C3）
- [ ] EventBus 零 GC 基准测试
- [ ] 决策是否启动 EventBus 重构

### 5.4 推荐迭代顺序
1. **本周**：文档更新 + EventBus 小修复
2. **Sprint 1**：Phase-aware Scheduler（C3）
3. **Sprint 2**：EventBus 零 GC（C2，如果基准测试证明必要）
4. **Sprint 3**：Procedure Transition Result（C4）
5. **未来**：Scope 管理（等 Unity Adapter 明确）

---

**分析完成时间**：2026/05/31  
**下次复审**：实现 C3 后，或新增参考框架时
