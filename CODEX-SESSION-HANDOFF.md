# Codex TryGet 会话交接规划

日期：2026-06-14  
作者：Codex  
作用：本文件是新的 Codex 交接文档，用于区分并补充根目录 `SESSION-HANDOFF.md`。不要覆盖 Claude 的 `SESSION-HANDOFF.md`。

## 结论

可以进入 V2.3 UI，但不要按 Claude handoff 里的“直接创建 Core/UI 接口 M1”开工。

更稳的路线是先做一个很短的 V2.2.5 稳定化闸门，再进入 UI 纵切片：

1. 修 TimerModule 的重复计时器 callback 内自取消和自暂停语义，并补测试。
2. 重新跑 Unity EditMode，确认 `RegistryDiagnosticsTests` 当前失败是否已由现有改动修复。
3. 把 Unity host 从 `Samples/Unity/Entry/TryGetMonoEntry.cs` 产品化到 `Runtime/Unity`。
4. 定下 UI 异步打开/关闭的取消和 operation-version 语义。
5. 再进入 V2.3 UI：Core 契约 + Unity 适配 + 一个可运行 sample window + EditMode 测试。

判断依据：Core 底层整体成熟度已经足够承载 UI；真正会污染 UI 语义的缺口集中在 Timer 边界、Unity 入口位置、以及 UI 异步生命周期。

## 当前仓库状态

- 分支：`v2.0-route-c-landing`，本地 ahead origin 17。
- 工作区已存在大量未提交修改，包含 Core、Generator、Tests、Samples、文档等。不要回滚不属于当前任务的改动。
- 已有分析文档：`docs/strategy/MyTryGetFramework-code-quality-gap-analysis-2026-06-14.md`。
- 本文件是新增交接，不覆盖 Claude 的 `SESSION-HANDOFF.md`。

## 已验证结果

已通过：

```powershell
dotnet build ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj
dotnet test MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.Tests/MyTryGetFramework.SourceGenerator.Tests.csproj
dotnet run --project Samples/Net/TryGet.Samples.Net.csproj
```

结果：

- Core shadow csproj build 成功，0 warning，0 error。
- Source generator tests 14/14 通过。
- Net sample 跑通 Boot -> Login -> InGame，以及 source-gen module/event 示例。

Unity EditMode 当前不能直接宣称全绿：

- 最新保存结果：`MyTryGetFramework/TestLogResults/TestResults_20260614_123726.xml`
- 结果：412 total / 411 passed / 1 failed。
- 失败项：`RegistryDiagnosticsTests.ModuleRegistry_Snapshot_MetadataPlusLegacyDelegate_ReportsBoth`
- 失败信息：Expected 3, But was 2。
- 位置：`MyTryGetFramework/Assets/MyTryGetFramework/Tests/EditMode/RegistryDiagnosticsTests.cs:125`
- Unity Editor 当前有项目进程占用，之前无法安全重跑。不要直接杀 Unity，除非用户明确要求。

## MyTryGetFramework 源码判断

### Core 是可用地基

- `MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/MyTryGetFramework.Core.asmdef:13` 设置 `noEngineReferences: true`，Core 仍保持无 UnityEngine 依赖。
- `ModuleSystem` 已经具备工程化约束：
  - `Register<T>` 强制用 service interface，不允许 concrete type 注册。
  - 禁止用 `IEarlyUpdateModule`、`IUpdateModule` 等帧接口当 service key。
  - Initialize 有拓扑排序和失败回滚。
  - EarlyUpdate / FixedUpdate / Update / LateUpdate / EndOfFrame 分发表已落地。
- `EventModule` 已有 struct typed event bus、最大发布深度 32、dispatch 中 pending change、handler 异常隔离。
- `ProcedureModule` 的 Start / Push / Pop / Replace 已返回 `TGTask`，支持 async enter/exit transition，能承接 UI/scene 示例。
- `TGTask` 已不是烟雾弹：有自定义 async method builder、池化 body、version guard、`Forget` 未观察异常钩子、phase-aware Yield / Delay / WaitForFrames。

结论：不需要因为“底层没成熟”而无限推迟 UI。

### 需要先补的底层缺口

1. `TimerModule` 有真实边界 bug。

源码位置：`MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Timer/TimerModule.cs`

- `Cancel` 写回 `_entries[i]`：86-97。
- `Pause` 写回 `_entries[i]`：103-114。
- `Update` 对 repeating timer 先复制 local `entry`，callback 后在 217 行 `_entries[i] = entry`。

问题：repeating callback 如果对自身 handle 调用 `Cancel(handle)` 或 `Pause(handle)`，callback 内的写回可能被 Update 结束时的旧 local `entry` 覆盖。当前测试覆盖了触发后 cancel、触发后 pause、catch-up，但没有覆盖 callback 内自取消/自暂停。

