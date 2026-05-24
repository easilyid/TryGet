namespace TryGet
{
    /// <summary>
    /// 网络消息基础标记接口（V1.0 起）。
    ///
    /// 业务定义自己的消息：
    /// <code>
    /// public readonly struct MoveRequest : INetMessage { public float X, Y; }
    /// public readonly struct MoveBroadcast : INetMessage { public long Id; public float X, Y; }
    /// </code>
    ///
    /// **约定**：
    /// - 推荐 <c>readonly struct</c>（值类型 + 不可变），降低 GC 压力
    /// - Core 不提供具体序列化；序列化由 V1.1+ Adapter（MemoryPack / Protobuf 等）实现
    /// - Shared 业务消息建议放 <c>Samples/Shared/*.cs</c>，跨端共享
    ///
    /// 与 V0.6 IEventBus 事件 (<c>struct</c>) 的区别：
    /// - <see cref="IEventBus"/> 事件 = 进程内通信
    /// - <see cref="INetMessage"/> = 跨进程通信，必须可序列化
    /// </summary>
    public interface INetMessage { }
}
