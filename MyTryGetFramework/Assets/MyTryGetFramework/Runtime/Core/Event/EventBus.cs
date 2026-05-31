using System;
using System.Collections.Generic;
using System.Linq;

namespace TryGet
{
    internal sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        public void Publish<T>(T evt) where T : struct
        {
            Type key = typeof(T);
            if (!_handlers.TryGetValue(key, out var obj) || !(obj is HandlerList<T> list))
                return;

            list.Publish(evt);
            if (list.Count == 0)
                _handlers.Remove(key);
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type key = typeof(T);
            if (!_handlers.TryGetValue(key, out var obj))
            {
                var list = new HandlerList<T>();
                list.Add(handler);
                _handlers[key] = list;
                return;
            }

            ((HandlerList<T>)obj).Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            Type key = typeof(T);
            if (_handlers.TryGetValue(key, out var obj) && obj is HandlerList<T> list)
            {
                list.Remove(handler);
                if (list.Count == 0)
                    _handlers.Remove(key);
            }
        }

        public int GetSubscriberCount<T>() where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var obj) && obj is HandlerList<T> list)
                return list.Count;

            return 0;
        }

        public IReadOnlyList<Type> GetEventTypes()
        {
            return _handlers.Keys.ToArray();
        }

        internal IReadOnlyList<Exception> GetLastPublishExceptions<T>() where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var obj) && obj is HandlerList<T> list)
                return list.LastPublishExceptions;

            return Array.Empty<Exception>();
        }

        internal void Clear()
        {
            _handlers.Clear();
        }

        private sealed class HandlerList<T> where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private readonly List<PendingChange> _pendingChanges = new List<PendingChange>();
            private readonly List<Exception> _lastPublishExceptions = new List<Exception>();

            private int _dispatchDepth;

            public int Count => _handlers.Count;

            public IReadOnlyList<Exception> LastPublishExceptions => _lastPublishExceptions;

            public void Publish(T evt)
            {
                if (_dispatchDepth == 0)
                    _lastPublishExceptions.Clear();

                _dispatchDepth++;
                try
                {
                    int count = _handlers.Count;
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            _handlers[i].Invoke(evt);
                        }
                        catch (Exception ex)
                        {
                            _lastPublishExceptions.Add(ex);
                        }
                    }
                }
                finally
                {
                    _dispatchDepth--;
                    if (_dispatchDepth == 0)
                        FlushPendingChanges();
                }
            }

            public void Add(Action<T> handler)
            {
                if (Contains(handler))
                    throw new InvalidOperationException("Handler is already subscribed to event " + typeof(T).Name + ".");

                if (_dispatchDepth > 0)
                    _pendingChanges.Add(new PendingChange(PendingChangeKind.Add, handler));
                else
                    _handlers.Add(handler);
            }

            public void Remove(Action<T> handler)
            {
                if (_dispatchDepth > 0)
                {
                    _pendingChanges.Add(new PendingChange(PendingChangeKind.Remove, handler));
                    return;
                }

                _handlers.Remove(handler);
            }

            private bool Contains(Action<T> handler)
            {
                bool contains = _handlers.Contains(handler);
                for (int i = 0; i < _pendingChanges.Count; i++)
                {
                    PendingChange change = _pendingChanges[i];
                    if (change.Handler != handler)
                        continue;

                    contains = change.Kind == PendingChangeKind.Add;
                }

                return contains;
            }

            private void FlushPendingChanges()
            {
                for (int i = 0; i < _pendingChanges.Count; i++)
                {
                    PendingChange change = _pendingChanges[i];
                    if (change.Kind == PendingChangeKind.Add)
                    {
                        if (!_handlers.Contains(change.Handler))
                            _handlers.Add(change.Handler);
                    }
                    else
                    {
                        _handlers.Remove(change.Handler);
                    }
                }

                _pendingChanges.Clear();
            }

            private readonly struct PendingChange
            {
                public readonly PendingChangeKind Kind;
                public readonly Action<T> Handler;

                public PendingChange(PendingChangeKind kind, Action<T> handler)
                {
                    Kind = kind;
                    Handler = handler;
                }
            }
        }

        private enum PendingChangeKind
        {
            Add,
            Remove,
        }
    }
}
