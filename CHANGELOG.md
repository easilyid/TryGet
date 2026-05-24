# Changelog

V0.x 时期：未承诺时间，按 Gate criteria 升版本（design.md §12）。

## V0.5（进行中 — Core 服务补齐 + InputModule）

### 迭代 0 — InputModule（V0.4 deferred 补齐）

**Added**
- `IInputModule` 接口：按 Action 名查询输入（IsPressed / WasPressedThisFrame / WasReleasedThisFrame / GetAxis / GetAxis2D / ActiveCount）
- `MemoryInputModule`：3 HashSet 区分 _pressed / _pressedThisFrame / _releasedThisFrame；
  SimulatePress / SimulateRelease / SetAxis / SetAxis2D 测试 API；
  Axis 自动 clamp 到 [-1, 1]；同帧 Press→Release 两个 edge 都保留 true（与 Unity 一致）
- 22 个 EditMode 测试

### 迭代 1 — ConfigModule（V0.3 deferred 补齐）

**Added**
- `IConfigModule` 接口：Register / Get / TryGet / Has / Unregister / RegisteredCount
- `MemoryConfigModule`：Dictionary<string,object>，Priority=-460（最早一批服务）
- `ConfigNotFoundException` 带 Key 属性
- 21 个 EditMode 测试

**Notes**
- 与 IResourceModule 语义区分：Resource 加载运行时对象（Prefab/AudioClip），Config 查询业务表数据（武器表/关卡表）

### 迭代 2 — SceneModule

**Added**
- `ISceneModule` 接口：Load / Unload / SetActive / IsLoaded / ActiveScene / LoadedScenes / UnloadAll / LoadedCount
- `MemorySceneModule`：List 保插入顺序 + HashSet O(1) IsLoaded + active 字符串引用，Priority=-250
- 19 个 EditMode 测试

**Notes**
- 默认 additive 加载语义（与 Unity LoadSceneMode.Additive 一致）
- 首个加载场景自动成为 ActiveScene；卸载 active 后 ActiveScene 置 null

### 迭代 3 — SceneFlowDemoTests 端到端 demo + Input edge 调度 bugfix

**Fixed**
- `MemoryInputModule` 从 `IUpdateModule` 改为 `ILateUpdateModule`：
  Update 调度按 Priority 升序，Input (-350) 在 Procedure (-200) 之前 Update，
  会先清掉 _pressedThisFrame 导致业务永远看不到本帧输入。改 LateUpdate 后业务在 Update 阶段消费 edge，host.LateUpdate 后才清空，与 Unity Input.GetKeyDown 行为一致

**Added**
- `SceneFlowDemoTests`：V0.5 端到端 demo，MainMenu → press Confirm → Battle 流程
  串联 V0.5 三件套（Input/Config/Scene）+ V0.4 Audio
- 2 测试：完整流程 + Input edge 调度顺序验证

### V0.5 Module Priority 链（进行中）
Log(-1000) → Pool/Timer(-500) → Config(-460) → Save(-450) → Localization(-420) →
Resource(-400) → Audio(-380) → Input(-350) → UI(-300) → Scene(-250) →
Procedure(-200) → EntityWorld(-100) → 业务(0)

### 迭代 4 — TimerModule 增强

**Added**
- `ITimerModule.ScheduleRepeat(intervalSeconds, callback)`：周期触发直到 Cancel
- `ITimerModule.Pause(handle)` / `Resume(handle)` / `IsPaused(handle)`：暂停/恢复（关卡暂停刚需）
- `TimerModule.Entry` struct 增加 Interval / Repeating / Paused 字段（非破坏性）
- 16 个 EditMode 测试

**Notes**
- 周期 timer interval<=0 抛 ArgumentOutOfRangeException（防死循环）
- 单帧 deltaTime >> interval 时只触发一次（不补帧），避免长时暂停后连发

### 迭代 5 — AudioModule 增强

