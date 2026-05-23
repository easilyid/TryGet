using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 框架根容器实现（ADR-0011）。
    ///
    /// 迭代 0 范围：Register / Get / TryGet / EventBus。
    /// 迭代 1 范围：Initialize（拓扑排序 + OnInit）/ Shutdown（逆序）。
    /// 后续迭代将引入 Update / LateUpdate。
    ///
    /// 非线程安全：所有方法须在主线程调用。
    /// </summary>
    public sealed class ModuleHost : IModuleHost
    {
        private static readonly HashSet<Type> ForbiddenRegistrationTypes = new HashSet<Type>
        {
            typeof(IModule),
            typeof(IUpdateModule),
            typeof(ILateUpdateModule),
            typeof(IEventBus),
        };

        private readonly Dictionary<Type, IModule> _modulesByInterface = new Dictionary<Type, IModule>();
        private readonly IEventBus _eventBus = new WorldEventBus();

        // 拓扑排序后的初始化顺序，Shutdown 用它的逆序。
        private List<IModule> _initOrder;

        // 缓存 IUpdateModule / ILateUpdateModule 实例（按 Initialize 顺序）避免每帧 OfType 过滤。
        private List<IUpdateModule> _updateModules;
        private List<ILateUpdateModule> _lateUpdateModules;

        private bool _initialized;

        public IEventBus EventBus => _eventBus;
        public bool IsInitialized => _initialized;

        #region Registration

        public void Register<T>(T module) where T : class, IModule
        {
            if (_initialized)
                throw new InvalidOperationException(
                    "Cannot Register Module after Initialize. Register all Modules first, then Initialize.");

            if (module == null)
                throw new ArgumentNullException(nameof(module));

            Type key = typeof(T);
            if (!key.IsInterface)
                throw new ArgumentException(
                    $"Register<T> must use the Module's service interface (e.g. ILogModule), not its concrete type ({key.Name}).");

            if (ForbiddenRegistrationTypes.Contains(key))
                throw new ArgumentException(
                    $"Cannot register against framework base interface {key.Name}. " +
                    "Use the Module's own service interface (e.g. ILogModule).");

            if (_modulesByInterface.ContainsKey(key))
                throw new InvalidOperationException(
                    $"Module of interface {key.Name} already registered.");

            _modulesByInterface[key] = module;
        }

        public T Get<T>() where T : class, IModule
        {
            if (_modulesByInterface.TryGetValue(typeof(T), out IModule module))
                return (T)module;

            throw new InvalidOperationException(
                $"Module of interface {typeof(T).Name} not registered.");
        }

        public bool TryGet<T>(out T module) where T : class, IModule
        {
            if (_modulesByInterface.TryGetValue(typeof(T), out IModule found))
            {
                module = (T)found;
                return true;
            }

            module = null;
            return false;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 按 DependsOn 拓扑序（Priority 作 tie-breaker）依次 OnInit 所有 Module。
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("ModuleHost already initialized.");

            _initOrder = TopologicalSort();

            // 去重：同一实例通过多接口注册时，只 OnInit 一次。
            var initialized = new HashSet<IModule>();
            _updateModules = new List<IUpdateModule>();
            _lateUpdateModules = new List<ILateUpdateModule>();

            foreach (var module in _initOrder)
            {
                if (initialized.Add(module))
                {
                    module.OnInit(this);

                    if (module is IUpdateModule um)
                        _updateModules.Add(um);
                    if (module is ILateUpdateModule lum)
                        _lateUpdateModules.Add(lum);
                }
            }

            _initialized = true;
        }

        /// <summary>
        /// 帧 Update。按 OnInit 顺序调用所有 <see cref="IUpdateModule"/>。
        /// </summary>
        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("Update requires Initialize first.");

            for (int i = 0; i < _updateModules.Count; i++)
            {
                _updateModules[i].Update(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 帧 LateUpdate。按 OnInit 顺序调用所有 <see cref="ILateUpdateModule"/>。
        /// </summary>
        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("LateUpdate requires Initialize first.");

            for (int i = 0; i < _lateUpdateModules.Count; i++)
            {
                _lateUpdateModules[i].LateUpdate(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 按 Initialize 逆序 Shutdown 所有 Module。允许多次调用。
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
                return;

            var shutdown = new HashSet<IModule>();
            for (int i = _initOrder.Count - 1; i >= 0; i--)
            {
                var module = _initOrder[i];
                if (shutdown.Add(module))
                {
                    module.Shutdown();
                }
            }

            _initialized = false;
            _initOrder = null;
            _updateModules = null;
            _lateUpdateModules = null;
        }

        #endregion

        #region Topological Sort

        /// <summary>
        /// 基于 Kahn 算法的拓扑排序。Priority 作 tie-breaker（同一拓扑层级内按 Priority 升序）。
        /// 检测循环依赖和未注册依赖，发现时抛 InvalidOperationException。
        /// </summary>
        private List<IModule> TopologicalSort()
        {
            // 去重：同实例多接口只算一次节点
            var allModules = new List<IModule>();
            var seen = new HashSet<IModule>();
            foreach (var kv in _modulesByInterface)
            {
                if (seen.Add(kv.Value))
                    allModules.Add(kv.Value);
            }

            // 入度统计：以"接口类型"为节点
            // module → 它依赖的其他 module 实例
            var dependsOn = new Dictionary<IModule, List<IModule>>();
            var dependedBy = new Dictionary<IModule, List<IModule>>();

            foreach (var module in allModules)
            {
                dependsOn[module] = new List<IModule>();
                dependedBy[module] = new List<IModule>();
            }

            foreach (var module in allModules)
            {
                foreach (var depType in module.DependsOn)
                {
                    if (!_modulesByInterface.TryGetValue(depType, out var depModule))
                    {
                        throw new InvalidOperationException(
                            $"Module {module.GetType().Name} declares dependency on {depType.Name}, but no such Module is registered.");
                    }

                    if (depModule == module)
                    {
                        throw new InvalidOperationException(
                            $"Module {module.GetType().Name} declares dependency on itself ({depType.Name}).");
                    }

                    // 同实例多接口注册：跳过自依赖（来自不同接口指向同实例）
                    if (!dependsOn[module].Contains(depModule))
                    {
                        dependsOn[module].Add(depModule);
                        dependedBy[depModule].Add(module);
                    }
                }
            }

            // Kahn 算法：入度 0 的节点入 ready 队列，按 Priority 排序
            var ready = new List<IModule>();
            foreach (var module in allModules)
            {
                if (dependsOn[module].Count == 0)
                    ready.Add(module);
            }

            var result = new List<IModule>(allModules.Count);
            while (ready.Count > 0)
            {
                // tie-breaker：同一层级按 Priority 升序，相同 Priority 按 Type 全名稳定
                ready.Sort(CompareByPriorityAndType);
                var picked = ready[0];
                ready.RemoveAt(0);
                result.Add(picked);

                foreach (var dependent in dependedBy[picked])
                {
                    dependsOn[dependent].Remove(picked);
                    if (dependsOn[dependent].Count == 0)
                        ready.Add(dependent);
                }
            }

            if (result.Count < allModules.Count)
            {
                var remaining = new List<string>();
                foreach (var module in allModules)
                {
                    if (!result.Contains(module))
                        remaining.Add(module.GetType().Name);
                }
                throw new InvalidOperationException(
                    "Circular dependency detected among Modules: " + string.Join(", ", remaining));
            }

            return result;
        }

        private static int CompareByPriorityAndType(IModule a, IModule b)
        {
            int p = a.Priority.CompareTo(b.Priority);
            if (p != 0) return p;
            return string.Compare(a.GetType().FullName, b.GetType().FullName, StringComparison.Ordinal);
        }

        #endregion
    }
}
