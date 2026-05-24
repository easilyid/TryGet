using System;

namespace TryGet
{
    /// <summary>
    /// 序列化服务契约（V0.8 起新增）。
    ///
    /// Core 内**不提供实现** — 避免引入 NuGet 依赖（System.Text.Json / Newtonsoft.Json / MemoryPack 等）
    /// 破坏 Shadow csproj 跨端纯净（0 警告 0 错误）。真实实现由 Adapter 提供：
    /// - V1.1+ `MemoryPackSerializer`（Cysharp/MemoryPack，AOT-safe，zero-encoding）
    /// - V1.1+ `JsonSerializer`（System.Text.Json，文本可读）
    /// - V1.1+ `MessagePackSerializer`（业界事实标准）
    ///
    /// 使用模式：
    /// <code>
    /// var serializer = host.Get&lt;ISerializer&gt;();
    /// byte[] data = serializer.Serialize(myObject);
    /// var restored = serializer.Deserialize&lt;MyClass&gt;(data);
    /// </code>
    ///
    /// 与 V0.8 其他抽象的关系：
    /// - <see cref="IKVStore"/> + Memory 实现：**绕过** ISerializer（直接持 object）
    /// - <see cref="IKVStore"/> + PlayerPrefs / 文件 Adapter：用 ISerializer 把 T 转 byte[]
    /// - <see cref="IConfigSource"/>：只持 byte[]；<see cref="ConfigLoader{T}"/> 用业务提供的反序列化函数（不强制 ISerializer）
    /// - <see cref="IAssetSource"/> + Memory 实现：**绕过** ISerializer；YooAsset Adapter 由 YooAsset 自己反序列化
    ///
    /// 测试覆盖：Tests 内 <c>MockBytesSerializer</c>（仅测试用，把 obj.ToString() 转 UTF-8 bytes）。
    /// </summary>
    public interface ISerializer : IModule
    {
        /// <summary>查询此 Serializer 是否支持给定类型。不支持时 Serialize/Deserialize 抛异常。</summary>
        bool IsSupported(Type type);

        /// <summary>序列化为 byte[]。不支持的类型抛 <see cref="NotSupportedException"/>。</summary>
        byte[] Serialize<T>(T value);

        /// <summary>序列化为 byte[]（非泛型重载）。不支持的类型抛 <see cref="NotSupportedException"/>。</summary>
        byte[] Serialize(Type type, object value);

        /// <summary>反序列化。不支持类型或数据格式错误抛 <see cref="InvalidOperationException"/>。</summary>
        T Deserialize<T>(byte[] data);

        /// <summary>反序列化（非泛型重载）。</summary>
        object Deserialize(Type type, byte[] data);
    }
}
