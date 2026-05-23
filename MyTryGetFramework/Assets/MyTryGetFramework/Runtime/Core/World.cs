using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// World 的运行状态。
    /// </summary>
    public enum WorldState
    {
        /// <summary>创建后、尚未启动。</summary>
        Created,
        /// <summary>正在执行 Enter Phase。</summary>
        Entering,
        /// <summary>运行中（Update Phase 循环）。</summary>
        Running,
        /// <summary>正在执行 Exit Phase。</summary>
        Exiting,
        /// <summary>已关闭。</summary>
        Shutdown,
    }

    /// <summary>
    /// 一个独立的游戏世界实例，承载 Entity、System 和生命周期。
    /// World 是运行时边界的根 — 拥有 Entity 生命周期、System 执行、Phase 推进和 SystemGroup 调度的所有权。
    /// </summary>
    public sealed class World
    {
        private readonly string _name;
        private WorldState _state = WorldState.Created;

        // Entity 管理
        private int _nextEntityIndex = 1;
        private readonly Dictionary<EntityId, Entity> _entities = new Dictionary<EntityId, Entity>();
        private readonly List<Entity> _entityList = new List<Entity>();

        // System 调度（按 Phase 分组）
        private readonly List<SystemGroup> _enterGroups = new List<SystemGroup>();
        private readonly List<SystemGroup> _updateGroups = new List<SystemGroup>();
        private readonly List<SystemGroup> _exitGroups = new List<SystemGroup>();
        private readonly List<SystemBase> _allSystems = new List<SystemBase>();

        // 事件
        private readonly WorldEventBus _eventBus = new WorldEventBus();

        public World(string name = "World")
        {
            _name = name ?? "World";
        }

        public string Name => _name;
        public WorldState State => _state;
        public IWorldEventBus EventBus => _eventBus;
        public IReadOnlyList<Entity> Entities => _entityList;

        #region Entity Lifecycle

        /// <summary>
        /// 创建 Entity。EntityId 在此 World 内唯一（V0.1 不回收 index，Version 固定为 1）。
        /// </summary>
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

        /// <summary>
        /// 销毁 Entity。级联销毁子 Entity（叶子优先，ADR-0001 Ownership 树）。
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            ThrowIfShutdown();
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (entity.World != this)
                throw new InvalidOperationException("Entity does not belong to this World.");
            if (entity.IsDestroyed)
                return;

            entity.MarkDestroyed(cascade: true);
            PurgeDestroyedEntities();
        }

        /// <summary>
        /// 通过 EntityId 获取 Entity。若已销毁或不存在则返回 null。
        /// </summary>
        public Entity GetEntity(EntityId id)
        {
            if (_entities.TryGetValue(id, out var entity) && !entity.IsDestroyed)
                return entity;
            return null;
        }

        /// <summary>
        /// 从 _entities / _entityList 中移除全部 IsDestroyed=true 的 Entity。
        /// 级联销毁会把整个子树标记为 destroyed，但单条 DestroyEntity 调用只会扫一次。
        /// </summary>
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

        #region System Registration (ADR-0008)

        /// <summary>
        /// 注册 System 到指定的 Phase 和 SystemGroup。
        /// 必须在 World 启动前（Enter Phase 前）或 Enter Phase 中注册。
        /// </summary>
        /// <param name="system">要注册的 System。</param>
        /// <param name="phase">System 运行的 Phase。</param>
        /// <param name="group">System 所属的 SystemGroup。若为 null 则使用默认组。</param>
        public void RegisterSystem(SystemBase system, Phase phase, SystemGroup group = null)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
            if (_state == WorldState.Shutdown)
                throw new InvalidOperationException("Cannot register System to a shutdown World.");

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

        private SystemGroup GetOrCreateDefaultGroup(Phase phase)
        {
            var groups = GetGroupsForPhase(phase);
            // 查找名为 "Default" 的 group
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

        /// <summary>
        /// 添加 SystemGroup 到指定 Phase（控制 Group 之间的执行顺序）。
        /// Group 的执行顺序 = 添加顺序。
        /// </summary>
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

        #endregion

        #region Phase Execution

        /// <summary>
        /// 启动 World — 执行 Enter Phase 中所有 System。
        /// </summary>
        public void Start()
        {
            if (_state != WorldState.Created)
                throw new InvalidOperationException($"World cannot Start from state {_state}.");

            _state = WorldState.Entering;
            ExecutePhase(_enterGroups);
            _state = WorldState.Running;
        }

        /// <summary>
        /// 执行一次 Update tick。
        /// </summary>
        public void Update()
        {
            if (_state != WorldState.Running)
                throw new InvalidOperationException($"World cannot Update in state {_state}.");

            ExecutePhase(_updateGroups);
        }

        /// <summary>
        /// 关闭 World — 执行 Exit Phase 中所有 System，然后清理。
        /// Exit Phase 的 System 以注册顺序执行（如需逆序，在注册时控制）。
        /// </summary>
        public void Shutdown()
        {
            if (_state == WorldState.Shutdown)
                return;

            _state = WorldState.Exiting;
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
            _state = WorldState.Shutdown;
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

        /// <summary>
        /// 对当前所有活跃 Entity 评估 Query，返回匹配列表。
        /// </summary>
        internal List<Entity> EvaluateQuery(Query query)
        {
            var result = new List<Entity>();
            if (query == null)
            {
                // 无 Query 的 System 接收空列表
                return result;
            }

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
            if (_state == WorldState.Shutdown)
                throw new InvalidOperationException($"World '{_name}' is shutdown.");
        }

        public override string ToString()
        {
            return $"World({_name}, state={_state}, entities={_entityList.Count})";
        }
    }
}
