using System;
using System.Collections.Generic;

namespace TryGet.Async
{
    /// <summary>
    /// <see cref="ITGTaskScheduler"/> 的默认实现。
    ///
    /// V2.2+ Phase-aware 设计：
    /// - 为每个 FramePhase 维护独立的 Yield/Delay/FrameWait 队列
    /// - 实现所有 5 个 Update 接口，每个 Phase 处理对应队列
    /// - 支持跨 Phase 调度（如在 EarlyUpdate 中 Yield(LateUpdate)）
    ///
    /// 取消（ADR-0021）：Yield/Delay/WaitForFrames 提供 <see cref="TGCancelToken"/> 重载。取消时绑定的
    /// pending TGTask 以 <see cref="OperationCanceledException"/> 完成；entry 的清理与 registration 注销由
    /// 各 ProcessXxxQueue 统一处理（见 <see cref="RegisterCancel"/> 的说明）。
    ///
    /// 命名特别说明：类名 <c>TGTaskScheduler</c> 与 <see cref="System.Threading.Tasks.TaskScheduler"/> 区分开。
    /// 同时承担"全局 UnobservedException 钩子持有者"角色（对标 UniTaskScheduler.UnobservedTaskException）。
    /// </summary>
    public sealed class TGTaskScheduler : ITGTaskScheduler
    {
        public int Priority => -150;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        // 全局时间和帧计数
        private long _frameCount;
        private float _elapsedTime;         // Scaled time（受 Time.timeScale 影响）
        private float _unscaledElapsedTime; // C3：Unscaled time（真实时间）
        private float _fixedElapsedTime;
        private float _fixedUnscaledElapsedTime;

        // 用于基于 Phase 顺序判断帧边界。
        // 规则：
        // - Early -> Fixed -> Update -> Late -> End 的完整单调序列里，只在第一个被调用的 Phase 里递增一次；
        // - Unity 真实顺序可能是 Fixed -> Early(由 TryGetMonoEntry 在 Update 开头模拟)，这仍视为同一帧；
        // - 同一 Phase 连续重复调用（例如测试直接多次调用 Update）视为每次进入新帧；
        // - 例外：Unity 一帧内可能连续多次 FixedUpdate，Fixed 时间应累加，但 FrameCount 仍只递增一次；
        // - Phase 顺序回退或回到更早的 Phase（例如 EndOfFrame 后再次 Update）也视为进入新帧。
        // 这样可以兼容完整帧、Unity Fixed-before-Update、部分帧和重复 Update 调用。
        private FramePhase _lastProcessedPhase = FramePhase.EndOfFrame;
        private bool _hasProcessedAnyPhase;
        private bool _currentFrameStartedWithFixedUpdate;
        private bool _lastEndOfFrameFollowedLateUpdate;

        // Phase 队列：每个 Phase 维护独立的 Yield/Delay/FrameWait 队列
        private readonly Dictionary<FramePhase, YieldQueues> _yieldQueuesByPhase;
        private readonly Dictionary<FramePhase, List<DelayedEntry>> _delayQueuesByPhase;
        private readonly Dictionary<FramePhase, List<FrameEntry>> _frameWaitQueuesByPhase;

        // ============= 全局 UnobservedException 钩子 =============

        /// <summary>
        /// 未观察到的 TGTask 异常的全局钩子。对标 UniTaskScheduler.UnobservedTaskException。
        ///
        /// 触发条件：<see cref="TGTask.Forget"/> 被调用后内部发生异常。
        /// 用法：业务在 GameLauncher 时订阅，统一上报到 LogModule。
        ///
        /// 静态事件 — 全局唯一，跨多 ModuleSystem 实例共享（与 .NET TaskScheduler.UnobservedTaskException 一致）。
        /// </summary>
        public static event Action<Exception> UnobservedException;

