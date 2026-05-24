using System;

namespace TryGet
{
    /// <summary>
    /// 网络连接的统一标识（V1.0 起）。
    ///
    /// Adapter 实现自行选择编码：可以是 socket fd / 单调递增 long / GUID 截短。
    /// 业务侧只把它当不透明 token 用，不解析其内部结构。
    /// </summary>
    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        public readonly long Value;

        public ConnectionId(long value) { Value = value; }

        public static ConnectionId None => default;
        public bool IsNone => Value == 0;

        public bool Equals(ConnectionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ConnectionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(ConnectionId a, ConnectionId b) => a.Value == b.Value;
        public static bool operator !=(ConnectionId a, ConnectionId b) => a.Value != b.Value;

        public override string ToString() => $"ConnectionId({Value})";
    }

    /// <summary>
    /// 网络连接生命周期状态。
    ///
    /// 状态流转（典型）：
    /// <c>Disconnected → Connecting → Connected → Disconnecting → Disconnected</c>
    /// 异常分支：<c>* → Faulted</c>（不可恢复，业务一般丢弃此 INetClient 重新构造）
    /// </summary>
    public enum ConnectionState
    {
        Disconnected = 0,
        Connecting,
        Connected,
        Disconnecting,
        Faulted,
    }
}
