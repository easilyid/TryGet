# 参考框架设计调研

## 范围与成功标准

本次调研基于仓库内以下参考框架的代码证据：

- `ReferenceFramework/BigCat`
- `ReferenceFramework/TEngine`
- `ReferenceFramework/hsenl`

成功标准：对比三个框架的启动/生命周期模型、模块边界、依赖方向、Unity 耦合程度、编辑器/运行时分离、资源/事件/UI/配置/网络方案，并总结 `MyTryGetFramework` 应该借鉴与应该避免的设计。本次只做分析，不做实现。

---

## BigCat

### 启动/生命周期模型

BigCat 不是典型的 Unity 场景优先框架，它的核心生命周期是事件循环、节点、Worker 模型：

- `Node` 构造子 Worker、导出 RPC 服务、启动模块，然后按顺序启动 Worker：`StartModules()`、`ExportServices`、`StartWorkers`，最后重新导出所有 Worker 服务（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/Node.cs:154-168`）。
- 关闭流程反向执行：先停止 Worker，再清空导出的服务映射，最后停止并销毁模块（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/Node.cs:170-184`）。
- `Worker` 与 `UnityWorker` 共享相同生命周期形态：`BeforeEventLoopStart`、`StartModules`、`ExportServices`、`AfterEventLoopStart`（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/Worker.cs:80-107`，`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Unity/UnityWorker.cs:94-120`）。
- `DefaultMainModule` 通过 `TimeModule` 驱动帧节奏，使用 `FrameInterval`、`CheckMainLoop`、`BeforeMainLoop`、`AfterMainLoop` 控制主循环（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/DefaultMainModule.cs:68-112`）。

### 模块边界

BigCat 的边界分层清晰：

- `Fx`：节点、Worker、事件循环、RPC、模块生命周期。
- `Gameplay`：场景、游戏单元、游戏组件、时间、协程管理器。
- `Unity`：Unity 驱动的 Worker/事件循环桥接层。
- `UI`：基于 `MonoBehaviour` 节点的 Unity UI 抽象。
- `Editor`：资源管线、数据图、编辑器工具。

代码证据：

- 场景运行时足够独立，可以由 Worker/协程管理器驱动（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Gameplay/SceneMgr.cs:99-128`）。
- Unity 运行时代码是桥接层，而不是根抽象：`UnityWorker` 注释说明 Unity 用户需要实现一个 `MonoBehaviour` 来驱动 Worker、UI 和场景更新（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Unity/UnityWorker.cs:28-40`）。
- 编辑器资源构建隔离在 `unity/Assets/Scripts/Core/Editor/Assetor` 下，`PackageBuilder` 组合资源收集器和构建管线任务（`ReferenceFramework/BigCat/unity/Assets/Scripts/Core/Editor/Assetor/PackageBuilder.cs:29-48`）。

### 依赖方向

整体是“核心优先”的依赖方向：

