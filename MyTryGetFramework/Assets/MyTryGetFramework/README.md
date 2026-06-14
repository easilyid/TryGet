# TryGet Framework

纯客户端 Unity 游戏框架，提供模块化服务容器、异步任务调度、流程栈管理与 Source Generator 自动注册。

## 核心特性

- **ModuleSystem 服务容器** — 统一的模块生命周期管理与依赖注入
- **FrameLoop 多阶段调度** — EarlyUpdate / Update / LateUpdate / FixedUpdate / EndOfFrame 五阶段分桶执行
- **自研 TGTask 异步原语** — 零外部依赖的 async/await 支持，含调度器驱动的帧/秒级延迟；切换/关闭等特定流程会显式取消挂起任务（暂无通用超时或 CancellationToken API）
- **Procedure Stack 流程管理** — 栈式流程切换（Push/Pop/Replace），支持暂停/恢复
- **零 GC 事件系统** — 泛型 struct 事件，稳态派发零分配，重入安全
- **Source Generator 自动注册** — `[Module]` / `[EventHandler]` 特性驱动的编译期代码生成
- **纯 C# 核心** — Core 层 `noEngineReferences: true`，可测试性强，引擎无关

## 快速开始

```csharp
using TryGet;
using TryGet.Async;

// 创建 Host（GameLauncher 预注册 Core 基础三件套：ILogger / IClock / ITGTaskScheduler）
var host = GameLauncher.CreateHost();

// 注册模块
host.Register<ITimerModule>(new TimerModule());
host.Register<IProcedureModule>(new ProcedureModule());

// 初始化
host.Initialize();

// 帧驱动（Unity 端由 MonoBehaviour 桥接）
void Update() {
    host.Update(Time.deltaTime, Time.unscaledDeltaTime);
}

void LateUpdate() {
    host.LateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
}
```

## 安装

### Git URL（推荐）
在 Unity Package Manager 中选择 "Add package from git URL"，输入：
```
https://github.com/easilyid/TryGet.git?path=/Assets/MyTryGetFramework
```

### 本地开发
克隆仓库后，在 Package Manager 中选择 "Add package from disk"，指向 `Assets/MyTryGetFramework/package.json`。

## 系统要求

- Unity 6000.0 或更高版本
- .NET Standard 2.1

## 文档

详见 `ARCHITECTURE.md` 了解架构设计与模块说明。

## 许可证

MIT License
