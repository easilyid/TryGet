using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// Entity-level 事件派发器实现。
    /// 由 Entity 持有，Aspect 通过 IEntityEventDispatcher 接口发布事件。
    /// </summary>
    internal sealed class EntityEventDispatcher : IEntityEventDispatcher
    {
        private readonly Entity _entity;
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        internal EntityEventDispatcher(Entity entity)
        {
            _entity = entity;
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var obj) && obj is List<Action<T>> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].Invoke(evt);
                }
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type key = typeof(T);
            if (!_handlers.TryGetValue(key, out var obj))
            {
                var list = new List<Action<T>>();
                list.Add(handler);
                _handlers[key] = list;
            }
            else
            {
                ((List<Action<T>>)obj).Add(handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            Type key = typeof(T);
            if (_handlers.TryGetValue(key, out var obj) && obj is List<Action<T>> list)
            {
                list.Remove(handler);
                if (list.Count == 0)
                    _handlers.Remove(key);
            }
        }

        internal void Clear()
        {
            _handlers.Clear();
        }
    }
}