- 核心 `Fx` 依赖通用库、注入、并发能力，不依赖 Unity（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/Node.cs:18-24`）。
- Unity 专属代码依赖 `Fx`，而不是 `Fx` 反向依赖 Unity（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Unity/UnityWorker.cs:18-27`）。
- Gameplay 只在必要位置通过条件编译接入 Unity/客户端能力，例如 `SceneMgr.Inst` 和客户端场景栈（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Gameplay/SceneMgr.cs:40-59`）。

### Unity 耦合程度

核心层中低耦合，Unity/UI 包中等耦合：

- `Node`、`Worker`、`DefaultMainModule`、RPC 和大多数 Gameplay 构造都是普通 C#。
- Unity 耦合集中在 `UnityWorker`、UI 和编辑器工具中。
- UI 明确是 Unity 耦合的：`UINode : MonoBehaviour`，并使用 `GameObject`、`Behaviour.enabled`、`SetActive` 和 Unity 子元素引用（`ReferenceFramework/BigCat/unity/Assets/Scripts/UI/Runtime/UINode.cs:30-43`，`ReferenceFramework/BigCat/unity/Assets/Scripts/UI/Runtime/UINode.cs:104-161`）。

### 编辑器/运行时分离

目录和程序集层面分离较强：

- 核心 C# 方案与 Unity 方案分离存在（`ReferenceFramework/BigCat/csharp/BigCat.sln`，`ReferenceFramework/BigCat/unity/BigCatUnity.sln`）。
- 编辑器资源管线位于 `unity/Assets/Scripts/Core/Editor/Assetor`，并导入 `UnityEditor`（`ReferenceFramework/BigCat/unity/Assets/Scripts/Core/Editor/Assetor/CollectorPackage.cs:18-28`）。
- 运行时 UI 位于 `unity/Assets/Scripts/UI/Runtime`，使用 Unity 运行时 API。

### 资源/事件/UI/配置/网络方案

- **资源**：自定义编辑器侧资源打包管线。`CollectorPackage` 初始化包元数据，通过 `AssetDatabase.GetAllAssetPaths` 收集所有 Unity 资源路径，构建 `PathCache`，并校验收集路径（`ReferenceFramework/BigCat/unity/Assets/Scripts/Core/Editor/Assetor/CollectorPackage.cs:80-128`）。在已抽样代码中，运行时资源加载不如编辑器构建管线突出。
- **事件/生命周期**：事件循环 + 模块生命周期，而不是零散依赖 Unity 消息。`DefaultMainModule` 还会按类型把 `WorkerEvent` 分发给已注册处理器（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/DefaultMainModule.cs:47-66`）。
- **UI**：自定义层级化 `UINode` 模型。它把节点配置（`UINodeCfg`）与运行时节点状态分离；配置保存脚本名、数据地址、元素、子节点、模板（`ReferenceFramework/BigCat/unity/Assets/Scripts/UI/Runtime/UINodeCfg.cs:24-82`）。
- **配置/数据**：资源管线中使用 Dson 可序列化的编辑器配置（`ReferenceFramework/BigCat/unity/Assets/Scripts/Core/Editor/Assetor/CollectorPackage.cs:30-37`）。
- **网络/RPC**：RPC 是核心能力。`RpcMethodAttribute` 定义导出规则、方法 ID 范围、参数/结果共享、手动返回、自定义切面数据（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/RpcMethodAttribute.cs:24-90`）。`S2SSessionMgr` 管理服务器会话，并在 Worker 线程上做请求派发校验（`ReferenceFramework/BigCat/csharp/Wjybxx.BigCat.Core/src/Fx/S2SSessionMgr.cs:23-103`）。

### MyTryGetFramework 应该借鉴

- 借鉴 **核心优先的依赖方向**：普通 C# 生命周期/核心先行，Unity 桥接层后置。
- 借鉴 **明确的启动/关闭顺序**：先启动自身模块，再导出/注册，再启动子节点；关闭时先关子节点，再关父模块。
- 借鉴 **Unity 桥接思路**（`UnityWorker`），但早期应简化成单一 Unity 驱动器。
- 借鉴 **编辑器/运行时分离**，避免运行时代码导入 `UnityEditor`。
- 如果 UI 成为重要子系统，可以借鉴 **UI 配置与运行时状态分离** 的思路。

### MyTryGetFramework 应该避免

- 避免过早引入 BigCat 完整的事件循环、Disruptor、RPC 架构；它很强，但对小型 Unity 框架过重。
- 避免服务端导向的 RPC/Session 抽象，除非多人/服务边界是当前目标。
- 避免在资源需求稳定前引入复杂编辑器资源管线。
- 避免在没有真实 UI 组合痛点前复制完整自定义 UI 层级。

---

## TEngine

### 启动/生命周期模型

TEngine 是 Unity 场景优先、流程驱动的框架：

- `GameEntry : MonoBehaviour` 在 `Awake` 中启动：强制创建更新、资源、调试器、FSM 模块，启动配置的流程，然后调用 `DontDestroyOnLoad`（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/GameEntry.cs:3-13`）。
- `ModuleSystem.GetModule<T>()` 按接口命名约定自动创建模块（例如 `IResourceModule` -> `ResourceModule`），并在注册时调用 `module.OnInit()`（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/Core/ModuleSystem.cs:67-88`，`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/Core/ModuleSystem.cs:142-193`）。
- 模块带优先级；更新模块会排序，并由 `ModuleSystem.Update` 每帧执行（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/Core/ModuleSystem.cs:16-40`，`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/Core/ModuleSystem.cs:142-205`）。
- 启动流是 FSM/Procedure 链：启动器 -> 闪屏 -> 初始化包 -> 初始化资源/下载/预加载/加载程序集/开始游戏。`ProcedureLaunch` 初始化启动 UI、语言、声音后立刻切到 `ProcedureSplash`（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/Procedure/ProcedureLaunch.cs:22-42`）。
- 热更入口调用 `GameApp.Entrance`，初始化生成的游戏事件、保存热更程序集、添加销毁监听，然后启动游戏逻辑（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/GameApp.cs:16-45`）。