建议优先级：P0/P1。UI 打开/关闭、隐藏延迟销毁、动画超时很容易用到 timer，先修更稳。

2. `Runtime/Unity` 还没有产品代码。

- `MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Unity` 当前 C# 文件数为 0。
- Unity host 现在在 sample：`MyTryGetFramework/Assets/MyTryGetFramework/Samples/Unity/Entry/TryGetMonoEntry.cs`。
- 该 sample 已经完整桥接 Awake、FixedUpdate、Update、LateUpdate、EndOfFrame、OnDestroy，但它不是包的正式 Unity adapter。

建议：先把 minimal host 放入 `Runtime/Unity`，sample 改为依赖正式 host。否则 UI 模块会被迫依赖 sample 层。

3. `TGTask` 缺少 UI 生命周期必需的公开组合语义。

当前已有 Delay/Yield/WaitForFrames 和取消态结果，但缺少公开的 cancellation token、timeout、WhenAll/WhenAny、owner-scope cancellation。V2.3 不一定要一次补全，但 UI open/close 必须先定义 operation-version 或 owner-cancel 策略，不能异步加载回来后无条件显示。

4. `PoolModule` DEBUG active tracking 有潜在误报。

DEBUG `_activeSet = new HashSet<T>()` 使用默认 equality。若池化 class override equality，active tracking 可能按值而不是引用去重。短期不阻塞 UI，但后续 GameObject pool 或 UI view pool 前建议改成 reference equality。

## ReferenceFramework 源码吸收点

### TEngine / DGame UI

应该吸收：

- Canvas 排序和子 Canvas 相对排序。
  - `ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIWindow.cs:87-138`
  - parent canvas `sortingOrder` 改变时，child canvas 用 `value + (childCanvas.sortingOrder - oldOrder)` 保持相对层级。
- 隐藏不直接依赖 `SetActive(false)`，而是切 layer 并禁用 GraphicRaycaster。
  - `ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIWindow.cs:143-215`
  - 这能保留对象生命周期和缓存状态，同时阻断输入。
- UI resource loader 抽象。
  - `ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/IUIResourceLoader.cs:11-32`
  - 同步/异步 `LoadGameObject`，异步带 `CancellationToken` 和 packageName。
- fullscreen window 影响下层可见性和层级。
  - `ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIModule.cs` 中有 prepare、fullscreen、depth sort 逻辑。

暂不吸收：

- `GameObject.Find("UIRoot")` 作为硬依赖。
- 直接静态全局 ModuleSystem。
- 完整 YooAsset/HybridCLR 加载链。V2.3 可以只定义资源加载接口和一个 `Resources.LoadAsync` 或 prefab provider 适配，V2.4 再接资源系统。

### AlicizaX UI

最值得吸收的是异步生命周期防护。

源码位置：`ReferenceFramework/AlicizaX/com.alicizax.unity.framework/Runtime/UI/Manager/UIService.Open.cs`

- LayerData 使用 layer array + typeId index：9-59。
- `ShowUIImplAsync` 在异步创建资源前记录 `operationVersion`，await 后校验版本、View、State：65-85。
- Close 也有 `BeginCloseOperation` 和 operationVersion：116-160。
- Push / Pop / MoveToTop 维护 layer 栈和 fullscreen index：200-290。
- `UpdateVisualState` 先 SortWindowVisible / SortWindowDepth，再 init/open：293-306。
- fullscreen 可见性和深度排序集中在 320-405。

这个模式直接对应 TryGet UI 的关键风险：窗口打开过程中可能被关闭、场景切换、资源加载失败、或者同一窗口重复打开。V2.3 至少要有 operation-version 语义，不建议只做接口。

### hsenl

可借鉴：

- `ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/Framework/FrameworkProxy.cs:10-30`
  - Unity MonoBehaviour proxy 负责 Awake 初始化、Update/LateUpdate 转发。
- `ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/UI/UIManager.cs`
  - static singleton + single/multi window 思路简单实用，可作为行为参考。
- `ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/Resource/Resource.cs`
  - 资源层可以晚一点再深入，不要拖慢 UI M1。

不要照搬：

- runtime 文件里混 editor class。
- static global manager 形态。
- UIManager 里的同步/异步加载细节直接绑死到当前资源系统。

### DGame GameObject pool

可留到 Unity object pooling 阶段：

- `ReferenceFramework/DGame/GameUnity/Assets/DGame/Runtime/Module/GameObjectPoolModule/GameObjectPool.cs`
- 有 async create、`CancellationToken`、destroy token、shutdown guard、`SpawnAsync`、`Recycle`。
- 对 V2.3 UI 不是前置条件，但 V2.5/V2.6 做 GameObject pool 时值得回看。

