using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// ITimerModule 默认实现。使用累加时间戳排序的列表（O(log n) Schedule，O(n) Update 但量级小）。
    /// V0.2 不优化大量并发定时器（V0.3+ 可换成 MinHeap）。
    /// </summary>
    public sealed class TimerModule : ITimerModule, IUpdateModule
    {
        private struct Entry
        {
            public long Id;
            public float RemainingSeconds;
            public Action Callback;
            public bool Unscaled;
            public bool Cancelled;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private long _nextId = 1;

        public int Priority => -500;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _entries.Count; i++)
                    if (!_entries[i].Cancelled) count++;
                return count;
            }
        }

        public void OnInit(IModuleHost host) { }
        public void Shutdown() { _entries.Clear(); }

        public TimerHandle Schedule(float seconds, Action callback) =>
            ScheduleInternal(seconds, callback, unscaled: false);

        public TimerHandle ScheduleUnscaled(float seconds, Action callback) =>
            ScheduleInternal(seconds, callback, unscaled: true);

        private TimerHandle ScheduleInternal(float seconds, Action callback, bool unscaled)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Must be >= 0");

            long id = _nextId++;
            _entries.Add(new Entry
            {
                Id = id,
                RemainingSeconds = seconds,
                Callback = callback,
                Unscaled = unscaled,
                Cancelled = false,
            });
            return new TimerHandle(id);
        }

        public bool Cancel(TimerHandle handle)
        {
            if (!handle.IsValid) return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == handle.Id && !_entries[i].Cancelled)
                {
                    var entry = _entries[i];
                    entry.Cancelled = true;
                    _entries[i] = entry;
                    return true;
                }
            }
            return false;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            // 倒序遍历允许就地移除已完成项
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Cancelled)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                entry.RemainingSeconds -= entry.Unscaled ? unscaledDeltaTime : deltaTime;
                if (entry.RemainingSeconds <= 0f)
                {
                    var callback = entry.Callback;
                    _entries.RemoveAt(i);
                    try { callback?.Invoke(); }
                    catch { /* TimerModule 不吞业务异常，但也不在迭代里崩 — V0.3 加 ILogModule 投递 */ throw; }
                }
                else
                {
                    _entries[i] = entry;
                }
            }
        }
    }
}