### 模块边界

TEngine 的 Unity 工程层次比较清楚：

- `Assets/TEngine/Runtime`：可复用引擎模块。
- `Assets/TEngine/Editor`：编辑器工具。
- `Assets/GameScripts/Procedure`：启动、下载、热更流程。
- `Assets/GameScripts/HotFix/GameLogic`：热更游戏逻辑与 UI 模块。
- `Assets/GameScripts/HotFix/GameProto`：生成代码、协议、配置相关代码。
- `Assets/Launcher`：进入游戏前的加载/更新 UI。

程序集定义体现了这层拆分：`TEngine.Runtime`、`TEngine.Editor`、`Launcher`、`GameLogic`、`GameProto`（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/TEngine.Runtime.asmdef:0-28`）。

### 依赖方向

依赖方向是实用型 Unity 应用方向：

- Game、Launcher、Procedure 代码依赖 `TEngine` 运行时模块。
- `ModuleSystem` 是全局静态服务定位器。
- 热更层 `GameModule` 用静态属性包装模块访问（`Resource`、`Audio`、`Scene`、`Timer`、`Localization`、`UI`），底层回退到 `ModuleSystem.GetModule<T>()`（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/GameModule.cs:4-116`）。
- 运行时模块通过 `ModuleSystem` 互相依赖，例如 `AudioModule.OnInit` 获取 `IResourceModule`（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/Module/AudioModule/AudioModule.cs:320-325`，来自 grep 证据）。

### Unity 耦合程度

高耦合：

- 启动入口由 `MonoBehaviour` 驱动（`GameEntry`）。
- `UpdateDriver` 创建隐藏 Unity 对象/`MainBehaviour`，把 Unity 的 update/fixed/late/destroy/gizmos/application-pause 事件转发给模块监听器（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/Module/UpdataDriver/UpdateDriver.cs:204-390`，来自 grep 证据）。
- UI 模块直接查找 `GameObject.Find("UIRoot")`，使用 `Canvas`、`Camera`、设置 Unity Layer，并调用 `DontDestroyOnLoad`（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIModule.cs:14-113`）。
- 资源、场景、本地化、启动器、音频系统都偏 Unity/YooAsset/UGUI。

### 编辑器/运行时分离

目录和 asmdef 分离较好，但运行时仍然强 Unity 化：

- `TEngine.Editor.asmdef` 隔离在 `Assets/TEngine/Editor` 下。
- 编辑器工具包括设置面板、引用查找、图集制作、HybridCLR 构建工具、本地化 Inspector、UI 脚本生成器。
- 运行时 asmdef 的 `noEngineReferences: false`，并引用 Unity/包 GUID（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Runtime/TEngine.Runtime.asmdef:0-28`）。
- 编辑器代码导入 `UnityEditor`、`AssetDatabase`、`MenuItem`、`HybridCLR.Editor.Settings` 等（`ReferenceFramework/TEngine/UnityProject/Assets/TEngine/Editor/Utility/UpdateSettingEditor.cs:1-101`，来自 grep 证据）。

### 资源/事件/UI/配置/网络方案

