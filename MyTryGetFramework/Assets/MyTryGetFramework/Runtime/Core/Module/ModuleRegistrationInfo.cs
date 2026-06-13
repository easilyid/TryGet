using System;

namespace TryGet
{
    /// <summary>
    /// C5 — ModuleRegistry 诊断快照条目。
    ///
    /// 替代当前 <see cref="ModuleRegistry.Snapshot"/> 返回不透明 <c>Action&lt;IModuleSystem&gt;</c> 的设计，
    /// 改为返回结构化信息，支持调试时查看"注册了哪些 Module、来自哪个 assembly"。
    /// </summary>
    public readonly struct ModuleRegistrationInfo
    {
        /// <summary>实现类型（标记了 <see cref="ModuleAttribute"/> 的具体类）。</summary>
        public readonly Type ImplementationType;

        /// <summary>服务接口（ModuleAttribute 的 serviceType 参数）。</summary>
        public readonly Type ServiceType;

        /// <summary>
        /// 来源 assembly 名称（生成的 manifest 类所在 assembly，例 "TryGet.Samples.Net"）。
        /// 用于多 assembly / 热更层诊断，null 表示未知来源（手动注册或旧生成代码）。
        /// </summary>
        public readonly string SourceAssembly;

        public ModuleRegistrationInfo(Type implementationType, Type serviceType, string sourceAssembly)
        {
            ImplementationType = implementationType;
            ServiceType = serviceType;
            SourceAssembly = sourceAssembly;
        }

        public override string ToString() =>
            $"{ImplementationType?.Name} as {ServiceType?.Name} (from {SourceAssembly ?? "unknown"})";
    }
}
