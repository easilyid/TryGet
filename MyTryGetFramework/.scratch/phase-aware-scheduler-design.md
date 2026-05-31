# Phase-aware TGTaskScheduler 设计文档

> **设计日期**：2026/05/31  
> **状态**：设计阶段  
> **参考框架**：BigCat CoroutineMgr、AlicizaX ServiceScope  
> **目标**：让 TGTaskScheduler 支持在指定 FramePhase 恢复异步操作

---

## 1. 核心设计思想（来自 BigCat）

### 1.1 BigCat 的 Phase 队列设计

**关键洞察**：
1. **每个 Phase 独立队列**：BigCat 为每个 GameLoopPhase 维护独立的优先队列
2. **三种时间类型**：Time（缩放时间）、UnscaledTime（非缩放时间）、FrameCount（帧数）
3. **队列映射**：通过 `GetQueue(TimingType, GameLoopPhase)` 获取对应队列
4. **外部驱动**：`Update(GameLoopPhase phase)` 由外部调用，每个 Phase 处理对应队列

**BigCat 队列组织**：
```csharp
// 10 个 Phase × 3 种时间类型 = 30 个队列
private readonly BetterIndexedPriorityQueue<PromiseTask> unscaledQueue0;  // BeginOfFrame
private readonly BetterIndexedPriorityQueue<PromiseTask> unscaledQueue1;  // EarlyUpdate
// ...
private readonly BetterIndexedPriorityQueue<PromiseTask> timeQueue1;      // EarlyUpdate
// ...
private readonly BetterIndexedPriorityQueue<PromiseTask> frameQueue1;     // EarlyUpdate
```

**Update 实现**：
```csharp
public void Update(GameLoopPhase phase) {
    switch (phase) {
        case GameLoopPhase.EarlyUpdate:
            if (unscaledQueue1 != null && unscaledQueue1.Count > 0)
                UpdateTimeQueue(unscaledQueue1, gTime.UnscaledTime, gTime.FrameCount);
            if (timeQueue1 != null && timeQueue1.Count > 0)
                UpdateTimeQueue(timeQueue1, gTime.Time, gTime.FrameCount);
            if (frameQueue1 != null && frameQueue1.Count > 0)
                UpdateFrameQueue(frameQueue1, gTime.FrameCount);
            break;
        // ...
    }
}
```

---

## 2. TryGet 的简化设计

### 2.1 设计约束

1. **只支持 5 个 Phase**：EarlyUpdate, FixedUpdate, Update, LateUpdate, EndOfFrame
2. **只支持 1 种时间类型**：Scaled Time（与当前 TGTaskScheduler 一致）
3. **保持 API 兼容性**：默认 Phase = Update
4. **纯 C# 可测试**：不依赖 Unity

### 2.2 队列组织

**简化方案**：5 个 Phase × 3 种队列类型（Yield/Delay/FrameWait）= 15 个队列

```csharp
// Yield 队列（双 buffer）
private Dictionary<FramePhase, YieldQueues> _yieldQueuesByPhase;

private class YieldQueues {
    public List<TGTaskCompletionSource> ThisFrame;
    public List<TGTaskCompletionSource> NextFrame;
}

// Delay 队列（按到期时间）
private Dictionary<FramePhase, List<DelayedEntry>> _delayQueuesByPhase;

// FrameWait 队列（按到期帧数）
private Dictionary<FramePhase, List<FrameEntry>> _frameWaitQueuesByPhase;
```

---

## 3. API 设计

### 3.1 新增方法

```csharp
public interface ITGTaskScheduler : IModule {
    // 现有方法（保持兼容）
    TGTask Yield();
    TGTask Delay(float seconds);
    TGTask WaitForFrames(int frameCount);
    
    // 新增：指定 Phase 的 Yield
    TGTask Yield(FramePhase phase);
    
    // 新增：等到指定 Phase
    TGTask DelayUntilPhase(FramePhase phase);
    
    // 新增：在指定 Phase 延迟指定秒数
    TGTask Delay(float seconds, FramePhase phase);
    
    // 新增：在指定 Phase 等待指定帧数
    TGTask WaitForFrames(int frameCount, FramePhase phase);
}
```

### 3.2 使用示例

```csharp
// 示例 1：在 LateUpdate 后执行
await scheduler.Yield(FramePhase.LateUpdate);
Debug.Log("This runs in LateUpdate");

// 示例 2：等到 EndOfFrame
await scheduler.DelayUntilPhase(FramePhase.EndOfFrame);
TakeScreenshot();

// 示例 3：在 FixedUpdate 中延迟 1 秒
await scheduler.Delay(1.0f, FramePhase.FixedUpdate);
ApplyPhysicsForce();

// 示例 4：在 EarlyUpdate 中等待 10 帧
await scheduler.WaitForFrames(10, FramePhase.EarlyUpdate);
```

---

## 4. 实现策略

### 4.1 实现所有 5 个 Update 接口