- **资源**：以 YooAsset 为中心，并通过 `IResourceModule` 包装。`ProcedureInitPackage` 调用 `_resourceModule.InitPackage`，检查 `EPlayMode`，并处理编辑器、单机、Host、Web 模式（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/Procedure/ProcedureInitPackage.cs:30-121`）。
- **热更/代码加载**：`ProcedureLoadAssembly` 通过 `_resourceModule.LoadAssetAsync<TextAsset>` 加载 AOT 元数据和热更新 DLL 文本资源，然后 `Assembly.Load`，查找 `GameApp.Entrance` 并调用（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/Procedure/ProcedureLoadAssembly.cs:49-149`）。
- **事件**：热更入口通过 `GameEventHelper.Init()` 初始化生成的游戏事件（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/GameApp.cs:24-32`）；另有独立源码生成器位于 `Tools/GameEventSourceGenerator`（文件列表证据）。
- **UI**：`UIModule` 是热更层单例并实现 `IUpdate`，拥有窗口栈、根节点 Transform/Camera、错误日志器、资源加载器、层级/深度常量和窗口打开方法（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIModule.cs:14-113`，`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIModule.cs:244-252`）。资源加载通过 `IUIResourceLoader` 抽象，默认实现委托给 `IResourceModule`（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/IUIResourceLoader.cs:10-67`）。
- **配置**：Luban 支持出现在生成的 `GameProto/LubanLib`；启动器样式使用 JSON 路径 `RawBytes/UIStyle/Style.json`（`ReferenceFramework/TEngine/UnityProject/Assets/Launcher/Scripts/LoadStyle.cs:19-21`，来自 grep 证据）。
- **网络**：在抽样 TEngine 工程中不是核心重点；启动器/流程中使用 `UnityWebRequest` 做更新/下载相关能力（`ReferenceFramework/TEngine/UnityProject/Assets/GameScripts/Procedure/ProcedureCreateDownloader.cs:5-14`，来自 grep 证据）。

### MyTryGetFramework 应该借鉴

- 借鉴 **小而清晰的 `ModuleSystem`**：基于接口获取模块、优先级排序、更新列表、反向关闭，对 Unity 学习框架很实用。
- 借鉴 **Procedure/FSM 启动流**：让启动阶段可见，例如启动、资源初始化、预加载、开始游戏。
- 借鉴 **资源门面** 思路：UI/游戏代码应该调用框架资源抽象，而不是到处直接调用 YooAsset。
- 如果 `MyTryGetFramework` 需要 UGUI 页面，可以最低限度借鉴 **UI 根节点 + 窗口栈**。
- 借鉴 **编辑器/运行时 asmdef 拆分**。

### MyTryGetFramework 应该避免

- 避免把隐式类型命名创建（`IThingModule` -> `ThingModule`）作为唯一机制；它方便但在重命名/重构时脆弱。
- 避免全局静态服务定位器无处不在；可以用于组合边界，但不要渗透到所有领域代码。
- 避免在项目没有明确热更/配置管线目标时引入 HybridCLR、热更、Luban、源码生成器复杂度。
- 避免把 `GameObject.Find("UIRoot")` 作为核心不变量；优先使用序列化引用、启动配置或显式安装。
- 如果目标是可复用框架核心，避免让每个子系统都强 Unity 化。

---

## hsenl

### 启动/生命周期模型

hsenl 是 Unity Proxy 驱动，中心是一个 Framework 对象和多个注册式 Manager：

- `FrameworkProxy : MonoBehaviour` 初始化 `Framework`，启动它，设置目标帧率，把代理对象标记为 `DontDestroyOnLoad`，转发 `Update`/`LateUpdate`，并在应用退出时销毁框架（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/Framework/FrameworkProxy.cs:8-35`）。
- 各个代理组件在 `Awake` 中把 Manager 注册进 `SingletonManager`。例如 `ProcedureProxy` 先注销旧 `ProcedureManager`，再注册新的 Manager，注册所有 `AProcedureState` 子类，然后在 `Start` 中进入配置的入口状态（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/Procedure/ProcedureProxy.cs:6-42`）。
- Manager 生命周期通过 `SingletonManager.Register`、`Unregister`、`UnregisterAll` 显式管理；在非 .NET Core 路径下会按反向顺序注销（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Internal/Universal/Core/Singleton/SingletonManager.cs:5-92`）。
- Unity 单例组件在 `Awake` 自动注册，在 `OnDestroy` 注销（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/Singleton/UnitySingleton.cs:42-89`）。

### 模块边界

hsenl 范围很广，拆成几个主要程序集：

- `HsenlFramework.Runtime`：Unity、运行时、Gameplay、通用框架。
- `HsenlFramework.Network`：网络栈。
- `HsenlFramework.Editor`：编辑器工具。
- `HsenlFramework.ThirdParty`：第三方集成。

证据见 asmdef（`ReferenceFramework/hsenl/HsenlFramework/Runtime/HsenlFramework.Runtime.asmdef:0-18`，`ReferenceFramework/hsenl/HsenlFramework/Network/HsenlFramework.Network.asmdef:0-16`）。

运行时内部还进一步拆分为：

- `Internal/GameModule`：行为树、剧本、时间线、数值、物理、Gameplay 概念。
- `Runtime/Unity/Universal/Core`：框架、事件系统、实体、单例、场景、HTask。
- `Runtime/Unity/Universal/Module`：UI、资源、流程、数据库、计时器、时间、本地化、输入等。
- `Runtime/Unity/GameModule`：Unity 绑定的 Gameplay 组件。

### 依赖方向

依赖方向混合：

- 网络程序集是普通 C#，asmdef 中 `noEngineReferences: true`（`ReferenceFramework/hsenl/HsenlFramework/Network/HsenlFramework.Network.asmdef:14-16`）。
- 运行时 asmdef 引用 Unity 和包，`noEngineReferences: false`（`ReferenceFramework/hsenl/HsenlFramework/Runtime/HsenlFramework.Runtime.asmdef:0-18`）。
- Unity Proxy 模块注册 Manager，许多系统随后依赖静态单例访问。
- 事件系统使用程序集扫描：`EventSystemProxy` 查找引用 `HsenlFramework.Runtime` 的程序集，过滤编辑器程序集，然后交给事件管理器（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/EventSystem/EventSystemProxy.cs:24-53`）。

