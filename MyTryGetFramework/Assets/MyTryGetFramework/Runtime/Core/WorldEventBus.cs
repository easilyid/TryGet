using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// World-level 事件总线实现。
    /// 按类型注册处理器，在 World 范围内分发事件 (ADR-0010)。
    /// </summary>
    internal sealed class WorldEventBus : IWorldEventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

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
