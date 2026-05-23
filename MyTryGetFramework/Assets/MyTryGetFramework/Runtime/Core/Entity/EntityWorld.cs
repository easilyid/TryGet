using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 玩法层根：承载 Entity、System 和 Phase 调度（V0.3 起替代 V0.1 的 <c>World</c>）。
    ///
    /// 关键变化（相对 V0.1 World）：
    /// - 实现 <see cref="IModule"/> + <see cref="IUpdateModule"/>，可挂到 ModuleHost 统一管理。
    /// - <see cref="IUpdateModule.Update"/> 签名是 (deltaTime, unscaledDeltaTime)，
    ///   V0.1 的无参 Update() 已弃用——测试需改用带参版本。
    /// - 不再持有外部"全局事件总线"角色；EntityWorld 自身的 <see cref="EventBus"/> 仅作为
    ///   World 级别的局部事件总线（V0.1 兼容）。全局事件请用 ModuleHost.EventBus。
    /// - 不再支持 IWorldAdapter；驱动方式由 Unity 侧 WorldProxy 通过 ModuleHost 驱动。
    /// </summary>
    public sealed class EntityWorld : IEntityWorld
    {
        private readonly string _name;
        private EntityWorldState _state = EntityWorldState.Created;

        // Entity 管理
        private int _nextEntityIndex = 1;
        private readonly Dictionary<EntityId, Entity> _entities = new Dictionary<EntityId, Entity>();
        private readonly List<Entity> _entityList = new List<Entity>();

        // System 调度（按 Phase 分组）
        private readonly List<SystemGroup> _enterGroups = new List<SystemGroup>();
        private readonly List<SystemGroup> _updateGroups = new List<SystemGroup>();
        private readonly List<SystemGroup> _exitGroups = new List<SystemGroup>();
        private readonly List<SystemBase> _allSystems = new List<SystemBase>();

        // World 级别事件总线（V0.1 兼容；全局事件用 ModuleHost.EventBus）
        private readonly WorldEventBus _eventBus = new WorldEventBus();

        public EntityWorld(string name = "World")
        {
            _name = name ?? "World";
        }

        public string Name => _name;
        public EntityWorldState State => _state;
        public IWorldEventBus EventBus => _eventBus;
        public IReadOnlyList<Entity> Entities => _entityList;

        #region IModule

        // EntityWorld 优先级介于 Common 三件套（-500）和业务 Module（0）之间，
        // 让 Log/Timer/Pool 在 EntityWorld OnInit 时可用。
        public int Priority => -100;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        /// <summary>
        /// IModule.OnInit：触发 Enter Phase。
        /// 测试中也可以直接调用 <see cref="Start"/>（旧 V0.1 API），不必经过 ModuleHost。
        /// </summary>
        public void OnInit(IModuleHost host)
        {
            if (_state == EntityWorldState.Created)
                Start();
        }

        #endregion

        #region Entity Lifecycle

        public Entity CreateEntity()
        {
            ThrowIfShutdown();

            int index = _nextEntityIndex++;
            EntityId id = new EntityId(index, 1);
            Entity entity = new Entity(id, this);
            _entities[id] = entity;
            _entityList.Add(entity);
            return entity;
        }

        public void DestroyEntity(Entity entity)
        {
            ThrowIfShutdown();
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (entity.World != this)
                throw new InvalidOperationException("Entity does not belong to this EntityWorld.");
            if (entity.IsDestroyed)
                return;

            entity.MarkDestroyed(cascade: true);
            PurgeDestroyedEntities();
        }

        public Entity GetEntity(EntityId id)
        {
            if (_entities.TryGetValue(id, out var entity) && !entity.IsDestroyed)
                return entity;
            return null;
        }

        private void PurgeDestroyedEntities()
        {
            for (int i = _entityList.Count - 1; i >= 0; i--)
            {
                Entity e = _entityList[i];
                if (e.IsDestroyed)
                {
                    _entities.Remove(e.Id);
                    _entityList.RemoveAt(i);
                }
            }
        }

        #endregion

        #region System Registration (ADR-0008 V2)

        public void RegisterSystem(SystemBase system, Phase phase, SystemGroup group = null)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
            if (_state == EntityWorldState.Shutdown)
                throw new InvalidOperationException("Cannot register System to a shutdown EntityWorld.");

            if (group == null)
            {
                group = GetOrCreateDefaultGroup(phase);
            }
            else
            {
                EnsureGroupInPhase(group, phase);
            }

            system.SetWorld(this);
            group.AddSystem(system);
            _allSystems.Add(system);
            system.OnCreate();
        }

        public void AddSystemGroup(SystemGroup group, Phase phase)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            var groups = GetGroupsForPhase(phase);
            if (!groups.Contains(group))
            {
                groups.Add(group);
            }
        }

        private SystemGroup GetOrCreateDefaultGroup(Phase phase)
        {
            var groups = GetGroupsForPhase(phase);
            foreach (var g in groups)
            {
                if (g.Name == "Default")
                    return g;
            }
            var defaultGroup = new SystemGroup("Default");
            groups.Add(defaultGroup);
            return defaultGroup;
        }

        private void EnsureGroupInPhase(SystemGroup group, Phase phase)
        {
            var groups = GetGroupsForPhase(phase);
            if (!groups.Contains(group))
            {
                groups.Add(group);
            }
        }

        private List<SystemGroup> GetGroupsForPhase(Phase phase)
        {
            switch (phase)
            {
                case Phase.Enter: return _enterGroups;
                case Phase.Update: return _updateGroups;
                case Phase.Exit: return _exitGroups;
                default: throw new ArgumentOutOfRangeException(nameof(phase));
            }
        }

        #endregion

        #region Phase Execution

        /// <summary>
        /// 启动 EntityWorld，执行 Enter Phase。
        /// 直接使用场景（非 ModuleHost 驱动）：测试 / 独立运行时。
        /// </summary>
        public void Start()
        {
            if (_state != EntityWorldState.Created)
                throw new InvalidOperationException($"EntityWorld cannot Start from state {_state}.");

            _state = EntityWorldState.Entering;
            ExecutePhase(_enterGroups);
            _state = EntityWorldState.Running;
        }

        /// <summary>
        /// IUpdateModule.Update：执行 Update Phase 中所有 System。
        /// V0.3 起取代 V0.1 的无参 Update()——deltaTime 参数留作未来 System 拿。
        /// </summary>
        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (_state != EntityWorldState.Running)
                throw new InvalidOperationException($"EntityWorld cannot Update in state {_state}.");

            ExecutePhase(_updateGroups);
        }

        /// <summary>
        /// IModule.Shutdown：执行 Exit Phase，然后清理所有 System / Entity / EventBus。
        /// </summary>
        public void Shutdown()
        {
            if (_state == EntityWorldState.Shutdown)
                return;

            _state = EntityWorldState.Exiting;
            ExecutePhase(_exitGroups);

            // 销毁所有 System
            for (int i = _allSystems.Count - 1; i >= 0; i--)
            {
                _allSystems[i].OnDestroy();
                _allSystems[i].ClearWorld();
            }
            _allSystems.Clear();
            _enterGroups.Clear();
            _updateGroups.Clear();
            _exitGroups.Clear();

            // 销毁所有 Entity
            for (int i = _entityList.Count - 1; i >= 0; i--)
            {
                if (!_entityList[i].IsDestroyed)
                    _entityList[i].MarkDestroyed(cascade: false);
            }
            _entities.Clear();
            _entityList.Clear();

            _eventBus.Clear();
            _state = EntityWorldState.Shutdown;
        }

        private void ExecutePhase(List<SystemGroup> groups)
        {
            foreach (var group in groups)
            {
                foreach (var system in group.Systems)
                {
                    var matched = EvaluateQuery(system.Query);
                    system.Execute(matched);
                }
            }
        }

        #endregion

        #region Query Evaluation

        internal List<Entity> EvaluateQuery(Query query)
        {
            var result = new List<Entity>();
            if (query == null)
                return result;

            for (int i = 0; i < _entityList.Count; i++)
            {
                Entity entity = _entityList[i];
                if (!entity.IsDestroyed && query.Matches(entity))
                {
                    result.Add(entity);
                }
            }
            return result;
        }

        #endregion

        private void ThrowIfShutdown()
        {
            if (_state == EntityWorldState.Shutdown)
                throw new InvalidOperationException($"EntityWorld '{_name}' is shutdown.");
        }

        public override string ToString()
        {
            return $"EntityWorld({_name}, state={_state}, entities={_entityList.Count})";
        }
    }
}
