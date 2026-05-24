using System;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// 服务端单个客户端连接抽象（V1.0 起）。
    ///
    /// 由 <see cref="INetServer"/> 在 <see cref="INetServer.OnClientConnected"/> 回调中分发。
    /// 业务持有 IConnection 引用作发送 / 关闭等操作；INetServer 内部追踪所有活跃 IConnection。
    /// </summary>
    public interface IConnection
    {
        /// <summary>此连接的唯一标识。</summary>
        ConnectionId Id { get; }

        /// <summary>当前连接状态。</summary>
        ConnectionState State { get; }

        /// <summary>
        /// 发送一条消息到客户端。具体序列化 / 拆包 / 协议由 <see cref="INetServer"/> 的 Adapter 实现决定。
        /// </summary>
        void Send<T>(T message) where T : INetMessage;

        /// <summary>主动关闭此连接（优雅）。</summary>
        TGTask CloseAsync();
    }

    /// <summary>
    /// 客户端连接抽象（V1.0 起）。
    ///
    /// 业务（通常是游戏 client）只持一个 INetClient 实例对应一条到服务端的逻辑通道。
    /// 具体协议（KCP / TCP / WebSocket）由 Adapter 决定，业务通过接口编程不依赖具体实现。
    ///
    /// 生命周期：
    /// 1. 构造 INetClient（典型由 <see cref="IModuleHost"/> 拿对应 Module）
    /// 2. <see cref="ConnectAsync"/> 异步连接服务端
    /// 3. 运行期：<see cref="Send{T}"/> 发送消息；订阅 <see cref="OnMessageReceived"/> / <see cref="OnStateChanged"/>
    /// 4. <see cref="DisconnectAsync"/> 优雅关闭 或 Dispose 强制关闭
    /// </summary>
    public interface INetClient : IDisposable
    {
        /// <summary>本端连接标识（同一进程多 INetClient 实例时区分）。</summary>
        ConnectionId Id { get; }

        /// <summary>当前连接状态。</summary>
        ConnectionState State { get; }

        /// <summary>
        /// 异步连接服务端。<c>State == Disconnected</c> 时合法；其他状态抛
        /// <see cref="System.InvalidOperationException"/>。
        /// </summary>
        TGTask ConnectAsync(string host, int port);

        /// <summary>异步优雅断开。<c>State == Connected</c> 时合法；其他状态静默 return。</summary>
        TGTask DisconnectAsync();

        /// <summary>
        /// 发送一条消息到服务端。具体序列化由 Adapter 决定。
        /// <c>State != Connected</c> 时抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void Send<T>(T message) where T : INetMessage;

        /// <summary>连接状态变化时触发。</summary>
        event Action<ConnectionState> OnStateChanged;

        /// <summary>收到服务端消息时触发。</summary>
        event Action<INetMessage> OnMessageReceived;

        /// <summary>网络错误时触发（Adapter 实现自己 catch 异常并转此事件）。</summary>
        event Action<Exception> OnError;
    }
}
