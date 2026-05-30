using System;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// 客户端网络连接契约（V2.0 简化版）。
    ///
    /// 业务持一个 INetClient 实例对应一条到服务端的逻辑通道。
    /// 具体协议（KCP / TCP / WebSocket）由 Adapter 决定，业务通过接口编程不依赖具体实现。
    /// </summary>
    public interface INetClient : IDisposable
    {
        bool IsConnected { get; }

        TGTask ConnectAsync(string host, int port);
        TGTask DisconnectAsync();

        void Send<T>(T message) where T : INetMessage;

        event Action<INetMessage> MessageReceived;
        event Action<Exception> ErrorOccurred;
        event Action Connected;
        event Action Disconnected;
    }
}
