using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 框架根容器实现（ADR-0011）。
    ///
    /// 提供 Module 注册、查询、依赖排序、生命周期和帧调度。
    ///
    /// 错误路径安全（来自 Stage-2 review）：
    /// - Initialize 中途 OnInit 抛出时，已 OnInit 的 Module 倒序 Shutdown，状态回滚为未初始化
    /// - Shutdown 中单 Module 抛出不中断其余 Module 的 Shutdown，最终聚合为 ModuleShutdownException
    /// - DependsOn 返回 null 视为空集合
    ///
    /// 非线程安全：所有方法须在主线程调用。
    /// </summary>
    public sealed class ModuleHost : IModuleHost
    {
        private static readonly HashSet<Type> ForbiddenRegistrationTypes = new HashSet<Type>
        {
            typeof(IModule),
            typeof(IEarlyUpdateModule),
            typeof(IFixedUpdateModule),
            typeof(IUpdateModule),
            typeof(ILateUpdateModule),
            typeof(IEndOfFrameModule),
            typeof(IEventBus),
        };

        private readonly Dictionary<Type, IModule> _modulesByInterface = new Dictionary<Type, IModule>();
        private readonly IEventBus _eventBus = new EventBus();

        // 拓扑排序后的初始化顺序，Shutdown 用它的逆序。
        private List<IModule> _initOrder;

        // 缓存各帧阶段 Module 实例避免每帧 OfType。
        private List<IEarlyUpdateModule> _earlyUpdateModules;
        private List<IFixedUpdateModule> _fixedUpdateModules;
        private List<IUpdateModule> _updateModules;
        private List<ILateUpdateModule> _lateUpdateModules;
        private List<IEndOfFrameModule> _endOfFrameModules;

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
                    $"Register<T> must use the Module's service interface (e.g. ILogger), not its concrete type ({key.Name}).");

            if (ForbiddenRegistrationTypes.Contains(key))
                throw new ArgumentException(
                    $"Cannot register against framework base interface {key.Name}. " +
                    "Use the Module's own service interface (e.g. ILogger).");

            if (_modulesByInterface.ContainsKey(key))
                throw new ModuleAlreadyRegisteredException(key);

            _modulesByInterface[key] = module;
        }

        public T Get<T>() where T : class, IModule
        {
            if (_modulesByInterface.TryGetValue(typeof(T), out IModule module))
                return (T)module;

            throw new ModuleNotRegisteredException(typeof(T));
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
        /// 按 DependsOn 拓扑序 OnInit 所有 Module。中途失败时倒序 Shutdown 已初始化部分，状态回滚。
        ///
        /// 失败语义：Initialize 抛异常后，<see cref="IsInitialized"/> 仍为 false，所有已注册 Module 保留在
        /// _modulesByInterface 中。调用方可在修复后（例如 V0.3+ Replace API）再次 Initialize；目前推荐做法
        /// 是丢弃 ModuleHost 实例并重新构造。重复 Initialize 已初始化的 host 抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("ModuleHost already initialized.");

            var sortedOrder = TopologicalSort();

            // 去重 + 渐进式 OnInit，失败时回滚
            var initialized = new HashSet<IModule>();
            var initializedInOrder = new List<IModule>();
            var earlyUpdateModules = new List<IEarlyUpdateModule>();
            var fixedUpdateModules = new List<IFixedUpdateModule>();
            var updateModules = new List<IUpdateModule>();
            var lateUpdateModules = new List<ILateUpdateModule>();
            var endOfFrameModules = new List<IEndOfFrameModule>();

            try
            {
                foreach (var module in sortedOrder)
                {
                    if (!initialized.Add(module))
                        continue;

                    module.OnInit(this);
                    initializedInOrder.Add(module);

                    if (module is IEarlyUpdateModule eum)
                        earlyUpdateModules.Add(eum);
                    if (module is IFixedUpdateModule fum)
                        fixedUpdateModules.Add(fum);
                    if (module is IUpdateModule um)
                        updateModules.Add(um);
                    if (module is ILateUpdateModule lum)
                        lateUpdateModules.Add(lum);
                    if (module is IEndOfFrameModule eofm)
                        endOfFrameModules.Add(eofm);
                }
            }
            catch
            {
                // 失败回滚：倒序 Shutdown 已 OnInit 的部分
                for (int i = initializedInOrder.Count - 1; i >= 0; i--)
                {
                    try { initializedInOrder[i].Shutdown(); }
                    catch { /* 回滚阶段忽略二次异常 */ }
                }
                throw;
            }

            _initOrder = sortedOrder;
            _earlyUpdateModules = earlyUpdateModules;
            _fixedUpdateModules = fixedUpdateModules;
            _updateModules = updateModules;
            _lateUpdateModules = lateUpdateModules;
            _endOfFrameModules = endOfFrameModules;
            _initialized = true;
        }

        /// <summary>
        /// 帧 EarlyUpdate。按 OnInit 顺序调用所有 <see cref="IEarlyUpdateModule"/>。
        /// </summary>
        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("EarlyUpdate requires Initialize first.");

            var list = _earlyUpdateModules;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].EarlyUpdate(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 帧 FixedUpdate。按 OnInit 顺序调用所有 <see cref="IFixedUpdateModule"/>。
        /// </summary>
        public void FixedUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("FixedUpdate requires Initialize first.");

            var list = _fixedUpdateModules;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].FixedUpdate(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 帧 Update。按 OnInit 顺序调用所有 <see cref="IUpdateModule"/>。
        /// </summary>
        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("Update requires Initialize first.");

            var list = _updateModules;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Update(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 帧 LateUpdate。按 OnInit 顺序调用所有 <see cref="ILateUpdateModule"/>。
        /// </summary>
        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("LateUpdate requires Initialize first.");

            var list = _lateUpdateModules;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].LateUpdate(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 帧 EndOfFrame。按 OnInit 顺序调用所有 <see cref="IEndOfFrameModule"/>。
        /// </summary>
        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized)
                throw new InvalidOperationException("EndOfFrame requires Initialize first.");

            var list = _endOfFrameModules;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].EndOfFrame(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 按 Initialize 逆序 Shutdown 所有 Module。允许多次调用。
        /// 单 Module 抛异常不中断后续 Module Shutdown，最终聚合抛 <see cref="ModuleShutdownException"/>。
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
                return;

            var shutdown = new HashSet<IModule>();
            List<Exception> failures = null;

            for (int i = _initOrder.Count - 1; i >= 0; i--)
            {
                var module = _initOrder[i];
                if (!shutdown.Add(module))
                    continue;

                try
                {
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    if (failures == null)
                        failures = new List<Exception>();
                    failures.Add(ex);
                }
            }

            _initialized = false;
            _initOrder = null;
            _earlyUpdateModules = null;
            _fixedUpdateModules = null;
            _updateModules = null;
            _lateUpdateModules = null;
            _endOfFrameModules = null;

            if (failures != null)
                throw new ModuleShutdownException(failures);
        }

        #endregion

        #region Topological Sort

        private List<IModule> TopologicalSort()
        {
            // 去重：同实例多接口只算一个节点
            var allModules = new List<IModule>();
            var seen = new HashSet<IModule>();
            foreach (var kv in _modulesByInterface)
            {
                if (seen.Add(kv.Value))
                    allModules.Add(kv.Value);
            }

            var dependsOn = new Dictionary<IModule, List<IModule>>();
            var dependedBy = new Dictionary<IModule, List<IModule>>();

            foreach (var module in allModules)
            {
                dependsOn[module] = new List<IModule>();
                dependedBy[module] = new List<IModule>();
            }

            foreach (var module in allModules)
            {
                // DependsOn 返回 null 视为空集合（防御）
                IReadOnlyList<Type> deps = module.DependsOn ?? Array.Empty<Type>();

                foreach (var depType in deps)
                {
                    if (!_modulesByInterface.TryGetValue(depType, out var depModule))
                        throw new ModuleDependencyMissingException(module.GetType(), depType);

                    if (depModule == module)
                        throw new InvalidOperationException(
                            $"Module {module.GetType().Name} declares dependency on itself ({depType.Name}).");

                    if (!dependsOn[module].Contains(depModule))
                    {
                        dependsOn[module].Add(depModule);
                        dependedBy[depModule].Add(module);
                    }
                }
            }

            var ready = new List<IModule>();
            foreach (var module in allModules)
            {
                if (dependsOn[module].Count == 0)
                    ready.Add(module);
            }

            var result = new List<IModule>(allModules.Count);
            while (ready.Count > 0)
            {
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
                throw new ModuleCircularDependencyException(remaining);
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