### Unity 耦合程度

`Runtime` 高耦合，`Network` 低耦合：

- `FrameworkProxy`、`ProcedureProxy`、`UIManager`、`UnitySingleton`、资源/场景辅助都与 Unity 强相关。
- `UIManager` 是 `UnitySingleton<UIManager>`，实例化 prefab，把它们挂到命名层级下，并使用 `Camera`、`Transform`、`GameObject`、YooAsset（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/UI/UIManager.cs:6-220`）。
- `Network` 明确不依赖引擎，是基于 socket、容器、Channel 的纯 C# 网络层（`ReferenceFramework/hsenl/HsenlFramework/Network/HsenlFramework.Network.asmdef:14-16`，`ReferenceFramework/hsenl/HsenlFramework/Network/Client/TcpClient.cs:0-259`）。

### 编辑器/运行时分离

程序集层面分离较好，但部分运行时文件中包含编辑器专属代码段：

- `HsenlFramework.Editor.asmdef` 隔离了构建、Luban、引导、代码生成、Inspector 工具。
- 运行时程序集文件里仍然有 `#if UNITY_EDITOR` 自定义编辑器或 Odin Inspector 钩子，例如 `FrameworkProxyEditor` 放在运行时文件内（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/Framework/FrameworkProxy.cs:37-56`）。
- 编辑器构建预处理单独存在，并导入 `UnityEditor.Build` API（`ReferenceFramework/hsenl/HsenlFramework/Editor/BuildPreProcessor/BuildPreProcessor.cs:0-50`）。

### 资源/事件/UI/配置/网络方案

