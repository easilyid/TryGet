using System;

namespace TryGet
{
    /// <summary>
    /// V1.0 起 — 网络层连接级 IPlugPoint 5 件套（与 V0.9 IPlugin/IPluginHost/IPlugPoint 集成）。
    ///
    /// 与 <c>IModuleHostBeforeUpdate</c> / <c>IModuleHostAfterUpdate</c>（全局横切）的区别：
    /// 本文件 5 个 IPlugPoint 是**连接级横切**（每个网络生命周期事件触发一次）。
    /// 作用域更窄，业务通常用来挂：流量监控、加密 / 解密、重连策略、心跳、协议升级等。
    ///
    /// Adapter 实现负责在合适时机触发对应插件（通过 <see cref="IPluginHost.GetPluginsAt{TPoint}"/>）。
    ///
    /// 参考：hsenl Network IPlug 7 件套（<c>IOnChannelStarted</c> / <c>IOnRecvData</c> 等），
    /// TryGet 简化为 5 件套（去掉 <c>IBeforeMessageReaded</c> / <c>IAfterMessageWrited</c> 等细颗粒事件；
    /// 业务多数场景用不到，V1.x 评估再加）。
    /// </summary>
    public interface IOnConnectionStarted : IPlugPoint
    {
        /// <summary>客户端 / 服务端连接建立完成时触发（含握手 / 认证完成）。</summary>
        void OnConnectionStarted(IConnection connection);
    }

    public interface IOnConnectionClosed : IPlugPoint
    {
        /// <summary>
        /// 连接关闭时触发（含主动 close、对端断开、网络异常）。
        /// <paramref name="reason"/> 标明最后已知状态（Disconnecting 表示主动、Faulted 表示异常）。
        /// </summary>
        void OnConnectionClosed(IConnection connection, ConnectionState reason);
    }

    public interface IOnRawDataReceived : IPlugPoint
    {
        /// <summary>
        /// 收到原始字节（反序列化前）。常用于流量监控 / 加密解密 / 抓包等。
        /// <para>注意：<paramref name="data"/> 是 framework 临时 buffer，业务不可持有引用跨调用。</para>
        /// </summary>
        void OnRawDataReceived(IConnection connection, ReadOnlySpan<byte> data);
    }

    public interface IOnMessageReceived : IPlugPoint
    {
        /// <summary>反序列化后业务 <see cref="INetMessage"/> 可见时触发。</summary>
        void OnMessageReceived(IConnection connection, INetMessage message);
    }

    public interface IOnNetError : IPlugPoint
    {
        /// <summary>网络错误时触发（Adapter catch 内部异常转此事件）。</summary>
        void OnNetError(IConnection connection, Exception error);
    }
}