        /// <summary>内部触发（由 TGTask.Forget 路径调用）。</summary>
        internal static void RaiseUnobservedException(Exception ex)
        {
            if (ex == null) return;
            var handler = UnobservedException;
            if (handler != null)
            {
                try { handler(ex); }
                catch { /* 吞 handler 异常防止级联 */ }
            }
        }

        // ============= 内部数据结构 =============

        // 每个排队项携带 tcs + 取消 registration（完成时 Dispose 注销，避免 owner-scope 累积注册，ADR-0021 D7）。
        private readonly struct YieldItem
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly TGCancelRegistration Reg;
            public YieldItem(TGTaskCompletionSource tcs, TGCancelRegistration reg)
            {
                Tcs = tcs;
                Reg = reg;
            }
        }

        private class YieldQueues
        {
            public List<YieldItem> ThisFrame = new List<YieldItem>();
            public List<YieldItem> NextFrame = new List<YieldItem>();
        }

        private readonly struct DelayedEntry
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly float DueTime;
            public readonly TimeMode TimeMode; // C3：区分 Scaled / Unscaled
            public readonly TGCancelRegistration Reg;
            public DelayedEntry(TGTaskCompletionSource tcs, float dueTime, TimeMode timeMode, TGCancelRegistration reg)
            {
                Tcs = tcs;
                DueTime = dueTime;
                TimeMode = timeMode;
                Reg = reg;
            }
        }

        private readonly struct FrameEntry
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly long DueFrame;
            public readonly TGCancelRegistration Reg;
            public FrameEntry(TGTaskCompletionSource tcs, long dueFrame, TGCancelRegistration reg)
            {
                Tcs = tcs;
                DueFrame = dueFrame;
                Reg = reg;
            }
        }

        // ============= 构造函数 =============

        public TGTaskScheduler()
        {
            _yieldQueuesByPhase = new Dictionary<FramePhase, YieldQueues>();
            _delayQueuesByPhase = new Dictionary<FramePhase, List<DelayedEntry>>();
            _frameWaitQueuesByPhase = new Dictionary<FramePhase, List<FrameEntry>>();

            // 为每个 Phase 初始化队列
            foreach (FramePhase phase in Enum.GetValues(typeof(FramePhase)))
            {
                _yieldQueuesByPhase[phase] = new YieldQueues();
                _delayQueuesByPhase[phase] = new List<DelayedEntry>();
                _frameWaitQueuesByPhase[phase] = new List<FrameEntry>();
            }
        }

        // ============= IModule =============

        public void OnInit(IModuleSystem host)
        {
            // 无依赖；保持空实现
        }

        public void Shutdown()
        {
            // 取消所有未完成的 tcs（避免 await 永远 hang），并注销其取消 registration
            foreach (var queues in _yieldQueuesByPhase.Values)
            {
                CancelYieldList(queues.ThisFrame);
                CancelYieldList(queues.NextFrame);
                queues.ThisFrame.Clear();
                queues.NextFrame.Clear();
            }

            foreach (var delayQueue in _delayQueuesByPhase.Values)
            {
                for (int i = 0; i < delayQueue.Count; i++)
                {
                    var e = delayQueue[i];
                    e.Reg.Dispose();
                    if (!e.Tcs.Task.IsCompleted) e.Tcs.SetCanceled();
                    TGTaskCompletionSource.Recycle(e.Tcs);
                }
                delayQueue.Clear();
            }

            foreach (var frameQueue in _frameWaitQueuesByPhase.Values)
            {
                for (int i = 0; i < frameQueue.Count; i++)
                {
                    var e = frameQueue[i];
                    e.Reg.Dispose();
                    if (!e.Tcs.Task.IsCompleted) e.Tcs.SetCanceled();
                    TGTaskCompletionSource.Recycle(e.Tcs);
                }
                frameQueue.Clear();
            }

            _elapsedTime = 0;
            _unscaledElapsedTime = 0;
            _fixedElapsedTime = 0;
            _fixedUnscaledElapsedTime = 0;
            _frameCount = 0;
            _lastProcessedPhase = FramePhase.EndOfFrame;
            _hasProcessedAnyPhase = false;
            _currentFrameStartedWithFixedUpdate = false;
            _lastEndOfFrameFollowedLateUpdate = false;
        }

        private static void CancelYieldList(List<YieldItem> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                item.Reg.Dispose();
                if (!item.Tcs.Task.IsCompleted) item.Tcs.SetCanceled();
                TGTaskCompletionSource.Recycle(item.Tcs);
            }
        }

        // ============= 公共 API =============

        public TGTask Yield() => Yield(FramePhase.Update, TGCancelToken.None);
        public TGTask Yield(FramePhase phase) => Yield(phase, TGCancelToken.None);
        public TGTask Yield(TGCancelToken token) => Yield(FramePhase.Update, token);

        public TGTask Yield(FramePhase phase, TGCancelToken token)
        {
            if (token.IsCancellationRequested) return TGTask.FromCanceled();
            var tcs = TGTaskCompletionSource.Rent();
            var task = tcs.Task;
            var reg = RegisterCancel(tcs, token);
            _yieldQueuesByPhase[phase].NextFrame.Add(new YieldItem(tcs, reg));
            return task;
        }

        public TGTask Delay(float seconds) => Delay(seconds, FramePhase.Update, TimeMode.Scaled, TGCancelToken.None);
        public TGTask Delay(float seconds, FramePhase phase) => Delay(seconds, phase, TimeMode.Scaled, TGCancelToken.None);
        public TGTask Delay(float seconds, TimeMode timeMode) => Delay(seconds, FramePhase.Update, timeMode, TGCancelToken.None);
        public TGTask Delay(float seconds, TGCancelToken token) => Delay(seconds, FramePhase.Update, TimeMode.Scaled, token);
        public TGTask Delay(float seconds, FramePhase phase, TimeMode timeMode) => Delay(seconds, phase, timeMode, TGCancelToken.None);

        public TGTask Delay(float seconds, FramePhase phase, TimeMode timeMode, TGCancelToken token)
        {
            if (token.IsCancellationRequested) return TGTask.FromCanceled();
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Delay seconds must be >= 0");

            var tcs = TGTaskCompletionSource.Rent();
            var task = tcs.Task;
            var reg = RegisterCancel(tcs, token);
            if (seconds == 0)
            {
                _yieldQueuesByPhase[phase].NextFrame.Add(new YieldItem(tcs, reg));
            }
            else
            {
                float dueTime = GetCurrentTime(phase, timeMode) + seconds;
                _delayQueuesByPhase[phase].Add(new DelayedEntry(tcs, dueTime, timeMode, reg));
            }
            return task;
        }

        public TGTask WaitForFrames(int frameCount) => WaitForFrames(frameCount, FramePhase.Update, TGCancelToken.None);
        public TGTask WaitForFrames(int frameCount, FramePhase phase) => WaitForFrames(frameCount, phase, TGCancelToken.None);
        public TGTask WaitForFrames(int frameCount, TGCancelToken token) => WaitForFrames(frameCount, FramePhase.Update, token);

        public TGTask WaitForFrames(int frameCount, FramePhase phase, TGCancelToken token)
        {
            if (token.IsCancellationRequested) return TGTask.FromCanceled();
            if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount), "frameCount must be >= 0");
            if (frameCount == 0)
            {
                // 0 帧 = 立即完成，无须排队，零分配
                return TGTask.CompletedTask;
            }

            var tcs = TGTaskCompletionSource.Rent();
            var task = tcs.Task;
            var reg = RegisterCancel(tcs, token);
            _frameWaitQueuesByPhase[phase].Add(new FrameEntry(tcs, _frameCount + frameCount, reg));
            return task;
        }

        public TGTask DelayUntilPhase(FramePhase phase) => Yield(phase);

        /// <summary>
        /// 取消注册：token 取消时，把仍 pending 的 tcs 置为 canceled（body SetException OCE）。
        ///
        /// 关键取舍（ADR-0021 D6/D7，见 TGTaskBody 读码结论）：取消回调**只** SetCanceled，不主动移除队列 entry，
        /// 因为 body 的 SetResult/SetException 幂等、且 tcs 一旦被 await 消费侧回收+Reset 后 version 会变，
        /// 此时由 ProcessXxxQueue 统一用 <c>tcs.Task.IsCompleted</c> 检测并跳过 SetResult、回收 entry + 注销 reg。
        /// guard <c>!IsCompleted</c> 防止对已完成/已回收的 tcs 误操作。None token 返回 inert registration。
        /// </summary>
        private static TGCancelRegistration RegisterCancel(TGTaskCompletionSource tcs, TGCancelToken token)
        {
            if (token.Source == null) return default; // None：永不取消，无需注册
            return token.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetCanceled();
            });
        }

        // ============= IUpdateModule 接口实现 =============

        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.EarlyUpdate, deltaTime, unscaledDeltaTime);
        }

        public void FixedUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.FixedUpdate, deltaTime, unscaledDeltaTime);
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.Update, deltaTime, unscaledDeltaTime);
        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.LateUpdate, deltaTime, unscaledDeltaTime);
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.EndOfFrame, deltaTime, unscaledDeltaTime);
        }

        // ============= 内部处理逻辑 =============

        private void ProcessPhase(FramePhase phase, float deltaTime, float unscaledDeltaTime)
        {
            // 以 Phase 顺序判断帧边界：
            // - 完整帧循环中 Phase 单调递增，只在 EarlyUpdate 递增一次。
            // - 直接重复调用同一 Phase（如测试直接调用 Update）视为每次进入新帧。
            // - Phase 顺序回退（如 EndOfFrame 后再次 Update）视为新帧。
            bool startsNewFrame = StartsNewFrame(phase);
            if (startsNewFrame)
            {
                _frameCount++;
                _currentFrameStartedWithFixedUpdate = phase == FramePhase.FixedUpdate;
            }
            if (phase == FramePhase.FixedUpdate)
            {
                _fixedElapsedTime += deltaTime;
                _fixedUnscaledElapsedTime += unscaledDeltaTime;
            }
            else if (ShouldAccumulateElapsedTime(phase, startsNewFrame))
            {
                _elapsedTime += deltaTime;
                _unscaledElapsedTime += unscaledDeltaTime;
            }
            bool endOfFrameFollowedLateUpdate =
                phase == FramePhase.EndOfFrame &&
                _lastProcessedPhase == FramePhase.LateUpdate;
            _lastProcessedPhase = phase;
            _hasProcessedAnyPhase = true;
            if (phase == FramePhase.EndOfFrame)
                _lastEndOfFrameFollowedLateUpdate = endOfFrameFollowedLateUpdate;
            if (phase == FramePhase.EarlyUpdate)
                _currentFrameStartedWithFixedUpdate = false;

            // 1. 处理 Yield 队列
            ProcessYieldQueue(phase);

            // 2. 处理 Delay 队列
            ProcessDelayQueue(phase);

            // 3. 处理 FrameWait 队列
            ProcessFrameWaitQueue(phase);
        }

        private bool StartsNewFrame(FramePhase phase)
        {
            if (!_hasProcessedAnyPhase)
                return true;

            if (_currentFrameStartedWithFixedUpdate &&
                phase == FramePhase.EarlyUpdate)
            {
                return false;
            }

            if (_currentFrameStartedWithFixedUpdate &&
                _lastProcessedPhase == FramePhase.FixedUpdate &&
                phase == FramePhase.FixedUpdate)
            {
                return false;
            }

            if (_lastProcessedPhase == FramePhase.EndOfFrame &&
                phase == FramePhase.LateUpdate &&
                !_lastEndOfFrameFollowedLateUpdate)
            {
                return false;
            }

            return phase <= _lastProcessedPhase;
        }

        private bool ShouldAccumulateElapsedTime(FramePhase phase, bool startsNewFrame)
        {
            if (phase == FramePhase.FixedUpdate)
                return false;

            return startsNewFrame ||
                   (_currentFrameStartedWithFixedUpdate && phase == FramePhase.EarlyUpdate);
        }

        private float GetCurrentTime(FramePhase phase, TimeMode timeMode)
        {
            if (phase == FramePhase.FixedUpdate)
                return timeMode == TimeMode.Scaled ? _fixedElapsedTime : _fixedUnscaledElapsedTime;

            return timeMode == TimeMode.Scaled ? _elapsedTime : _unscaledElapsedTime;
        }

        private void ProcessYieldQueue(FramePhase phase)
        {
            var queues = _yieldQueuesByPhase[phase];

            // Swap: NextFrame -> ThisFrame
            var tmp = queues.ThisFrame;
            queues.ThisFrame = queues.NextFrame;
            queues.NextFrame = tmp;
            queues.NextFrame.Clear();

            // 处理 ThisFrame 队列
            for (int i = 0; i < queues.ThisFrame.Count; i++)
            {
                var item = queues.ThisFrame[i];
                item.Reg.Dispose();                 // 注销取消注册（D7）
                if (!item.Tcs.Task.IsCompleted)     // 未被取消才完成；已取消则跳过 SetResult（body 已 OCE / 已回收）
                    item.Tcs.SetResult();
                // body 由 await 消费侧（Awaiter.GetResult）自动回池；
                // tcs 对象本身在此处立即回收复用（task 句柄在排队时已发出）
                TGTaskCompletionSource.Recycle(item.Tcs);
            }
            queues.ThisFrame.Clear();
        }

        private void ProcessDelayQueue(FramePhase phase)
        {
            var delayQueue = _delayQueuesByPhase[phase];

            for (int i = delayQueue.Count - 1; i >= 0; i--)
            {
                var e = delayQueue[i];

                // 已被取消（token）：清理 entry，不再 SetResult（避免 await 消费后 version mismatch 抛 Expired）
                if (e.Tcs.Task.IsCompleted)
                {
                    e.Reg.Dispose();
                    TGTaskCompletionSource.Recycle(e.Tcs);
                    delayQueue.RemoveAt(i);
                    continue;
                }

                float currentTime = GetCurrentTime(phase, e.TimeMode);
                if (e.DueTime <= currentTime)
                {
                    e.Reg.Dispose();
                    e.Tcs.SetResult();
                    TGTaskCompletionSource.Recycle(e.Tcs);
                    delayQueue.RemoveAt(i);
                }
            }
        }

        private void ProcessFrameWaitQueue(FramePhase phase)
        {
            var frameQueue = _frameWaitQueuesByPhase[phase];

            for (int i = frameQueue.Count - 1; i >= 0; i--)
            {
                var e = frameQueue[i];

                // 已被取消（token）：清理 entry，不再 SetResult
                if (e.Tcs.Task.IsCompleted)
                {
                    e.Reg.Dispose();
                    TGTaskCompletionSource.Recycle(e.Tcs);
                    frameQueue.RemoveAt(i);
                    continue;
                }

                if (e.DueFrame <= _frameCount)
                {
                    e.Reg.Dispose();
                    e.Tcs.SetResult();
                    TGTaskCompletionSource.Recycle(e.Tcs);
                    frameQueue.RemoveAt(i);
                }
            }
        }

        // ============= 诊断（测试用）=============

        public int PendingYieldCount => _yieldQueuesByPhase[FramePhase.Update].NextFrame.Count;
        public int PendingDelayCount => _delayQueuesByPhase[FramePhase.Update].Count;
        public int PendingFrameWaitCount => _frameWaitQueuesByPhase[FramePhase.Update].Count;
        public long FrameCount => _frameCount;
        public float ElapsedTime => _elapsedTime;
    }
}
