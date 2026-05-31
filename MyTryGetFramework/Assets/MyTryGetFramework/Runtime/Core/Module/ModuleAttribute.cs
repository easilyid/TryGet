using System;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：标记一个 class 为 Module，让 Source Generator 自动生成注册代码。
    ///
    /// 使用：
    /// <code>
    /// [Module(typeof(ILogger))]
    /// public sealed class ConsoleLogger : ILogger { ... }
    /// </code>
    ///
    /// Generator 扫描所有 <see cref="ModuleAttribute"/> 标记的类，生成
    /// <c>__AssemblyManifest_&lt;asm&gt;</c> 内部 static 类，在 <c>[ModuleInitializer]</c> /
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 触发时把所有 Module 注册到 <see cref="ModuleRegistry"/>。
    ///
    /// <para>约束：</para>
    /// - 类必须实现 <typeparamref name="ServiceType"/> 指定的服务接口
    /// - 类必须有公共无参构造器（Generator 用 <c>new T()</c> 实例化）
    /// - <typeparamref name="ServiceType"/> 必须是 interface，不能是 Module 自身具体类型
    ///   （与 <see cref="IModuleSystem.Register{T}"/> 约束一致）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModuleAttribute : Attribute
    {
        /// <summary>Module 实现的服务接口（如 <c>typeof(ILogger)</c>）。</summary>
        public Type ServiceType { get; }

        public ModuleAttribute(Type serviceType)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        }
    }
}
