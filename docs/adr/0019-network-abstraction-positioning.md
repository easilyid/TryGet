# ADR-0019: 网络抽象层定位 — Core 持契约，Adapter 持实现

## Status

Accepted（V1.0 起生效）

## Context

V0.1-V0.9.5 期间 TryGet Core 完全没有网络层：
- Core 是"框架"层，提供 Bootstrap / Module / Aspect / Event / Async 等通用机制
- Network / Hotfix / Asset Adapter 在 V0.4 期间 ADR-0014 曾尝试纳入 Core，2026/05/24 用户战略反馈后 ADR-0016 整体退出 Core

V1.0 推进时遇到核心问题：**业务侧业务（V1.1+ MMO demo 等）需要在两端共享网络协议接口**。需求场景：
- Net 端业务：`Send<MoveRequest>(...)` / `OnMessageReceived` 订阅
- Unity 端业务：同样 `Send / OnMessageReceived`
- 实际网络协议（KCP / TCP / WebSocket / LiteNetLib）切换业务不感知
- 网络生命周期事件（连接建立 / 断开 / 收原始字节 / 错误）可插中间件（流量监控、加密、重连）

**矛盾**：Core 不能含具体协议实现（违反"无外部依赖"约束），但业务又需要 Core 级的接口规范。

## Decision

**Core 持网络契约 + Adapter 持实现的分层**（V1.0 落地范围）：

### 1. Core 提供的契约（V1.0 Iter 2 落地）

放 `Runtime/Core/Net/`：

| 类型 | 性质 | 内容 |
|------|------|------|
| `INetClient` | 接口 | 客户端连接（Connect / Disconnect / Send / 3 events） |
| `INetServer` | 接口 | 服务端监听（Start / Stop / Connections / 4 events） |
| `IConnection` | 接口 | 服务端单连接（Id / State / Send / CloseAsync） |
| `INetMessage` | marker 接口 | 网络消息基础（业务 struct 实现） |
| `ConnectionId` | readonly struct | 连接标识 |
| `ConnectionState` | enum | 5 状态（Disconnected/Connecting/Connected/Disconnecting/Faulted） |
| `IOnConnectionStarted/Closed` `IOnRawDataReceived` `IOnMessageReceived` `IOnNetError` | IPlugPoint 派生 | 5 个网络生命周期 IPlugPoint（与 V0.9 IPlugin 集成） |

### 2. Adapter 提供的实现（V1.1+ 范围）

放 `Adapters/Network/`（独立 csproj，独立 nuget 依赖）：

| Adapter | 协议 | 依赖 |
|--------|------|------|
| `KcpNetClient / KcpNetServer` | KCP（UDP 可靠） | KCP C# port nuget |
| `LiteNetLibNetClient / LiteNetLibNetServer` | LiteNetLib | LiteNetLib nuget |
| `TcpNetClient / TcpNetServer` | TCP | .NET 标准库 |
| `WebSocketNetClient` | WebSocket | System.Net.WebSockets |

### 3. 序列化的分层

INetMessage 是 marker only —**Core 不负责序列化**。
- `ISerializer`（V0.8）已是 Core 契约（IsSupported / Serialize / Deserialize）
- 业务用 INetMessage 实现自己的消息 struct
- Adapter 内部用 `ISerializer` 把 INetMessage 转字节流
- Adapter Constructor 接收 ISerializer 注入（典型用 `MemoryPackSerializer` Adapter）

### 4. 与 hsenl Channel/Service 设计的差异

hsenl 抽象更深（`Channel` 持 `OnMessageReadedEvent` / `OnMessageWritedEvent` 等 buffer-level event），`Service` 是 Channel 容器。

TryGet 决策（更轻）：
- 不暴露 buffer-level event 到 Core 契约（业务多数场景不需要）
- INetServer 直接管 IConnection 集合（不引入 Service 中间层）
- 业务 message-level 关注通过 `OnMessageReceived` event；底层 buffer 关注通过 `IOnRawDataReceived` IPlugPoint 切入

### 5. IPlugPoint 与 V0.9 ModuleHost 横切的关系

