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
    ///
    /// 支持一次性、周期性、暂停和恢复定时器。
    /// </summary>
    public sealed class TimerModule : ITimerModule, IUpdateModule
    {
        private struct Entry
        {
            public long Id;
            public float RemainingSeconds;
            public float Interval;
            public Action Callback;
            public bool Unscaled;
            public bool Cancelled;
            public bool Repeating;
            public bool Paused;
            public int MaxCatchUp;
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

        public void OnInit(IModuleSystem host) { }
        public void Shutdown() { _entries.Clear(); }

        public TimerHandle Schedule(float seconds, Action callback) =>
            ScheduleInternal(seconds, callback, unscaled: false, repeating: false, maxCatchUp: 0);

        public TimerHandle ScheduleUnscaled(float seconds, Action callback) =>
            ScheduleInternal(seconds, callback, unscaled: true, repeating: false, maxCatchUp: 0);

        public TimerHandle ScheduleRepeat(float intervalSeconds, Action callback, int maxCatchUp = 0) =>
            ScheduleInternal(intervalSeconds, callback, unscaled: false, repeating: true, maxCatchUp);

        private TimerHandle ScheduleInternal(float seconds, Action callback, bool unscaled, bool repeating, int maxCatchUp)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Must be >= 0");
            if (repeating && seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Repeating interval must be > 0 to avoid infinite loop");
            if (maxCatchUp < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCatchUp), "Must be >= 0");

            long id = _nextId++;
            _entries.Add(new Entry
            {
                Id = id,
                RemainingSeconds = seconds,
                Interval = repeating ? seconds : 0f,
                Callback = callback,
                Unscaled = unscaled,
                Cancelled = false,
                Repeating = repeating,
                Paused = false,
                MaxCatchUp = maxCatchUp,
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

        public bool Pause(TimerHandle handle)
        {
            if (!handle.IsValid) return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == handle.Id && !_entries[i].Cancelled && !_entries[i].Paused)
                {
                    var entry = _entries[i];
                    entry.Paused = true;
                    _entries[i] = entry;
                    return true;
                }
            }
            return false;
        }

        public bool Resume(TimerHandle handle)
        {
            if (!handle.IsValid) return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == handle.Id && !_entries[i].Cancelled && _entries[i].Paused)
                {
                    var entry = _entries[i];
                    entry.Paused = false;
                    _entries[i] = entry;
                    return true;
                }
            }
            return false;
        }

        public bool IsPaused(TimerHandle handle)
        {
            if (!handle.IsValid) return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == handle.Id && !_entries[i].Cancelled)
                    return _entries[i].Paused;
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

                // V0.5: 暂停 timer 不推进
                if (entry.Paused) continue;

                entry.RemainingSeconds -= entry.Unscaled ? unscaledDeltaTime : deltaTime;
                if (entry.RemainingSeconds <= 0f)
                {
                    var callback = entry.Callback;

                    // V0.5: 周期 timer 不删除，重置 RemainingSeconds
                    // C7: 补偿策略 — 长帧时触发主要的一次 + 最多 maxCatchUp 次补偿
                    if (entry.Repeating)
                    {
                        // 主触发（总是执行一次）
                        try { callback?.Invoke(); }
                        catch (Exception ex)
                        {
                            if (failures == null) failures = new List<Exception>();
                            failures.Add(ex);
                        }

                        // callback 内可能对自己 Cancel/Pause。Cancel/Pause 只就地标记 _entries[i]
                        // （不移位），因此必须把最新状态读回局部 entry，否则下面的 _entries[i] = entry
                        // 会用 callback 之前的旧快照覆盖，导致周期 timer 的自取消/自暂停失效。
                        if (!RefreshSelfAfterCallback(i, ref entry))
                        {
                            _entries.RemoveAt(i); // callback 取消了自己
                            continue;
                        }

                        entry.RemainingSeconds += entry.Interval;

                        if (entry.Paused)
                        {
                            // callback 暂停了自己：对齐到下一周期并冻结，本帧不再补偿
                            _entries[i] = entry;
                            continue;
                        }

                        // 补偿触发：循环直到 RemainingSeconds > 0 或达到 maxCatchUp 上限
                        int compensated = 0;
                        bool selfInterrupted = false;
                        while (entry.RemainingSeconds <= 0f && compensated < entry.MaxCatchUp)
                        {
                            entry.RemainingSeconds += entry.Interval;
                            compensated++;
                            try { callback?.Invoke(); }
                            catch (Exception ex)
                            {
                                if (failures == null) failures = new List<Exception>();
                                failures.Add(ex);
                            }

                            // 补偿触发中同样可能自取消/自暂停
                            if (!RefreshSelfAfterCallback(i, ref entry))
                            {
                                _entries.RemoveAt(i);
                                selfInterrupted = true;
                                break;
                            }
                            if (entry.Paused)
                            {
                                if (entry.RemainingSeconds <= 0f)
                                    entry.RemainingSeconds = entry.Interval;
                                _entries[i] = entry;
                                selfInterrupted = true;
                                break;
                            }
                        }
                        if (selfInterrupted) continue;

                        // 如果仍然 <= 0，说明超过 maxCatchUp，直接对齐到下一个周期
                        if (entry.RemainingSeconds <= 0f)
                        {
                            entry.RemainingSeconds = entry.Interval;
                        }

                        _entries[i] = entry;
                    }
                    else
                    {
                        _entries.RemoveAt(i);
                        try { callback?.Invoke(); }
                        catch (Exception ex)
                        {
                            if (failures == null) failures = new List<Exception>();
                            failures.Add(ex);
                        }
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

        /// <summary>
        /// 周期 timer 的 callback 执行后，把自身 entry 在 <see cref="_entries"/> 中的最新状态读回
        /// 局部副本。callback 只能通过 <see cref="Cancel"/>/<see cref="Pause"/>（就地标记，不移位）
        /// 或 <see cref="Schedule"/>（追加末尾）修改集合，因此 <c>_entries[index]</c> 仍是同一 entry。
        /// 返回 false 表示该 timer 已被自身 callback 取消，调用方应将其移除。
        /// </summary>
        private bool RefreshSelfAfterCallback(int index, ref Entry entry)
        {
            var current = _entries[index];
            if (current.Cancelled)
                return false;
            entry.Paused = current.Paused;
            return true;
        }
    }
}
