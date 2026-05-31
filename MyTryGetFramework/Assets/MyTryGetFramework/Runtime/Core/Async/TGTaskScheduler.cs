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
    /// 命名特别说明：类名 <c>TGTaskScheduler</c> 与 <see cref="System.Threading.Tasks.TaskScheduler"/> 区分开。
    /// 同时承担"全局 UnobservedException 钩子持有者"角色（对标 UniTaskScheduler.UnobservedTaskException）。
    /// </summary>
    public sealed class TGTaskScheduler : ITGTaskScheduler
    {
        public int Priority => -150;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        // 全局时间和帧计数
        private long _frameCount;
        private float _elapsedTime;

        // 用于基于 Phase 顺序判断帧边界。
        // 规则：
        // - Early -> Fixed -> Update -> Late -> End 的完整单调序列里，只在第一个被调用的 Phase 里递增一次；
        // - 同一 Phase 连续重复调用（例如测试直接多次调用 Update）视为进入新帧；
        // - Phase 顺序回退或回到更早的 Phase（例如 EndOfFrame 后再次 Update）也视为进入新帧。
        // 这样可以兼容完整帧、部分帧和重复 Update 调用，同时保持实现最小化。
        private FramePhase _lastProcessedPhase = FramePhase.EndOfFrame;

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

        private class YieldQueues
        {
            public List<TGTaskCompletionSource> ThisFrame = new List<TGTaskCompletionSource>();
            public List<TGTaskCompletionSource> NextFrame = new List<TGTaskCompletionSource>();
        }

        private readonly struct DelayedEntry
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly float DueTime;
            public DelayedEntry(TGTaskCompletionSource tcs, float dueTime) { Tcs = tcs; DueTime = dueTime; }
        }

        private readonly struct FrameEntry
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly long DueFrame;
            public FrameEntry(TGTaskCompletionSource tcs, long dueFrame) { Tcs = tcs; DueFrame = dueFrame; }
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
            // 取消所有未完成的 tcs（避免 await 永远 hang）
            foreach (var queues in _yieldQueuesByPhase.Values)
            {
                foreach (var tcs in queues.ThisFrame) tcs.SetCanceled();
                foreach (var tcs in queues.NextFrame) tcs.SetCanceled();
                queues.ThisFrame.Clear();
                queues.NextFrame.Clear();
            }

            foreach (var delayQueue in _delayQueuesByPhase.Values)
            {
                foreach (var e in delayQueue) e.Tcs.SetCanceled();
                delayQueue.Clear();
            }

            foreach (var frameQueue in _frameWaitQueuesByPhase.Values)
            {
                foreach (var e in frameQueue) e.Tcs.SetCanceled();
                frameQueue.Clear();
            }

            _elapsedTime = 0;
            _frameCount = 0;
            _lastProcessedPhase = FramePhase.EndOfFrame;
        }

        // ============= 公共 API =============

        public TGTask Yield()
        {
            return Yield(FramePhase.Update);
        }

        public TGTask Yield(FramePhase phase)
        {
            var tcs = new TGTaskCompletionSource();
            var queues = _yieldQueuesByPhase[phase];
            queues.NextFrame.Add(tcs);
            return tcs.Task;
        }

        public TGTask Delay(float seconds)
        {
            return Delay(seconds, FramePhase.Update);
        }

        public TGTask Delay(float seconds, FramePhase phase)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Delay seconds must be >= 0");
            var tcs = new TGTaskCompletionSource();
            if (seconds == 0)
            {
                _yieldQueuesByPhase[phase].NextFrame.Add(tcs);
            }
            else
            {
                _delayQueuesByPhase[phase].Add(new DelayedEntry(tcs, _elapsedTime + seconds));
            }
            return tcs.Task;
        }

        public TGTask WaitForFrames(int frameCount)
        {
            return WaitForFrames(frameCount, FramePhase.Update);
        }

        public TGTask WaitForFrames(int frameCount, FramePhase phase)
        {
            if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount), "frameCount must be >= 0");
            var tcs = new TGTaskCompletionSource();
            if (frameCount == 0)
            {
                tcs.SetResult();
                tcs.Return();
            }
            else
            {
                _frameWaitQueuesByPhase[phase].Add(new FrameEntry(tcs, _frameCount + frameCount));
            }
            return tcs.Task;
        }

        public TGTask DelayUntilPhase(FramePhase phase)
        {
            return Yield(phase);
        }

        // ============= IUpdateModule 接口实现 =============

        public void EarlyUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.EarlyUpdate, deltaTime);
        }

        public void FixedUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.FixedUpdate, deltaTime);
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.Update, deltaTime);
        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.LateUpdate, deltaTime);
        }

        public void EndOfFrame(float deltaTime, float unscaledDeltaTime)
        {
            ProcessPhase(FramePhase.EndOfFrame, deltaTime);
        }

        // ============= 内部处理逻辑 =============

        private void ProcessPhase(FramePhase phase, float deltaTime)
        {
            // 以 Phase 顺序判断帧边界：
            // - 完整帧循环中 Phase 单调递增，只在 EarlyUpdate 递增一次。
            // - 直接重复调用同一 Phase（如测试直接调用 Update）视为每次进入新帧。
            // - Phase 顺序回退（如 EndOfFrame 后再次 Update）视为新帧。
            if (phase <= _lastProcessedPhase)
            {
                _frameCount++;
                _elapsedTime += deltaTime;
            }
            _lastProcessedPhase = phase;

            // 1. 处理 Yield 队列
            ProcessYieldQueue(phase);

            // 2. 处理 Delay 队列
            ProcessDelayQueue(phase);

            // 3. 处理 FrameWait 队列
            ProcessFrameWaitQueue(phase);
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
                var tcs = queues.ThisFrame[i];
                tcs.SetResult();
                tcs.Return();
            }
            queues.ThisFrame.Clear();
        }

        private void ProcessDelayQueue(FramePhase phase)
        {
            var delayQueue = _delayQueuesByPhase[phase];

            for (int i = delayQueue.Count - 1; i >= 0; i--)
            {
                var e = delayQueue[i];
                if (e.DueTime <= _elapsedTime)
                {
                    e.Tcs.SetResult();
                    e.Tcs.Return();
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
                if (e.DueFrame <= _frameCount)
                {
                    e.Tcs.SetResult();
                    e.Tcs.Return();
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
