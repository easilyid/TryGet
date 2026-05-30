namespace TryGet
{
    /// <summary>
    /// 客户端网络消息基础标记接口。
    ///
    /// 业务定义自己的消息：
    /// <code>
    /// public readonly struct MoveRequest : INetMessage { public float X, Y; }
    /// public readonly struct MoveBroadcast : INetMessage { public long Id; public float X, Y; }
    /// </code>
    ///
    /// **约定**：
    /// - 推荐 <c>readonly struct</c>（值类型 + 不可变），降低 GC 压力
    /// - Core 不提供具体序列化；序列化由 Adapter（MemoryPack / Protobuf 等）实现
    /// - Core 仅定义客户端消息边界，不包含服务端连接模型
    ///
    /// 与 <see cref="IEventBus"/> 事件的区别：
    /// - <see cref="IEventBus"/> 事件 = 进程内模块通信
    /// - <see cref="INetMessage"/> = 网络通信载荷，必须由 Adapter 可序列化
    /// </summary>
    public interface INetMessage { }
}