### BigCat

可作为生命周期参考，不是 UI M1 依赖：

- `ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/Node.cs:162-178`
  - 启动顺序：BeforeEventLoopStart、StartModules、ExportServices、StartWorkers、重新 ExportServices、AfterEventLoopStart。
  - 关闭顺序：StopWorkers 后 StopModules。
- `ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Gameplay/SceneMgr.cs`
  - scene manager、active/closed scene list、GTime、coroutine manager，适合后续 scene/service scope 设计。
- `CoroutineMgr` 的存在说明 coroutine/timer 是框架基础设施，但 TryGet 现有 `TGTaskScheduler` 已覆盖 phase queue，不需要复制 BigCat coroutine 体系。

## 推荐执行顺序

### V2.2.5 稳定化闸门

1. 修 TimerModule repeating self-cancel/self-pause。
   - 补测试：callback 内 `Cancel(selfHandle)` 后下一帧不再触发。
   - 补测试：callback 内 `Pause(selfHandle)` 后下一帧保持 paused，不继续触发。
   - 注意 catch-up 循环内 callback 多次触发时，取消/暂停后应停止补偿循环。

2. 重跑 Unity EditMode。
   - 如 Unity Editor 占用，先让用户关闭项目或允许用命令行关闭。
   - 不要在未重跑前宣称 412/412 通过。

3. 产品化 `Runtime/Unity` host。
   - 新增正式 `TryGetUnityHost` 或 `TryGetMonoEntry` 到 Runtime/Unity。
   - 保持 Core 无 UnityEngine 依赖。
   - Sample 只继承/使用正式 Runtime/Unity host。

4. 定 UI 异步生命周期协议。
   - 推荐：每个 UI entry 持有 operation version。
   - await 资源加载/初始化/打开动画返回后必须校验 version、state、owner。
   - 关闭、销毁、场景切换要能让未完成 open 失效。

### V2.3 UI 纵切片

不要先空铺一堆接口。第一刀应该能跑起来：

1. Core/UI 契约：
   - `IUIManager` 或 `IUIModule`
   - `IUIWindow`
   - `IUIResourceLoader`
   - `UILayer`
   - `UIOpenOptions` / `UICloseOptions`
   - `UIState`

2. Runtime/Unity 实现：
   - UIRoot 注入或自动创建，但不要强依赖 `GameObject.Find("UIRoot")`。
   - Canvas sortingOrder，layer base depth，window depth step。
   - Visible 控制 layer + GraphicRaycaster，不直接用 SetActive 做唯一语义。
   - Async open/close 带 operation-version 校验。

3. Sample：
   - 一个 prefab 或 Resources window。
   - 打开、置顶、关闭、fullscreen 遮挡下层。
   - 用 TGTask 跑 async open/close。

4. Tests：
   - Core state machine tests。
   - Unity EditMode：depth sort、visible/raycaster、重复 open、open 中 close、resource load failed。

## Definition of Done

进入 UI 实装前：

- TimerModule 新增自取消/自暂停测试并通过。
- Unity EditMode 当前 registry 失败已修复或明确记录为非 UI 阻塞。
- `Runtime/Unity` 有正式 host，sample 不再承载产品入口责任。
- UI async operation-version 规则写入接口或实现测试。

V2.3 UI M1 完成时：

- 能在 Unity sample 打开一个实际 UI window。
- 能关闭、重复打开、置顶。
- Fullscreen window 能正确隐藏/禁用下层输入。
- 异步资源加载完成后，如果窗口已关闭或 owner 已销毁，不会重新显示。
- Core 仍无 UnityEngine 依赖。

## 下一任接手命令

优先跑：

```powershell
dotnet build ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj
dotnet test MyTryGetFramework/Assets/MyTryGetFramework/Generators~/MyTryGetFramework.SourceGenerator.Tests/MyTryGetFramework.SourceGenerator.Tests.csproj
dotnet run --project Samples/Net/TryGet.Samples.Net.csproj
```

Unity EditMode：

```powershell
MyTryGetFramework/RunTests.bat
```

如果 Unity 项目已打开导致命令行测试失败，先让用户关闭 Unity Editor，再跑。不要擅自杀进程。

## 与 Claude handoff 的差异

Claude 的方向“V2.3 UI M1：创建 Core/UI interfaces”太轻了。基于当前源码和参考框架复核，UI 的首个里程碑不能只是接口层，否则会把最关键的 Unity 生命周期、资源加载取消、异步打开竞态推迟到后面，最终返工更大。

Codex 建议：UI 是下一阶段，但第一步应是 V2.2.5 稳定化闸门，然后做 UI 纵切片。这样既不在底层无限打磨，也不会把不稳定语义带进 UI。