**Added**
- `IAudioModule.Pause(cue)` / `Resume(cue)` / `IsPaused(cue)` / `PauseAll(category)` / `ResumeAll(category)`
- `MemoryAudioModule` 内部 Dictionary 改为 <cue, PlayingEntry>（struct 含 Category + Paused）
- 17 个 EditMode 测试，含游戏暂停菜单场景验证

**Notes**
- Pause 后 IsPlaying 仍 true（cue 保留在播放列表，与 Stop 区分）
- 重新 Play 隐含 Resume（业务"重启"语义）

### 迭代 6 — UnityAudioModule Adapter（第一个 Unity Adapter）

**Added**
- `Runtime/Unity/Audio/UnityAudioModule.cs`：IAudioModule 的 Unity 实现，基于 AudioSource 池（默认 16）
- `RegisterClip(cue, AudioClip)` / `UnregisterClip(cue)` API：业务先加载 AudioClip 后注入 Adapter
- 溢出策略：池满时 FIFO 复用最旧 cue
- MasterVolume × CategoryVolume 合成 effective volume，Set 时刷新所有在播 AudioSource
- **新建 Tests/PlayMode test asmdef**（基建）：references Core + Unity + TestRunner，includePlatforms=[] 允许所有平台
- `UnityAudioModulePlayModeTests` (11 测试) 用 AudioClip.Create 生成 1 秒静音 clip

### 迭代 7 — UnityInputModule Adapter（接 InputSystem 1.18）

**Added**
- `Runtime/Unity/Input/UnityInputModule.cs`：IInputModule 的 Unity 实现，接 com.unity.inputsystem 1.18.0
- `RegisterButton(name, binding)` / `RegisterAxis(name, binding)` / `RegisterAxis2D(name, binding)` API：
  业务用 binding 字符串注册（如 "<Keyboard>/space"），Adapter 内部 new InputAction + Enable + hook performed/canceled
- ILateUpdateModule：LateUpdate 清 edge buffers（与 MemoryInputModule + V0.5 Iter 3 修正语义一致）
- Shutdown：DisposeAll 所有 InputAction，释放 OS 输入资源
- MyTryGetFramework.Unity.asmdef references 加 "Unity.InputSystem"
- Tests.PlayMode.asmdef references 加 "Unity.InputSystem" + "Unity.InputSystem.TestFramework"
- `UnityInputModulePlayModeTests` (11 测试) 用 `InputSystem.AddDevice<Keyboard>/<Gamepad>` + `StateEvent.From` 模拟硬件

### V0.5 Module Priority 链（最新）
Log(-1000) → Pool/Timer(-500) → Config(-460) → Save(-450) → Localization(-420) →
Resource(-400) → Audio(-380) → Input(-350) → UI(-300) → Scene(-250) →
Procedure(-200) → EntityWorld(-100) → 业务(0)

### 迭代 8 — PlayerPrefsSaveModule Adapter

**Added**
- `Runtime/Unity/Save/PlayerPrefsSaveModule.cs`：ISaveModule 的最简 Unity Adapter，接 UnityEngine.PlayerPrefs
- bool 编码为 int (0/1) 透明转换（PlayerPrefs 原生只支持 string/int/float）
- 内部 _keys HashSet 维护 KeyCount 诊断
- Shutdown 不擦盘（持久化数据不应被 Adapter Shutdown 擦除）
- `PlayerPrefsSaveModulePlayModeTests` (16 测试)

### 迭代 9 — UGUIUIModule Adapter

**Added**
- `Runtime/Unity/UI/UGUIUIModule.cs`：IUIModule 的 Unity 实现，基于 UGUI Canvas
- V0.5 最小版（不分层 / 不 Modal / 不传参，留给业务子类化扩展）
- 单 Canvas root + GraphicRaycaster，Initialize 创建、Shutdown 销毁
- `RegisterPrefab/UnregisterPrefab` API（不耦合 IResourceModule）
- 委托 MemoryUIModule 做状态机，Adapter 只包裹 GameObject lifecycle
- `virtual OnOpened/OnClosed` 钩子供业务子类化扩展
- 加 "UnityEngine.UI" references
- `UGUIUIModulePlayModeTests` (11 测试)

