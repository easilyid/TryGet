# Network / Hot-reload 通过 Adapters/ 抽象

## Status

**Superseded by ADR-0016** (2026/05/24)

围绕 Adapter 层的设计已整体退出 Core 范围（见 ADR-0016 + `docs/strategy/V2-direction-pivot.md`）。
Network / HotReload 在 V1.1+ 真做时重写本 ADR。

原 Accepted 内容保留以供历史参考：

## Context

V2 设计文档（见 `.scratch/framework-design-v2/design.md` §1、§8、§9、§10）将网络与热更明确为"接口在 Core/Common，实现在 Adapters/"。这里要把决策落成 ADR。

**网络方案的产业现状（2026）：**
- **Mirror**：开源、社区活跃、Unity 客户端事实标准。
- **Photon Fusion 2**：商业方案、可信、贵。
- **FishNet**：开源、性能口碑好。
- **Kcp / 自家 TCP**：底层方案，作为传输层备选。
- **ET / Fantasy 自带网络层**：方案完整但与具体框架深耦合，不可直接复用。

**热更新方案：**
- **HybridCLR**：当前 Unity IL2CPP 下唯一成熟的全 C# 热更方案，V2 的默认实现。
- **CoreCLR for Unity**：远期 Unity 官方方向，2026 仍在演进，可能逐步替代 HybridCLR。
- **Lua（xLua / sLua）**：脚本语言路线，与 V2 全 C# 定位冲突，不予支持。

如果框架把 Mirror 或 HybridCLR 的具体类型烤进 Core，未来换方案就要伤筋动骨。商业项目还经常有"我们公司只能用 Photon"的硬约束，框架硬绑会直接劝退。

## Decision

网络层与热更层均采用"接口在内、实现在 Adapter"的抽象方式，Core/Network/Common 仅定义抽象契约，具体实现走独立 `Adapters/*` asmdef，可选启用、可换实现。

**网络抽象（详见 design.md §8）：**

```csharp
// Core / Network 中
public interface IChannel
{
    void Send(ArraySegment<byte> data);
    event Action<ArraySegment<byte>> OnReceived;
    void Close();
}

public interface IMessageBus
{
    Task<TResponse> RpcAsync<TRequest, TResponse>(TRequest request);
    void Send<TNotify>(TNotify notify);
    void Subscribe<TNotify>(Action<TNotify> handler);
}

public interface ISession { /* ... */ }
public interface ISessionManager { /* ... */ }

public interface IPipe
{
    Task ProcessAsync(PipeContext ctx, Func<PipeContext, Task> next);
}
```

**热更抽象（详见 design.md §9）：**

```csharp
public interface IHotfixLoader : IModule
{
    Task<Assembly> LoadHotfixAssemblyAsync(string name);
    Task<bool> CheckForUpdatesAsync();
    HotfixVersion CurrentVersion { get; }
}
```

**Adapter 落点：**

| Adapter asmdef | 实现接口 | 角色 |
|---|---|---|
| `Adapters/Mirror` | `IChannel` | 开源默认网络实现 |
| `Adapters/Kcp` | `IChannel` | 轻量传输层备选 |
| `Adapters/HybridCLR` | `IHotfixLoader` | V2 当前默认热更 |
| `Adapters/CoreCLR`（远期） | `IHotfixLoader` | Unity 官方路线就绪后替代 HybridCLR |

序列化默认走 `MemoryPack`，定义在 `Core/ISerializer`，同样通过接口可替换为 Newtonsoft.Json 等。

**配套纪律（design.md §13 关键纪律 #4、#5、#7）：**

1. Core / Network / Common 三个 asmdef 严禁 `using UnityEngine`、严禁 `using Mirror`、严禁 `using HybridCLR`。
2. `Adapters/*` 各自一个 asmdef，依赖具体三方库；项目按需引用，未启用的 Adapter 不进 Player Build。
3. 网络与热更的 Module 必须先有 `I{Name}Module` 接口（继承 ADR-0011 纪律），不允许 `Adapters/Mirror` 内部偷偷暴露 Mirror 类型给业务层。

## Consequences

**好处：**
- 业务代码只看 `IChannel` / `IMessageBus` / `IHotfixLoader`，不感知底层是 Mirror / Kcp / HybridCLR / CoreCLR，换方案改 Adapter 注册即可。
- 商业项目可自行实现 `Adapters/PhotonFusion`，无需 fork 框架。
- 热更技术演进（HybridCLR → CoreCLR）通过新增 Adapter 完成，框架根版本不破坏。
- 与 ADR-0012 的 Shadow csproj 配合，Network 接口本身可在服务端复用，只是 Adapter 选不同实现。

**成本：**
- 抽象层不是零成本：每写一个接口、每加一个 Adapter，多一层胶水代码。小项目"我就只用 Mirror"的体感是"为啥不让我直接 `using Mirror`"。
- 抽象设计本身需要预见性。`IMessageBus.RpcAsync<TReq, TResp>` 这种签名要兼容 Mirror 的 Command / Photon 的 RPC / Kcp 的裸消息三种语义，接口稳定性是个长期议题。
- `Adapters/HybridCLR` 加载热更 dll 后，`RuntimeTypeHandle` 变化可能影响 Aspect 反射缓存（design.md §附 未解决问题 #2），抽象层需考虑生命周期回调，让 Aspect 反射缓存有机会刷新。
- 多 Adapter 并存增加 CI 矩阵：每个核心 Adapter 都要有最小可用样例验证，否则"可替换"沦为纸面承诺。