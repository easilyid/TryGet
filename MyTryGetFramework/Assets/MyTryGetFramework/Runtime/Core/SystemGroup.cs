using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// World 内用于组织 System 执行顺序的调度分组。
    /// SystemGroup 是纯调度组，不成为领域边界或模块系统 (ADR-0006)。
    /// SystemGroup 内的执行顺序 = 注册顺序 (ADR-0008)。
    /// </summary>
    public sealed class SystemGroup
    {
        private readonly string _name;
        private readonly List<SystemBase> _systems = new List<SystemBase>();

        public SystemGroup(string name)
        {
            _name = name ?? "Default";
        }

        /// <summary>
        /// 此 SystemGroup 的名称（用于调试和日志）。
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// 此 SystemGroup 中注册的 System 列表（只读）。
        /// </summary>
        public IReadOnlyList<SystemBase> Systems => _systems;

        /// <summary>
        /// 框架内部：添加 System 到此 Group。
        /// </summary>
        internal void AddSystem(SystemBase system)
        {
            _systems.Add(system);
        }

        /// <summary>
        /// 框架内部：移除 System。
        /// </summary>
        internal bool RemoveSystem(SystemBase system)
        {
            return _systems.Remove(system);
        }

        public override string ToString()
        {
            return $"SystemGroup({_name}, count={_systems.Count})";
        }
    }
}
