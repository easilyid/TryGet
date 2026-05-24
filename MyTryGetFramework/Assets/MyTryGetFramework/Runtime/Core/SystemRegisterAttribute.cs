using System;

namespace TryGet
{
    /// <summary>
    /// V0.9.5 起：标记一个 <see cref="SystemBase"/> 子类，让 Source Generator 自动注册到
    /// <see cref="EntityWorld"/> 的指定 <see cref="Phase"/> 和 SystemGroup。
    ///
    /// 使用：
    /// <code>
    /// [SystemRegister(Phase.Update)]
    /// public sealed class MovementSystem : SystemBase { ... }
    ///
    /// [SystemRegister(Phase.Update, groupName: "Physics")]
    /// public sealed class PhysicsSystem : SystemBase { ... }
    /// </code>
    ///
    /// Generator 扫描所有标记类，按 assembly 分组生成 <c>__SystemManifest_&lt;asm&gt;</c>，
    /// 该类在 <c>[ModuleInitializer]</c> / <c>[RuntimeInitializeOnLoadMethod]</c> 触发时
    /// 把所有 System 注册到 <see cref="SystemRegistry"/>。业务在创建 <see cref="EntityWorld"/>
    /// 后调 <see cref="SystemRegistry.ApplyAll"/> 应用。
    ///
    /// 约束：
    /// - 标记的类必须继承 <see cref="SystemBase"/>
    /// - 必须有公共无参构造器（Generator 用 <c>new T()</c> 实例化）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SystemRegisterAttribute : Attribute
    {
        /// <summary>System 所属 Phase（Enter/Update/Exit）。</summary>
        public Phase Phase { get; }

        /// <summary>System 所属 SystemGroup 名（null 或 "Default" 进默认组）。</summary>
        public string GroupName { get; }

        public SystemRegisterAttribute(Phase phase, string groupName = null)
        {
            Phase = phase;
            GroupName = groupName;
        }
    }
}
