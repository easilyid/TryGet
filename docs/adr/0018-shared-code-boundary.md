# ADR-0018: Samples/Shared 跨端共享代码边界规范

## Status

Superseded by ADR-0020（V1.0 落地生效；V2.0 路线 C 移除 Samples/Shared 跨端共享层后归档，双端推迟 V3.0+）

## Context

V0.5.5 Adapter 退场后，TryGet 的代码分层稳定为：

| 层 | 物理位置 | 跨端性 |
|---|---|---|
| **Core** | `Assets/MyTryGetFramework/Runtime/Core/`（Unity 端） + `ServerProject/MyTryGetFramework.Core/`（Shadow csproj 反向引用 Unity 源） | **跨端**（netstandard2.1） |
| **Net Adapter** | （Net 端独立程序集，V1.1+） | Net 专属 |
| **Unity Adapter** | `Samples/Unity/Adapters/`（Audio/Input/UI/Scene/Save 5 件套） | Unity 专属 |
| **Samples/Net** | `Samples/Net/`（dotnet console demo） | Net 专属 |
| **Samples/Unity** | （仅有 V0.3 期 WorldProxy.cs 残留） | Unity 专属 |

V1.0 推进时遇到新需求：**业务侧业务 Aspect / 网络消息 struct / Module 接口希望"一份代码两端共享"**。

举例（V1.1+ 真做时的形态）：
- `MoveAspect` — 客户端用来本地预测移动，服务端用来权威 tick；两端共用同一 Aspect 类
- `MoveRequest` / `MoveBroadcast` — 网络消息 struct，客户端 send / 服务端 recv（反之亦然），两端共用同一 struct 定义
- `IRoomService` — 业务 Module 接口（业务自身定义），两端共用接口契约，各自实现

**现状不足**：
- Core 是"框架"层，业务代码不应混入 Core
- Net/Unity Adapter 各自单端，不能承载跨端业务
- 缺一个**专门承载跨端业务代码**的层

## Decision

**新增 `Samples/Shared/` 层（V1.0 引入），作为跨端业务代码的物理承载**。

### 1. 物理位置与 csproj

```
Samples/
├── Shared/                              # NEW V1.0
│   ├── TryGet.Shared.csproj            # netstandard2.1，ProjectReference Core
│   ├── SharedInfo.cs                   # Iter 1 placeholder
│   └── (业务 Aspect / 网络消息 / Module 接口)
├── Net/                                # 已有
│   └── TryGet.Samples.Net.csproj       # net8.0，ProjectReference Shared
└── Unity/                              # NEW V1.0（Entry 模板等）
    └── Entry/
        └── TryGetMonoEntry.cs          # MonoBehaviour 模板
```

### 2. csproj 引用关系图

```
TryGet.Samples.Net (net8.0)
        │
        ▼
TryGet.Shared (netstandard2.1)
        │
        ▼
MyTryGetFramework.Core (netstandard2.1) — Shadow csproj
        ↑
        │ 反向 Compile Include
        │
MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/ (Unity 源)
```

Unity 端：
- `Assets/MyTryGetFramework/Runtime/Core/` 由 Unity asmdef 编译为 `MyTryGetFramework.Core.dll`
- `Samples/Shared/*.cs` 在 Unity 端通过 **asmdef + 物理路径引用**接入（详见 §5）

### 3. Shared 代码允许的内容

| 类型 | 允许？ | 理由 |
|------|-------|------|
| 业务 Aspect / Aspect 子类 | ✅ | 跨端业务核心 |
| 业务 Module 接口（如 `IRoomService`） | ✅ | 业务 host.Get<T> 拉依赖 |
| 业务 Module 实现（如 `MemoryRoomService`） | ⚠️ 仅限内存/纯逻辑 | 真实现（如 RoomServer + KCP）通常带 Adapter 依赖，放各自端 |
| 网络消息 struct（实现 `INetMessage`） | ✅ | 跨端共享是网络消息天然需求 |
| 业务 Event struct（IEventBus 用） | ✅ | 同上 |
| 业务工具类（如 `RoomIdGenerator`） | ✅ | 跨端业务可共享 |
| `[Module]` 标记 | ✅ | V0.9.5 Source Generator 跨端 |
| `[SystemRegister]` / `[EventHandler]` 标记 | ✅ | 跨端注册 |

### 4. Shared 代码禁止的内容

