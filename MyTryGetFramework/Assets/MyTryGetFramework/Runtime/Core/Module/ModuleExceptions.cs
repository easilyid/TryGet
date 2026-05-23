using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// Module 已注册同一接口时抛出（ADR-0011 §4）。
    /// </summary>
    public sealed class ModuleAlreadyRegisteredException : InvalidOperationException
    {
        public Type InterfaceType { get; }

        public ModuleAlreadyRegisteredException(Type interfaceType)
            : base($"Module of interface {interfaceType.Name} already registered.")
        {
            InterfaceType = interfaceType;
        }
    }

    /// <summary>
    /// Get/TryGet 时目标 Module 未注册抛出。
    /// </summary>
    public sealed class ModuleNotRegisteredException : InvalidOperationException
    {
        public Type InterfaceType { get; }

        public ModuleNotRegisteredException(Type interfaceType)
            : base($"Module of interface {interfaceType.Name} not registered.")
        {
            InterfaceType = interfaceType;
        }
    }

    /// <summary>
    /// 拓扑排序检测到循环依赖抛出（ADR-0011 §4）。
    /// </summary>
    public sealed class ModuleCircularDependencyException : InvalidOperationException
    {
        public IReadOnlyList<string> InvolvedModules { get; }

        public ModuleCircularDependencyException(IReadOnlyList<string> involvedModules)
            : base("Circular dependency detected among Modules: " + string.Join(", ", involvedModules))
        {
            InvolvedModules = involvedModules;
        }
    }

    /// <summary>
    /// Module 声明的 DependsOn 引用了未注册的接口类型时抛出。
    /// </summary>
    public sealed class ModuleDependencyMissingException : InvalidOperationException
    {
        public Type DependentModuleType { get; }
        public Type MissingInterfaceType { get; }

        public ModuleDependencyMissingException(Type dependentModuleType, Type missingInterfaceType)
            : base($"Module {dependentModuleType.Name} declares dependency on {missingInterfaceType.Name}, but no such Module is registered.")
        {
            DependentModuleType = dependentModuleType;
            MissingInterfaceType = missingInterfaceType;
        }
    }

    /// <summary>
    /// Shutdown 过程中一个或多个 Module 抛出异常时，聚合所有异常抛出。
    /// </summary>
    public sealed class ModuleShutdownException : AggregateException
    {
        public ModuleShutdownException(IEnumerable<Exception> innerExceptions)
            : base("One or more Modules threw during Shutdown. See InnerExceptions.", innerExceptions)
        {
        }
    }
}
