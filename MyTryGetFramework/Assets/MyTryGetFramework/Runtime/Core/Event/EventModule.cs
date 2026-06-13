using System;
using System.Collections.Generic;
using System.Linq;

namespace TryGet
{
    internal sealed class EventModule : IEventModule
    {
        /// <summary>
        /// Publish 嵌套深度上限。handler 内再 Publish（任意事件类型）累计深度超过此值时抛出，
        /// 防止事件互相触发形成无限递归导致栈溢出（参考 MyFramework EventSystem MAX_DEPTH）。
        /// </summary>
        internal const int MaxPublishDepth = 32;

        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        private int _publishDepth;

        public event Action<Type, Exception> HandlerException;

        public void Publish<T>(T evt) where T : struct
        {
            if (_publishDepth >= MaxPublishDepth)
                throw new InvalidOperationException(
                    "EventModule.Publish nesting depth exceeded " + MaxPublishDepth +
                    ". Events are likely publishing each other in an infinite cycle (event type: " + typeof(T).Name + ").");

            Type key = typeof(T);
            if (!_handlers.TryGetValue(key, out var obj) || !(obj is HandlerList<T> list))
                return;

            _publishDepth++;
            try
            {
                list.Publish(evt, this);
            }
            finally
            {
                _publishDepth--;
            }

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

        private void RaiseHandlerException(Type eventType, Exception ex)
        {
            var handler = HandlerException;
            if (handler == null)
                return;

            try
            {
                handler(eventType, ex);
            }
            catch
            {
                // 吞掉钩子自身的异常，防止级联中断派发
            }
        }

        private sealed class HandlerList<T> where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private readonly List<PendingChange> _pendingChanges = new List<PendingChange>();
            private readonly List<Exception> _lastPublishExceptions = new List<Exception>();

            private int _dispatchDepth;

            public int Count => _handlers.Count;

            public IReadOnlyList<Exception> LastPublishExceptions => _lastPublishExceptions;

            public void Publish(T evt, EventModule owner)
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
                            owner.RaiseHandlerException(typeof(T), ex);
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