```csharp
public sealed class TGTaskScheduler : ITGTaskScheduler,
    IEarlyUpdateModule,
    IFixedUpdateModule,
    IUpdateModule,
    ILateUpdateModule,
    IEndOfFrameModule
{
    public void EarlyUpdate(float deltaTime, float unscaledDeltaTime) 
        => ProcessPhase(FramePhase.EarlyUpdate, deltaTime);
    
    public void FixedUpdate(float deltaTime, float unscaledDeltaTime) 
        => ProcessPhase(FramePhase.FixedUpdate, deltaTime);
    
    public void Update(float deltaTime, float unscaledDeltaTime) 
        => ProcessPhase(FramePhase.Update, deltaTime);
    
    public void LateUpdate(float deltaTime, float unscaledDeltaTime) 
        => ProcessPhase(FramePhase.LateUpdate, deltaTime);
    
    public void EndOfFrame(float deltaTime, float unscaledDeltaTime) 
        => ProcessPhase(FramePhase.EndOfFrame, deltaTime);
}
```

### 4.2 ProcessPhase 统一处理

```csharp
private void ProcessPhase(FramePhase phase, float deltaTime) {
    _frameCount++;
    _elapsedTime += deltaTime;
    
    // 1. 处理 Yield 队列
    ProcessYieldQueue(phase);
    
    // 2. 处理 Delay 队列
    ProcessDelayQueue(phase);
    
    // 3. 处理 FrameWait 队列
    ProcessFrameWaitQueue(phase);
}
```

### 4.3 跨 Phase 调度

**问题**：如果在 EarlyUpdate 中调用 `Yield(FramePhase.LateUpdate)`，如何处理？

**方案**：
1. **立即加入目标 Phase 的 NextFrame 队列**
2. **当前帧的 LateUpdate 会处理它**
3. **如果目标 Phase 已过（如在 LateUpdate 中 Yield(EarlyUpdate)），则等到下一帧的 EarlyUpdate**

```csharp
public TGTask Yield(FramePhase phase = FramePhase.Update) {
    var tcs = new TGTaskCompletionSource();
    var queues = _yieldQueuesByPhase[phase];
    queues.NextFrame.Add(tcs);  // 总是加入 NextFrame
    return tcs.Task;
}
```

---

## 5. 数据结构设计

### 5.1 完整字段定义

```csharp
public sealed class TGTaskScheduler : ITGTaskScheduler, ... {
    // 全局时间和帧计数
    private long _frameCount;
    private float _elapsedTime;
    
    // Phase 队列
    private readonly Dictionary<FramePhase, YieldQueues> _yieldQueuesByPhase;
    private readonly Dictionary<FramePhase, List<DelayedEntry>> _delayQueuesByPhase;
    private readonly Dictionary<FramePhase, List<FrameEntry>> _frameWaitQueuesByPhase;
    
    // 内部类
    private class YieldQueues {
        public List<TGTaskCompletionSource> ThisFrame = new();
        public List<TGTaskCompletionSource> NextFrame = new();
    }
    
    private readonly struct DelayedEntry {
        public readonly TGTaskCompletionSource Tcs;
        public readonly float DueTime;
        public DelayedEntry(TGTaskCompletionSource tcs, float dueTime) {
            Tcs = tcs; DueTime = dueTime;
        }
    }
    
    private readonly struct FrameEntry {
        public readonly TGTaskCompletionSource Tcs;
        public readonly long DueFrame;
        public FrameEntry(TGTaskCompletionSource tcs, long dueFrame) {
            Tcs = tcs; DueFrame = dueFrame;
        }
    }
}
```

### 5.2 初始化

```csharp
public TGTaskScheduler() {
    _yieldQueuesByPhase = new Dictionary<FramePhase, YieldQueues>();
    _delayQueuesByPhase = new Dictionary<FramePhase, List<DelayedEntry>>();
    _frameWaitQueuesByPhase = new Dictionary<FramePhase, List<FrameEntry>>();
    
    foreach (FramePhase phase in Enum.GetValues(typeof(FramePhase))) {
        _yieldQueuesByPhase[phase] = new YieldQueues();
        _delayQueuesByPhase[phase] = new List<DelayedEntry>();
        _frameWaitQueuesByPhase[phase] = new List<FrameEntry>();
    }
}
```

---

## 6. 测试用例

### 6.1 基础 Phase 测试

```csharp
[Test]
public void Yield_InLateUpdate_RunsInLateUpdate() {
    var scheduler = new TGTaskScheduler();
    var host = new ModuleHost();
    host.Register<ITGTaskScheduler>(scheduler);
    host.Initialize();
    
    bool executed = false;
    
    async TGTask TestAsync() {
        await scheduler.Yield(FramePhase.LateUpdate);
        executed = true;
    }
    
    TestAsync().Forget();
    
    // EarlyUpdate, FixedUpdate, Update 都不应执行
    host.EarlyUpdate(0.016f, 0.016f);
    Assert.IsFalse(executed);
    
    host.FixedUpdate(0.016f, 0.016f);
    Assert.IsFalse(executed);
    
    host.Update(0.016f, 0.016f);
    Assert.IsFalse(executed);
    
    // LateUpdate 应该执行
    host.LateUpdate(0.016f, 0.016f);
    Assert.IsTrue(executed);
}
```