### 迭代 10 — UnitySceneModule Adapter

**Added**
- `Runtime/Unity/Scene/UnitySceneModule.cs`：ISceneModule 的 Unity 实现，接 SceneManager
- Load 同步（LoadScene Additive）、Unload 异步 fire-and-forget（UnloadSceneAsync）
- 内部 List+HashSet 维护"已请求加载"状态，与 MemorySceneModule 契约一致
- Shutdown 不卸载已加载场景（运行时资源持久化）
- `UnitySceneModulePlayModeTests` (13 测试)

### V0.5 Unity Adapter 套件总结
5 个 Unity Adapter 全数落地（Audio / Input / Save / UI / Scene），合计 ~62 PlayMode 测试。
Adapter 测试基建（Tests/PlayMode asmdef + InputSystem.TestFramework + UGUI）就位。

### V0.5 Gate 剩余项（待做）
- [ ] NetworkModule（IChannel + IMessageBus 接口）— Plan agent 建议与首个真实实现共生设计
- [ ] Adapters/Mirror 默认实现
- [ ] MemoryPack 序列化集成
- [ ] HybridCLR IHotfixLoader 接口 + 实现
- [ ] YooAsset Adapter（包未装，物理阻塞）

---

## V0.4（Common Modules 五件套）

### 迭代 0 — ResourceModule

**Added**
- `IResourceModule` 接口：`Load<T>` / `TryLoad<T>` / `Release` / `Register` / `Unregister` / `RegisteredCount`
- `MemoryResourceModule`：Dictionary<string,object> 实现，Priority=-400，介于 Pool/Timer (-500) 与 EntityWorld (-100) 之间
- `ResourceNotFoundException` 带 `Path` 属性便于诊断
- 18 个 EditMode 测试

**Notes**
- Core 不依赖 YooAsset / Addressables / UniTask（Adapter 层后续迭代提供 YooAssetResourceModule）

### 迭代 1 — UIModule

**Added**
- `IUIModule` 接口：`Open` / `Close` / `IsOpen` / `OpenedUIs` / `OpenedCount` / `CloseAll`
- `MemoryUIModule`：List 保插入顺序 + HashSet 保 O(1) IsOpen，Priority=-300
- 17 个 EditMode 测试

**Notes**
- Core 只管"哪些 UI 在打开"的状态机，不渲染、不分层、不传参、不 Modal（这些由 Adapters/UGUI 层 UGUIUIModule 扩展）
- 重复 Open 同名抛 InvalidOperationException；Close 不存在 UI 静默幂等

### 迭代 2 — SaveModule

**Added**
- `ISaveModule` 接口：`HasKey` / `Get/Set(String|Int|Float|Bool)` / `DeleteKey` / `DeleteAll` / `Save` / `KeyCount`
- `MemorySaveModule`：单 Dictionary<string,object> 实现（保 KeyCount 准确、同 key 跨类型互斥），Priority=-450
- 24 个 EditMode 测试

**Notes**
- 行为对齐 PlayerPrefs：同 key 跨类型 Set 覆盖；Get 类型不匹配/key 不存在返回 defaultValue（读容错）；Set null/empty key 抛 ArgumentException（写强约束）
- Save() 为 no-op；Adapter 实现负责真正持久化（PlayerPrefs / 本地文件 / 云存档）

### 迭代 3 — LocalizationModule

**Added**
- `ILocalizationModule` 接口：`CurrentLanguage` / `SetLanguage` / `RegisterTable` / `T(key)` / `T(key, default)` / `TryGet` / `AvailableLanguages` / `RegisteredCount`
- `MemoryLocalizationModule`：Dictionary<语言, Dictionary<key,值>> 双层表，Priority=-420
- 23 个 EditMode 测试