| 层 | IPlugPoint 示例 | 触发频率 | 作用域 |
|---|---|---|---|
| 全局横切（V0.9 落地） | `IModuleHostBeforeUpdate` / `IModuleHostAfterUpdate` | 每帧 1 次 | 整个 host |
| 连接级横切（V1.0 新增） | `IOnConnectionStarted` / `IOnRawDataReceived` 等 5 件 | 每个连接事件 | 单个 IConnection |

业务用同一套 IPlugin 机制（`host.AddPlugin<IOnConnectionStarted, MyMonitorPlugin>(...)`）跨两类作用域。Adapter 内部触发对应插件通过 `host.GetPluginsAt<TPoint>()` 枚举。

## Consequences

### 好处

- **Core 边界整洁**：不含 KCP / LiteNetLib 等具体协议依赖，跨端编译零外部 nuget
- **业务接口稳定**：换 KCP ↔ LiteNetLib 不动业务代码（同 INetClient/Server）
- **Plug 机制复用**：V0.9 已成熟的 IPlugin/IPluginHost 机制无缝服务网络层；业务挂中间件无侵入
- **与 hsenl/ET/Fantasy 对齐**：三框架都是"抽象 + 多协议实现"思路，TryGet 简化了 hsenl Channel 抽象但保留 IPlug 价值
- **Adapter 独立演进**：V1.1 KCP Adapter / V1.2 LiteNetLib Adapter / V1.3 WebSocket Adapter 可独立迭代

### 风险

- **契约定得太死风险**（IPlugPoint 5 件 / INetClient/Server 7+ event）：V1.1+ Adapter 真做时若发现遗漏，需要慎重补充避免破坏性变更
  - 缓解：V1.0 仅契约不冻结，V1.1 Adapter 落地后再考虑 V1.2 契约升级
- **业务可能直接用 ConnectionId 当作 Entity-related 标识**导致跨 EntityWorld 引用泄露
  - 缓解：文档明确 ConnectionId 仅"网络层不透明 token"，业务自己维护 ConnectionId ↔ Entity 映射
- **连接级 IPlugPoint 触发性能**（高 QPS 时每个消息触发 hook）
  - 缓解：Adapter 实现自带 fast-path（"无 plugin 注册时不进入 GetPluginsAt 枚举循环"）
- **INetMessage 是空 marker** 容易让业务把任意类型当消息
  - 缓解：V1.1+ 配合 [NetMessage] Source Generator 加编译期校验

### 不允许的退路

- ✗ 把 KCP / LiteNetLib 实现塞进 Core：违反 Core "无外部 nuget" 约束（ADR-0011 / ADR-0016 双重重申）
- ✗ 不通过 IPlugPoint 而直接给 INetClient/Server 加事件：会让 Core 接口臃肿
- ✗ 在 INetMessage 内塞序列化方法：Adapter 关注点污染 Core 契约

## Future Work（V1.1+）

| 项 | 何时 | 备注 |
|----|------|------|
| `Adapters/Network.Kcp/` | V1.1 | 首个 Adapter，验证契约可用性 |
| `Adapters/Network.LiteNetLib/` | V1.1 后期 | 验证多 Adapter 切换 |
| [NetMessage] Source Generator | V1.1 或 V1.2 | 编译期校验 INetMessage 必须 readonly struct + MemoryPack-friendly |
| 心跳 / 重连 Plugin（业务复用） | V1.2 | Plug 机制示范实现 |
| Actor / MailBox / Location（参考 ET） | V2.0+ | 高层抽象 |

## 关联

- `docs/design/V1.0-architecture-contracts.md`：V1.0 PRD §2.2 INetClient / INetServer 详细 API
- ADR-0011：ModuleHost / IModule 契约（Core 边界基础）
- ADR-0012：Shadow csproj 双端编译（Core 跨端基础）
- ADR-0014：Network/HotReload Adapter 抽象（Superseded by ADR-0016；本 ADR-0019 是 V1.0 的重新定位）
- ADR-0016：Adapter 层退出 Core 范围（本 ADR 在 ADR-0016 边界下决定"接口可以放 Core，实现不行"）
- ADR-0018：Samples/Shared 跨端共享代码（业务网络消息 struct 放 Shared 的位置）
- ReferenceFramework/hsenl Network/Common/Channel：抽象设计参考
- V0.9 ADR-0017 IPlugin/IPluginHost/IPlugPoint：本 ADR 网络生命周期事件复用此机制
