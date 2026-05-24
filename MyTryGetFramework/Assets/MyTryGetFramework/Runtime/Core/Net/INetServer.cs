using System;
using System.Collections.Generic;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// 服务端监听抽象（V1.0 起）。
    ///
    /// 一个 INetServer 监听单个 (host, port) 端口，接受多个客户端 <see cref="IConnection"/>。
    /// 具体协议（KCP / TCP / WebSocket）由 Adapter 决定。
    ///
    /// 生命周期：
    /// 1. 构造 INetServer
    /// 2. <see cref="StartAsync"/> 异步绑定端口 + 开始 accept
    /// 3. 运行期：订阅 <see cref="OnClientConnected"/> / <see cref="OnClientDisconnected"/> / <see cref="OnMessageReceived"/>
    /// 4. <see cref="StopAsync"/> 或 Dispose 停止
    /// </summary>
    public interface INetServer : IDisposable
    {
        /// <summary>
        /// 异步启动监听。
        /// 已 start 时再调抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        TGTask StartAsync(string bindAddress, int port);

        /// <summary>异步停止监听 + 关所有现有连接。允许多次调用（仅首次有效）。</summary>
        TGTask StopAsync();

        /// <summary>所有当前活跃的客户端连接（只读快照视图，Adapter 实现负责并发安全）。</summary>
        IReadOnlyCollection<IConnection> Connections { get; }

        /// <summary>新客户端连接建立时触发（在认证 / 握手完成之后）。</summary>
        event Action<IConnection> OnClientConnected;

        /// <summary>客户端连接断开时触发（含主动关闭、对端断开、网络异常等）。</summary>
        event Action<IConnection> OnClientDisconnected;

        /// <summary>收到客户端消息时触发。<paramref name="connection"/> 标明来源。</summary>
        event Action<IConnection, INetMessage> OnMessageReceived;

        /// <summary>网络错误时触发（与具体 IConnection 无关的服务级错误，如 bind 失败）。</summary>
        event Action<Exception> OnError;
    }
}