**Notes**
- production-ready 实现（不是 stub），Adapter 层只需"从 Excel/CSV/JSON 加载 + RegisterTable"工厂
- 与 Unity Localization Package / i18next 共识：T(key) 漏译返回 key 本身（让 UI 立即暴露漏译）
- RegisterTable 浅拷贝输入 dict 防外部污染；重复注册同语言覆盖；覆盖 current 立即刷新引用

### 迭代 4 — AudioModule

**Added**
- `AudioCategory` 枚举：BGM / SFX / UI / Voice
- `IAudioModule` 接口：`Play(cue, category)` / `Stop` / `StopAll(category)` / `StopAllSounds` / `IsPlaying` / `MasterVolume` / `SetMasterVolume` / `GetCategoryVolume` / `SetCategoryVolume` / `PlayingCount`
- `MemoryAudioModule`：Dictionary<cue, AudioCategory> 状态机 + 4 个 float 存分类音量，Priority=-380
- 22 个 EditMode 测试

**Notes**
- Core 不依赖 AudioClip / AudioSource / AudioMixer；Adapters/Unity 层后续 UnityAudioModule 接 AudioSource
- 同 cue 重复 Play 幂等；音量自动 clamp 到 [0,1]；Shutdown 清空列表 + 重置音量

### 迭代 5 — V0.4 端到端 demo

**Added**
- `MainMenuFlowDemoTests`：Boot → MainMenu → InGame 三 Procedure 串联 V0.4 五件套全协同
  - Boot：Save.GetString("lang") → Localization.SetLanguage → Resource.Register UI Prefab → TransitionTo MainMenu
  - MainMenu：Resource.Load + UI.Open + Audio.Play(BGM) + Localization.T 显示标题
  - InGame：UI.Open(HUD) + Audio.Play(SFX) + Save.Save 写时间戳
- 3 个测试：默认 zh-CN 流程、从 Save 恢复 en-US 偏好、Priority 拓扑顺序验证

### V0.4 Module Priority 完整链
Log(-1000) → Pool/Timer(-500) → Save(-450) → Localization(-420) → Resource(-400) → Audio(-380) → UI(-300) → Procedure(-200) → EntityWorld(-100) → 业务(0)

### Deferred to V0.5
- **InputModule**：强耦合 Unity InputSystem 包，Core 抽象价值低，留 V0.5 与 UnityInputAdapter 一起做
- **YooAsset adapter**：V0.5 首推（IResourceModule 接口设计漏没漏，得真实加载验证）
- **UGUIUIModule adapter**：紧随 YooAsset，配合跑通"加载 Prefab → Open UI"真实闭环
- **sqlite-net SaveModule adapter**：Core ISaveModule + Memory 已封口，sqlite-net 降级为 Adapter 备选

---

## V0.3（玩法层增强 + 关键服务）

### 迭代 0 — EntityWorld 拆分

**Breaking**
- `World` → `EntityWorld`（重命名 + 实现 `IModule` / `IUpdateModule`）
- `WorldState` → `EntityWorldState`
- `world.Update()` 无参版本 → `world.Update(float deltaTime, float unscaledDeltaTime)`
- `IWorldAdapter` 删除（被 ADR-0014 Adapter 模式替代）
- `Handle.Resolve(World)` → `Handle.Resolve(EntityWorld)`
- `SystemBase._world / SetWorld(World)` → `EntityWorld`

**Added**
- `IEntityWorld` 接口：暴露 Entities / CreateEntity / RegisterSystem / EventBus
- `WorldProxy`（Unity 侧）改为持有 EntityWorld，Update 用 `Time.deltaTime`
- `ModuleHostEndToEndTests` 验证完整生命周期闭环
- `EntityWorldModuleIntegrationTests` 5 用例验证 ModuleHost + EntityWorld 协同

**Removed**
- `IWorldAdapter.cs` + `FakeWorldAdapter` + `FakeAdapterTests.cs`

**Deprecated**
- `IWorldEventBus`（V0.4 计划合并到 `IEventBus` 全局事件总线）

### 迭代 1 — BitArray256 位运算加速