- **资源**：基于 YooAsset，并增加自定义 Manifest/最优资源查找层。`Resource.GetOptimalAssetInfo` 根据 tag 下的 `AssetInfo` 和组合地址片段寻找最优资源，`GetOptimalAsset` 再通过 `YooAssets.LoadAssetSync` 加载（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/Resource/Resource.cs:7-70`）。`AssetManifest` 按 tag/address 缓存 `YooAssets.GetAssetInfos(tag)` 结果（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/Resource/AssetManifest.cs:8-21`，来自 grep 证据）。
- **事件**：由 `EventSystemProxy` 注册基于程序集扫描的事件系统管理器；它注册 `MemoryPackableAttribute`，并支持实验性的程序集替换热重载（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Core/EventSystem/EventSystemProxy.cs:24-72`）。
- **UI**：围绕 prefab 名称加载和层级挂载提供简单静态门面。单实例 UI 缓存在 `_singles`，多实例 UI 使用队列复用（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/UI/UIManager.cs:15-20`，`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/UI/UIManager.cs:55-166`）。
- **配置**：Luban 生成通过编辑器菜单调用 `Luban/ExcelConfig/` 下的 `gen.bat`（`ReferenceFramework/hsenl/HsenlFramework/Editor/LubanEditor/LubanGenEditor.cs:0-9`）。
- **网络**：完整的纯 C# TCP/KCP 风格网络架构。`TcpClient` 构建容器并绑定 Channel、PacketReceiver、PacketSender，配置 socket keepalive，连接后绑定 Channel 读写/错误事件，并在 `OnUpdate` 中驱动 Channel（`ReferenceFramework/hsenl/HsenlFramework/Network/Client/TcpClient.cs:23-187`）。`Channel` 是独立于 socket 的数据/消息转换抽象，负责收发 buffer 与读写钩子（`ReferenceFramework/hsenl/HsenlFramework/Network/Common/Channel/Channel.cs:0-72`）。

### MyTryGetFramework 应该借鉴

- 最小化借鉴 **Proxy 驱动模型**：一个 `FrameworkProxy` 负责初始化、Update、LateUpdate、Destroy，容易在 Unity 中理解和测试。
- 如果框架包含网络，借鉴 **无引擎依赖的网络边界**；hsenl 的 `Network` asmdef 是很好的分离样例。
- 早期 UI 可以借鉴 **简单 UIManager 形态**：层级根节点、单实例/多实例打开、按 prefab 名称加载。
- 如果确实需要静态访问，借鉴 **显式单例注册/注销**，因为它提供生命周期钩子和清理点。
- 借鉴 **资源 Manifest/门面** 思路，但不要照搬整套实现。

### MyTryGetFramework 应该避免

- 避免太多全局 singleton/proxy 组件；它们会让启动顺序难以推理。
- 避免把运行时代码和编辑器代码混在同一个运行时文件里，即使使用 `#if UNITY_EDITOR` 也应尽量拆到 Editor 程序集。
- 避免在没有明确需求前引入程序集扫描、热重载、事件替换。
- 避免复制完整庞大的 GameModule 目录；其中包含 AI、行为树、时间线、物理、数值、输入、数据库、本地化等，会淹没最小框架目标。
- 避免在运行时生命周期路径中使用 `DestroyImmediate`，除非明确是编辑器场景或有充分理由（`ReferenceFramework/hsenl/HsenlFramework/Runtime/Unity/Universal/Module/UI/UIManager.cs:35-53`）。

---

## 横向对比

| 维度 | BigCat | TEngine | hsenl |
|---|---|---|---|
| 启动模型 | 事件循环 Node/Worker 生命周期 | Unity `GameEntry` + Procedure FSM | Unity `FrameworkProxy` + Manager Proxy |
| 核心形态 | 服务端级纯 C# 核心 + Unity 桥接 | Unity 游戏框架/运行时模块 | 大而全 Unity 框架 + 无引擎网络层 |
| 模块边界 | Core/Gameplay/Unity/Editor 强拆分 | Runtime/Editor/Game/HotFix 清晰拆分 | 程序集拆分，但 Runtime 很宽 |
| 依赖方向 | Core -> Unity bridge，Unity 依赖 Core | Game/Procedure 依赖 Runtime，模块使用服务定位器 | 混合；Network 干净，Runtime 偏 Unity/静态 |
| Unity 耦合 | Core 低，Unity/UI 中等 | 高 | Runtime 高，Network 低 |
| 编辑器/运行时分离 | 强 | 较好 | 程序集较好，但部分运行时文件包含编辑器段 |
| 资源方案 | 自定义编辑器资源打包管线 | YooAsset 资源模块 + Procedure 流程 | YooAsset Manifest/Finder 门面 |
| 事件方案 | 事件循环 WorkerEvent + RPC | 生成事件 + FSM/Procedure | 程序集扫描事件系统 |
| UI 方案 | 自定义层级 `UINode` 配置/运行时 | 窗口栈 UIModule + 加载器门面 | 静态 UIManager + 层级根 + 单/多实例缓存 |
| 配置方案 | Dson/编辑器数据图配置 | Luban/Proto/热更友好配置 | Luban 编辑器生成 |
| 网络方案 | RPC/Session 服务核心 | 抽样项目中不是核心 | 完整纯 C# TCP/KCP Channel/Plug 栈 |