### 6.2 跨 Phase 调度测试

```csharp
[Test]
public void Yield_FromEarlyToLate_RunsInSameFrame() {
    var scheduler = new TGTaskScheduler();
    var host = new ModuleHost();
    host.Register<ITGTaskScheduler>(scheduler);
    host.Initialize();
    
    int executionOrder = 0;
    int earlyOrder = 0, lateOrder = 0;
    
    async TGTask EarlyAsync() {
        earlyOrder = ++executionOrder;
        await scheduler.Yield(FramePhase.LateUpdate);
        lateOrder = ++executionOrder;
    }
    
    EarlyAsync().Forget();
    
    host.EarlyUpdate(0.016f, 0.016f);
    Assert.AreEqual(1, earlyOrder);
    Assert.AreEqual(0, lateOrder);  // 还未执行
    
    host.Update(0.016f, 0.016f);
    Assert.AreEqual(0, lateOrder);  // 还未执行
    
    host.LateUpdate(0.016f, 0.016f);
    Assert.AreEqual(2, lateOrder);  // 在 LateUpdate 执行
}
```

### 6.3 DelayUntilPhase 测试

```csharp
[Test]
public void DelayUntilPhase_WaitsForTargetPhase() {
    var scheduler = new TGTaskScheduler();
    var host = new ModuleHost();
    host.Register<ITGTaskScheduler>(scheduler);
    host.Initialize();
    
    bool executed = false;
    
    async TGTask TestAsync() {
        await scheduler.DelayUntilPhase(FramePhase.EndOfFrame);
        executed = true;
    }
    
    TestAsync().Forget();
    
    // 前面所有 Phase 都不应执行
    host.EarlyUpdate(0.016f, 0.016f);
    host.FixedUpdate(0.016f, 0.016f);
    host.Update(0.016f, 0.016f);
    host.LateUpdate(0.016f, 0.016f);
    Assert.IsFalse(executed);
    
    // EndOfFrame 应该执行
    host.EndOfFrame(0.016f, 0.016f);
    Assert.IsTrue(executed);
}
```

---

## 7. 迁移指南

### 7.1 现有代码兼容性

**✅ 完全兼容**：所有现有代码无需修改

```csharp
// 现有代码（默认在 Update 阶段）
await scheduler.Yield();
await scheduler.Delay(1.0f);
await scheduler.WaitForFrames(10);

// 等价于
await scheduler.Yield(FramePhase.Update);
await scheduler.Delay(1.0f, FramePhase.Update);
await scheduler.WaitForFrames(10, FramePhase.Update);
```

### 7.2 新代码推荐

```csharp
// 物理相关逻辑：在 FixedUpdate
await scheduler.Yield(FramePhase.FixedUpdate);
ApplyForce();

// 相机跟随：在 LateUpdate
await scheduler.Yield(FramePhase.LateUpdate);
UpdateCameraPosition();

// 截图：在 EndOfFrame
await scheduler.DelayUntilPhase(FramePhase.EndOfFrame);
TakeScreenshot();
```

---

## 8. 性能考量

### 8.1 内存开销

- **当前**：3 个队列（Yield × 2 + Delay + FrameWait）
- **Phase-aware**：15 个队列（5 Phase × 3 类型）
- **增量**：12 个队列，每个队列初始容量 ~16 个元素
- **估算**：额外 ~2KB 内存（可接受）

### 8.2 CPU 开销

- **当前**：每帧处理 1 个 Phase（Update）
- **Phase-aware**：每帧处理 5 个 Phase
- **优化**：空队列跳过（Count == 0）
- **估算**：额外 ~5% CPU（可接受）

---

## 9. 实施计划

### 9.1 第一步：重构现有代码（保持功能不变）

1. 将现有队列改为 Dictionary 组织
2. 实现 ProcessPhase 统一处理
3. 验证所有现有测试通过

### 9.2 第二步：实现 Phase-aware API

1. 添加新方法签名
2. 实现跨 Phase 调度逻辑
3. 添加新测试用例

### 9.3 第三步：实现所有 Update 接口

1. 实现 IEarlyUpdateModule, IFixedUpdateModule, ILateUpdateModule, IEndOfFrameModule
2. 验证 ModuleHost 正确调用所有 Phase
3. 性能测试

---

## 10. 风险与缓解

### 10.1 风险：跨 Phase 调度语义不清晰

**缓解**：
- 文档明确说明：Yield(Phase) 总是等到"下一次该 Phase 执行"
- 如果当前帧该 Phase 未执行，则在当前帧执行
- 如果当前帧该 Phase 已执行，则在下一帧执行

### 10.2 风险：性能回归

**缓解**：
- 实施前做基准测试
- 实施后对比性能
- 如果回归 > 10%，考虑优化或回滚

### 10.3 风险：测试覆盖不足

**缓解**：
- 至少 20 个测试用例覆盖所有 Phase 组合
- 包含边界情况（空队列、大量任务、跨帧调度）

---

**设计完成时间**：2026/05/31  
**下一步**：等待 Workflow 分析结果，然后开始实施