**Added**
- `BitArray256` struct：4×ulong 固定容量 256 位，O(1) Add/Remove/Contains/ContainsAll/ContainsAny
- `TypeIndex<T>` 静态泛型：lazy 分配稳定 int index
- `TypeRegistry` + `TypeIndexOverflowException`：类型注册中心
- `Entity._aspectMask / _tagMask`：与字典并行的 mask 镜像
- `Query` 内部完全位运算（删除 HashSet<Type>）

**Performance**
- Query.Matches 退化为 4 次 ContainsAll/ContainsAny 位运算
- 预期 10000 Entity × 10 Query 场景 5-10x 提升（待 Unity 跑实测）
- benchmark 测试：10000 × 5 × 10 frame = 500k Matches 实测 < 1s

### 迭代 1.5 — 加固

**Fixed**
- `Entity.Attach` OnAttach 异常时完整回滚 `_aspects / _aspectMask / Owner`
- `Entity.Detach` OnDetach 异常时回滚 `_aspects / _aspectMask`（Owner 保留作"未完全 detach"标志）
- `TypeRegistry` 所有公开 API 加 `lock(_lock)` 串行化，避免 `_next++` 并发竞态

### 迭代 2 — ProcedureModule（跨帧流程状态机）

**Added**
- `IProcedure` + `ProcedureBase`（OnEnter / OnUpdate / OnExit）
- `IProcedureModule` + `ProcedureModule`（Priority=-200）
- `IProcedureModule.Host` 注入：Procedure 通过 `m.Host.Get<...>()` 拉其他 Module

### 迭代 3 — 端到端 Demo

**Added**
- `LoginFlowDemoTests`：Boot → Login → InGame 三阶段完整流程
  覆盖 ModuleHost / Common 三件套 / EntityWorld / ProcedureModule / System / Aspect / Pool / Timer / Log 全协同

---

## V0.2（ModuleHost 基础设施）

### 迭代 0-3 — ModuleHost 拓扑排序 + Update/LateUpdate 调度
- `IModule` / `IUpdateModule` / `ILateUpdateModule` / `IModuleHost` / `IEventBus`
- `ModuleHost`（Kahn 拓扑排序 + Priority tie-breaker）
- 类型化异常：`ModuleAlreadyRegistered` / `NotRegistered` / `CircularDependency` / `DependencyMissing` / `Shutdown`

### 迭代 4 — Common 三件套
- `ILogModule` + `ConsoleLogModule`（Priority=-1000，OnLog 钩子，CaptureToMemory）
- `ITimerModule` + `TimerModule`（Priority=-500，IUpdateModule 驱动，scaled/unscaled 双轨）
- `IPoolModule` + `IObjectPool<T>` + `PoolModule`（Priority=-500，Stack-based）

### 迭代 5 — 双端门 + V0.1 资产迁移

**Added**
- `ServerProject/MyTryGetFramework.Core/MyTryGetFramework.Core.csproj`：Shadow csproj 双端编译验证
- 6 个 V0.1 复用文件平移到 `Runtime/Core/Entity/`：Aspect / Entity / EntityId / Handle / Tag / Phase

### 迭代加固
- ModuleHost 错误路径加固：Initialize 半失败回滚 / Shutdown 异常聚合 / DependsOn null 防御
- TimerModule callback 异常聚合（同帧其他 timer 不受单 callback 失败影响）
- ConsoleLogModule Shutdown 后静默 / OnInit 重新可写
- IObjectPool.Return 文档警示重复 Return 是未定义行为

---

## V0.1（ECS 骨架）

- `World` / `Entity` / `Aspect` / `Tag` / `EntityId` / `Handle` / `Phase`
- `SystemBase` / `SystemGroup` / `Query`（HashSet<Type> 路径）
- `IEntityEventDispatcher` + `EntityEventDispatcher`
- `IWorldEventBus` + `WorldEventBus`
- `IWorldAdapter` + `WorldProxy`（V0.3 删除/重构）
- Aspect 隔离纪律（ADR-0007）
- 10 个 ADR + 18 个 issue 骨架