| 类型 | 禁止？ | 缓解 |
|------|-------|------|
| `using UnityEngine` / Unity API | ❌ | dotnet build 强制；放 `Samples/Unity/` 或 Adapter |
| `using System.Drawing` / Net 专属 API | ❌ | 同上；放 `Samples/Net/` 或 Adapter |
| KCP / LiteNetLib / Network 协议库引用 | ❌ | 网络协议是 Adapter 内部决策；Shared 仅持 `INetClient/INetServer` 接口与 INetMessage struct |
| 序列化库（MemoryPack / Protobuf）的真依赖 | ❌ | 同上；Shared 仅持 `ISerializer` 接口；序列化 Attribute（如 `[MemoryPackable]`）可以放（不引入运行时反序列化代码） |
| Editor-only API | ❌ | 跨端无意义 |
| UnityEditor namespace | ❌ | 同上 |

### 5. Unity 端接入 Shared 的方式

**V1.0 选择源引用，不用 Plugin DLL**：

| 方案 | 选 / 不选 | 理由 |
|------|---------|------|
| **A. 源引用**（Unity asmdef + Samples/Shared/*.cs 软链接 / asmdef includePaths） | **选** | 双端可直接 step-into 调试；改源即两端同步 |
| **B. Plugin DLL**（Net 端 build `.dll`，copy 到 Unity Plugins/） | **不选**（V1.0） | 调试体验差（DLL 无源码可看）；ET 框架已踩过此坑 |
| **C. Asset 复制脚本** | **不选** | 复制后 GUID 漂移、源数据丢失 |

**V1.0 落地细则**：
- `Samples/Shared/` 物理路径同时被 Net csproj 和 Unity asmdef 包含
- Unity asmdef 选项：
  - 在 `Assets/MyTryGetFramework/Samples/Shared/` 建一个 asmdef，`Override Files Match Pattern` 反向引用 `../../../../../Samples/Shared/*.cs`
  - 或更简单：让业务自行把 `Samples/Shared/` 内 .cs 文件软链接到 Unity Assets 内
- V1.0 不提供官方接入脚本（留 V1.1+ 文档化标准 workflow）

### 6. Iter 1 现状

V1.0 Iter 1 只建立 csproj + 物理目录 + 此 ADR，**Shared 内部业务代码留空（仅 `SharedInfo.cs` placeholder）**。后续 V1.1+ 业务真做时再填充：
- V1.1 网络 Adapter demo：会用到 Shared 内的 `MoveRequest` / `MoveBroadcast` 等网络消息
- V1.2 真 MMO demo：会有 `RoomAspect` 等业务 Aspect

## Consequences

### 好处

- **业务跨端代码有专属落地点**：不再因为"放 Core 污染框架 / 放 Adapter 单端"而纠结
- **维持 Core 边界整洁**：业务代码不混入 Core，Core 永远是"框架"层
- **与 ET/Fantasy/BigCat 路线一致**：三框架都有"Shared / Model / Core"跨端层
- **Net 端接入零摩擦**：标准 `<ProjectReference>` 一行搞定
- **Unity 端调试体验好**：源引用方式让 step-into 工作

### 风险

- **Unity 端 asmdef 配置仍未完全自动化**（V1.0 不提供官方脚本，业务自行配 includePaths 或软链接）
- **跨端代码纪律靠人工 review**（dotnet build 能拦 UnityEngine 但不能拦不必要的依赖增长）
- **Shared csproj 与 Core csproj 边界可能模糊**（"框架 vs 业务"边界靠纪律：Core 是无业务知识的通用机制，Shared 是含业务知识的跨端逻辑）

### 不允许的退路

- ✗ 把 Shared 业务代码放进 Core：违反 Core "无业务知识" 原则（ADR-0011 §3 隐含约定）
- ✗ 让 Shared csproj 引用 Adapter 程序集：会让 Shared 单端化，失去意义
- ✗ Plugin DLL 路线替代源引用：调试体验差是已知红线

## Future Work（V1.1+）

| 项 | 何时 | 备注 |
|----|------|------|
| Unity asmdef 自动配 Shared 引用脚本 | V1.1 | 提供 Editor 工具一键接入 |
| 真业务 Aspect / 网络消息填入 Shared | V1.2 真 demo | 见 V1.0 PRD §6 Future Work |
| Shared 内 Aspect 跨端测试套件 | V1.2 | Net 端 + Unity PlayMode 双端跑同一 Aspect 测试 |

## 关联

- `docs/design/V1.0-architecture-contracts.md`：V1.0 PRD §2.1 Samples/Shared 物理结构
- ADR-0011：ModuleHost / IModule 契约（Core "无业务知识"的隐含约束）
- ADR-0012：Shadow csproj 双端编译（Core 跨端的实现机制）
- ADR-0016：Adapter 层退出 Core 范围（V1.0 Shared 是 Adapter 退场后留下的"业务跨端"空洞的填补）
- ReferenceFramework/hsenl：Network 完全独立 asmdef（V1.0 网络层抽象的参考之一）
- ReferenceFramework/BigCat/Wjybxx.BigCat.Core：三端 csproj 拆分模式
