using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// ITimerModule 默认实现。使用累加时间戳排序的列表（O(log n) Schedule，O(n) Update 但量级小）。
    /// V0.2 不优化大量并发定时器（V0.3+ 可换成 MinHeap）。
    ///
    /// 精度：每帧 deltaTime 减法累积，长时定时器（&gt;10s）会有浮点漂移（量级毫秒）。
    /// 关键业务计时请避免直接依赖本实现，等待 V0.3+ 升级为绝对时间戳方案。
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

        /// <summary>
        /// 推进所有定时器。单个 callback 抛异常不会中断剩余 timer：异常会被收集并在遍历结束后聚合为
        /// <see cref="AggregateException"/> 抛出。这样保证"同帧到期"语义不被部分 callback 失败破坏。
        ///
        /// 行为约定（callback 内调用 Schedule/Cancel）：
        /// - callback 内 Schedule：新 entry 追加到 _entries 末尾，因倒序遍历，本帧不会被处理（下一帧才推进）。
        /// - callback 内 Cancel(handle)：若 handle 指向尚未访问的 entry，会被标记 Cancelled，本帧到达时跳过。
        /// </summary>
        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            List<Exception> failures = null;

            // 倒序遍历允许就地移除已完成项
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                // 防御：callback 可能在遍历过程中通过 Cancel/Schedule 修改 _entries。
                // i 已超界（callback 内异常或外部行为）则提前结束。
                if (i >= _entries.Count) continue;

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
                    catch (Exception ex)
                    {
                        if (failures == null) failures = new List<Exception>();
                        failures.Add(ex);
                    }
                }
                else
                {
                    _entries[i] = entry;
                }
            }

            if (failures != null)
                throw new AggregateException(
                    "One or more timer callbacks threw. See InnerExceptions.", failures);
        }
    }
}