---

## 对 MyTryGetFramework 的建议

### 应作为一等设计方向借鉴

1. **使用小型 Unity 驱动器 + 普通 C# 核心。**
   - 借鉴 hsenl 的 `FrameworkProxy` 形态接入 Unity。
   - 借鉴 BigCat 的依赖方向：Unity 驱动 Core；Core 不应依赖 Unity，除非该类型明确是 Unity Adapter。

2. **使用轻量模块生命周期。**
   - 借鉴 TEngine 的 `ModuleSystem` 概念：注册/获取模块、按优先级更新、反向关闭。
   - 避免把 TEngine 的反射命名约定作为唯一路径；对学习型框架来说，显式注册更清楚。

3. **Procedure/FSM 只用于启动阶段。**
   - 借鉴 TEngine 的启动流程思路。
   - 不要在没有真实需求前引入热更、下载分支。

4. **建立硬性的编辑器/运行时边界。**
   - 遵循 BigCat/TEngine 的目录拆分方式。
   - 不采用 hsenl 那种在运行时文件中放编辑器类的模式，即使有条件编译也不建议。

5. **资源系统先做门面。**
   - 定义小型资源接口，用于 prefab/asset 加载。
   - 把 YooAsset、Resources、Addressables 的选择隐藏在接口后面。
   - 暂时不要构建自定义资源打包管线。

6. **UI 从简单模型开始。**
   - 对早期框架来说，hsenl 的层级 + 单实例/多实例打开模型，比 BigCat 完整 `UINode` 层级或 TEngine 完整窗口栈更轻。
   - 如果 UI 增长，再吸收 TEngine 的资源加载抽象、窗口栈和层级深度概念。

7. **网络应可选且隔离。**
   - 如果加入网络，借鉴 hsenl 的 `noEngineReferences` 边界和 Channel 抽象。
   - 不要把网络绑定进 Unity 生命周期，只通过 Adapter/update pump 接入。

### 应明确避免的反目标

- 不要把 BigCat 完整 Node/Worker/RPC/Disruptor 模型复制进 Unity 学习框架。
- 不要复制 TEngine 的 HybridCLR、热更新、资源下载链路，除非热更新就是项目目标。
- 不要复制 hsenl 大量 Manager/Proxy 的表面积；它会让框架启动顺序和所有权更难看清。
- 不要让 `GameObject.Find`、静态单例访问、反射扫描成为核心行为基础。
- 不要让编辑器代码泄漏进运行时程序集。

### 建议的最小目标架构

```text
MyTryGetFramework
├─ Runtime/Core
│  ├─ Framework
│  ├─ ModuleSystem
│  ├─ IModule / IUpdateModule / IFixedUpdateModule / ILateUpdateModule
│  └─ Procedure（可选，仅用于启动）
├─ Runtime/Unity
│  ├─ FrameworkProxy : MonoBehaviour
│  ├─ UnityTimeProvider
│  └─ UnityResourceLoader adapter
├─ Runtime/UI（可选）
│  ├─ UIManager
│  ├─ IUIWindow
│  └─ IUIResourceLoader
└─ Editor
   └─ 仅放 Inspectors / generators / validation tools
```

这个组合的底层逻辑是：

- 用 hsenl 的可见 Unity Proxy 生命周期做入口；
- 用 TEngine 的易理解模块/流程模型做骨架；
- 用 BigCat 的核心优先依赖方向和干净关闭纪律做约束。
