using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// World 内承载身份、层级和生命周期的组合宿主。
    /// Entity 拥有 Aspect（能力切片）和 Tag（存在标记），并通过 Ownership 支持父子树。
    /// </summary>
    public sealed class Entity
    {
        private readonly EntityId _id;
        private readonly EntityWorld _world;
        private readonly Dictionary<Type, Aspect> _aspects = new Dictionary<Type, Aspect>();
        private readonly HashSet<Type> _tags = new HashSet<Type>();

        // V0.3：类型索引位掩码镜像。GetAspect 仍走 _aspects 取实例；
        // Query.Matches 走 mask 加速（O(1) 位运算替代 O(n) Dictionary 查找）。
        private BitArray256 _aspectMask;
        private BitArray256 _tagMask;

        private Entity _parent;
        private readonly List<Entity> _children = new List<Entity>();
        private readonly EntityEventDispatcher _eventDispatcher;

        private bool _destroyed;

        internal Entity(EntityId id, EntityWorld world)
        {
            _id = id;
            _world = world;
            _eventDispatcher = new EntityEventDispatcher(this);
        }

        public EntityId Id => _id;
        public EntityWorld World => _world;
        public bool IsDestroyed => _destroyed;

        /// <summary>框架内部：暴露给 Query.Matches 做位运算。</summary>
        internal BitArray256 AspectMask => _aspectMask;

        /// <summary>框架内部：暴露给 Query.Matches 做位运算。</summary>
        internal BitArray256 TagMask => _tagMask;

        /// <summary>
        /// 获取此 Entity 的弱引用 Handle。
        /// </summary>
        public Handle GetHandle() => new Handle(_id);

        #region Aspect

        /// <summary>
        /// 挂载 Aspect 到此 Entity。
        /// </summary>
        /// <exception cref="InvalidOperationException">Entity 已销毁或已存在同类型 Aspect。</exception>
        public void Attach(Aspect aspect)
        {
            ThrowIfDestroyed();
            if (aspect == null)
                throw new ArgumentNullException(nameof(aspect));

            Type key = aspect.AspectType;
            if (_aspects.ContainsKey(key))
                throw new InvalidOperationException($"Entity {_id} already has Aspect of type {key.Name}.");

            _aspects[key] = aspect;
            _aspectMask.Add(TypeRegistry.GetOrAllocate(key));
            aspect.SetOwner(this, _eventDispatcher);
            aspect.OnAttach();
        }

        /// <summary>
        /// 从此 Entity 卸载 Aspect。
        /// </summary>
        /// <returns>是否成功卸载。</returns>
        public bool Detach(Aspect aspect)
        {
            ThrowIfDestroyed();
            if (aspect == null)
                return false;

            Type key = aspect.AspectType;
            if (!_aspects.TryGetValue(key, out var existing) || existing != aspect)
                return false;

            _aspects.Remove(key);
            if (TypeRegistry.TryGet(key, out int idx))
                _aspectMask.Remove(idx);
            aspect.OnDetach();
            aspect.ClearOwner();
            return true;
        }

        /// <summary>
        /// 卸载指定类型的 Aspect。
        /// </summary>
        public bool Detach<T>() where T : Aspect
        {
            if (_aspects.TryGetValue(typeof(T), out var aspect))
            {
                return Detach(aspect);
            }
            return false;
        }

        /// <summary>
        /// 获取指定类型的 Aspect。
        /// </summary>
        public T GetAspect<T>() where T : Aspect
        {
            _aspects.TryGetValue(typeof(T), out var aspect);
            return aspect as T;
        }

        /// <summary>
        /// 是否拥有指定类型的 Aspect。V0.3 起走位运算 O(1)。
        /// </summary>
        public bool HasAspect<T>() where T : Aspect
        {
            return _aspectMask.Contains(TypeIndex<T>.Index);
        }

        /// <summary>
        /// 是否拥有指定类型的 Aspect（Type 版本）。
        /// </summary>
        public bool HasAspect(Type aspectType)
        {
            return TypeRegistry.TryGet(aspectType, out int idx) && _aspectMask.Contains(idx);
        }

        /// <summary>
        /// 当前 Aspect 数量。
        /// </summary>
        public int AspectCount => _aspects.Count;

        /// <summary>
        /// 枚举所有 Aspect 类型。
        /// </summary>
        internal IEnumerable<Type> AspectTypes => _aspects.Keys;

        #endregion

        #region Tag

        /// <summary>
        /// 添加 Tag。
        /// </summary>
        public bool AddTag<T>() where T : Tag
        {
            ThrowIfDestroyed();
            if (_tags.Add(typeof(T)))
            {
                _tagMask.Add(TypeIndex<T>.Index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除 Tag。
        /// </summary>
        public bool RemoveTag<T>() where T : Tag
        {
            ThrowIfDestroyed();
            if (_tags.Remove(typeof(T)))
            {
                _tagMask.Remove(TypeIndex<T>.Index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 是否拥有指定 Tag。V0.3 起走位运算 O(1)。
        /// </summary>
        public bool HasTag<T>() where T : Tag
        {
            return _tagMask.Contains(TypeIndex<T>.Index);
        }

        /// <summary>
        /// 是否拥有指定类型的 Tag（Type 版本）。
        /// </summary>
        public bool HasTag(Type tagType)
        {
            return TypeRegistry.TryGet(tagType, out int idx) && _tagMask.Contains(idx);
        }

        #endregion

        #region Ownership

        /// <summary>
        /// 父 Entity（仅一个）。
        /// </summary>
        public Entity Parent => _parent;

        /// <summary>
        /// 子 Entity 列表（只读视图）。
        /// </summary>
        public IReadOnlyList<Entity> Children => _children;

        /// <summary>
        /// 将子 Entity 纳入此 Entity 的所有权树。
        /// </summary>
        public void AttachChild(Entity child)
        {
            ThrowIfDestroyed();
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (child == this)
                throw new InvalidOperationException("Cannot attach Entity to itself.");
            if (child._parent != null)
                throw new InvalidOperationException($"Entity {child.Id} already has a parent.");
            if (child._world != _world)
                throw new InvalidOperationException("Cannot attach Entity from a different World.");
            if (child.IsDestroyed)
                throw new InvalidOperationException("Cannot attach a destroyed Entity.");

            child._parent = this;
            _children.Add(child);
        }

        /// <summary>
        /// 将子 Entity 从此 Entity 的所有权树中移出。
        /// 分离后子 Entity 继续独立存在。
        /// </summary>
        public bool DetachChild(Entity child)
        {
            ThrowIfDestroyed();
            if (child == null || child._parent != this)
                return false;

            child._parent = null;
            _children.Remove(child);
            return true;
        }

        #endregion

        #region Entity Event

        /// <summary>
        /// 订阅此 Entity 的事件。Aspect 和外部均可订阅。
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            _eventDispatcher.Subscribe(handler);
        }

        /// <summary>
        /// 取消订阅此 Entity 的事件。
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            _eventDispatcher.Unsubscribe(handler);
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 框架内部：标记 Entity 为已销毁并清理。
        /// </summary>
        internal void MarkDestroyed(bool cascade)
        {
            if (_destroyed)
                return;

            if (cascade)
            {
                // 叶子优先：先递归销毁子 Entity
                for (int i = _children.Count - 1; i >= 0; i--)
                {
                    _children[i].MarkDestroyed(true);
                }
            }

            _destroyed = true;

            // 卸载所有 Aspect（逆序概念上的清理）
            var aspects = new List<Aspect>(_aspects.Values);
            for (int i = aspects.Count - 1; i >= 0; i--)
            {
                aspects[i].OnDetach();
                aspects[i].ClearOwner();
            }
            _aspects.Clear();
            _tags.Clear();
            _aspectMask.Clear();
            _tagMask.Clear();
            _eventDispatcher.Clear();

            // 从父级移除
            _parent?._children.Remove(this);
            _parent = null;
            _children.Clear();
        }

        private void ThrowIfDestroyed()
        {
            if (_destroyed)
                throw new InvalidOperationException($"Entity {_id} is destroyed.");
        }

        #endregion

        public override string ToString()
        {
            return $"Entity({_id})";
        }
    }
}
