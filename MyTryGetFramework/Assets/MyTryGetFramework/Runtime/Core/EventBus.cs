using System;
using System.Collections.Generic;

namespace TryGet
{
    internal sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        public void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var obj) || !(obj is List<Action<T>> list))
                return;

            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i].Invoke(evt);
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
                return;
            }

            var handlers = (List<Action<T>>)obj;
            if (handlers.Contains(handler))
                throw new InvalidOperationException("Handler is already subscribed to event " + key.Name + ".");

            handlers.Add(handler);
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

        public int GetSubscriberCount<T>() where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var obj) && obj is List<Action<T>> list)
                return list.Count;

            return 0;
        }

        public IReadOnlyList<Type> GetEventTypes()
        {
            return new List<Type>(_handlers.Keys);
        }

        internal void Clear()
        {
            _handlers.Clear();
        }
    }
}
