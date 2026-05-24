using System;
using System.Collections.Generic;

namespace TryGet.Async
{
    /// <summary>
    /// <see cref="ITGTaskScheduler"/> 的默认实现。
    ///
    /// 命名特别说明：类名 <c>TGTaskScheduler</c> 与 <see cref="System.Threading.Tasks.TaskScheduler"/> 区分开。
    /// 同时承担"全局 UnobservedException 钩子持有者"角色（对标 UniTaskScheduler.UnobservedTaskException）。
    ///
    /// 内部三组队列：
    /// - <c>_yieldThisFrame / _yieldNextFrame</c>：双 buffer swap。<see cref="Yield"/> 加入 next，
    ///   <see cref="Update"/> 时 swap 到 this 后处理（保证"下一帧"语义）。
    /// - <c>_delayed</c>：按到期时间存的 list（无序，每帧线性 scan）。typical N=10~50，O(N) 可接受。
    /// - <c>_frameWait</c>：同上，按 dueFrame 存。
    /// </summary>
    public sealed class TGTaskScheduler : ITGTaskScheduler
    {
        public int Priority => -150;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        // 双 buffer Yield 队列
        private List<TGTaskCompletionSource> _yieldThisFrame = new List<TGTaskCompletionSource>();
        private List<TGTaskCompletionSource> _yieldNextFrame = new List<TGTaskCompletionSource>();

        // Delay 队列
        private readonly struct DelayedEntry
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly float DueTime;
            public DelayedEntry(TGTaskCompletionSource tcs, float dueTime) { Tcs = tcs; DueTime = dueTime; }
        }
        private readonly List<DelayedEntry> _delayed = new List<DelayedEntry>();
        private float _elapsedTime;

        // Frame-wait 队列
        private readonly struct FrameEntry
        {
            public readonly TGTaskCompletionSource Tcs;
            public readonly long DueFrame;
            public FrameEntry(TGTaskCompletionSource tcs, long dueFrame) { Tcs = tcs; DueFrame = dueFrame; }
        }
        private readonly List<FrameEntry> _frameWait = new List<FrameEntry>();
        private long _frameCount;

        // ============= 全局 UnobservedException 钩子 =============

        /// <summary>
        /// 未观察到的 TGTask 异常的全局钩子。对标 UniTaskScheduler.UnobservedTaskException。
        ///
        /// 触发条件：<see cref="TGTask.Forget"/> 被调用后内部发生异常。
        /// 用法：业务在 Bootstrap 时订阅，统一上报到 LogModule。
        ///
        /// 静态事件 — 全局唯一，跨多 ModuleHost 实例共享（与 .NET TaskScheduler.UnobservedTaskException 一致）。
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

        // ============= IModule / IUpdateModule =============

        public void OnInit(IModuleHost host)
        {
            // 无依赖；保持空实现
        }

        public void Shutdown()
        {
            // 取消所有未完成的 tcs（避免 await 永远 hang）
            foreach (var tcs in _yieldThisFrame) tcs.SetCanceled();
            foreach (var tcs in _yieldNextFrame) tcs.SetCanceled();
            foreach (var e in _delayed) e.Tcs.SetCanceled();
            foreach (var e in _frameWait) e.Tcs.SetCanceled();

            _yieldThisFrame.Clear();
            _yieldNextFrame.Clear();
            _delayed.Clear();
            _frameWait.Clear();
            _elapsedTime = 0;
            _frameCount = 0;
        }

        public TGTask Yield()
        {
            var tcs = new TGTaskCompletionSource();
            _yieldNextFrame.Add(tcs);
            return tcs.Task;
        }

        public TGTask Delay(float seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Delay seconds must be >= 0");
            var tcs = new TGTaskCompletionSource();
            if (seconds == 0)
            {
                _yieldNextFrame.Add(tcs);
            }
            else
            {
                _delayed.Add(new DelayedEntry(tcs, _elapsedTime + seconds));
            }
            return tcs.Task;
        }

        public TGTask WaitForFrames(int frameCount)
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
                _frameWait.Add(new FrameEntry(tcs, _frameCount + frameCount));
            }
            return tcs.Task;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            _frameCount++;
            _elapsedTime += deltaTime;

            // --- Yield 队列：swap + 清空 + 处理 ---
            var tmp = _yieldThisFrame;
            _yieldThisFrame = _yieldNextFrame;
            _yieldNextFrame = tmp;
            _yieldNextFrame.Clear();

            for (int i = 0; i < _yieldThisFrame.Count; i++)
            {
                var tcs = _yieldThisFrame[i];
                tcs.SetResult();
                tcs.Return();
            }
            _yieldThisFrame.Clear();

            // --- Delay 队列 ---
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                var e = _delayed[i];
                if (e.DueTime <= _elapsedTime)
                {
                    e.Tcs.SetResult();
                    e.Tcs.Return();
                    _delayed.RemoveAt(i);
                }
            }

            // --- Frame-wait 队列 ---
            for (int i = _frameWait.Count - 1; i >= 0; i--)
            {
                var e = _frameWait[i];
                if (e.DueFrame <= _frameCount)
                {
                    e.Tcs.SetResult();
                    e.Tcs.Return();
                    _frameWait.RemoveAt(i);
                }
            }
        }

        // ---- 诊断（测试用）----
        public int PendingYieldCount => _yieldNextFrame.Count;
        public int PendingDelayCount => _delayed.Count;
        public int PendingFrameWaitCount => _frameWait.Count;
        public long FrameCount => _frameCount;
        public float ElapsedTime => _elapsedTime;
    }
}
