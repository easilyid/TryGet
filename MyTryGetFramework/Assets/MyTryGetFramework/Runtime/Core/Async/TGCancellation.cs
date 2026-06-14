using System;
using System.Collections.Generic;

namespace TryGet.Async
{
    /// <summary>
    /// 取消信号句柄（<see langword="readonly struct"/>，零分配传递）。对标 .NET <c>CancellationToken</c> 的"句柄"角色，
    /// 但单线程、零锁、无 class 分配。<c>default</c> = <see cref="None"/>（永不取消，不持 <see cref="TGCancelSource"/>）。
    ///
    /// 设计见 ADR-0021（D1 自研、D2 token 式、D6 version 守卫、D9 命名）。取消语义为异常型：
    /// 触发后绑定的 pending <see cref="TGTask"/> 以 <see cref="TGTaskAbortException"/> 完成。
    /// </summary>
    public readonly struct TGCancelToken
    {
        internal readonly TGCancelSource Source;
        internal readonly int Version; // 快照 source 世代；防 source 池化复用后旧 token 误判

        internal TGCancelToken(TGCancelSource source, int version)
        {
            Source = source;
            Version = version;
        }

        /// <summary>永不取消的空 token（不持 source）。对标 <c>CancellationToken.None</c>。</summary>
        public static TGCancelToken None => default;

        /// <summary>是否已请求取消。None 或已回收世代的 token 恒为 false。</summary>
        public bool IsCancellationRequested =>
            Source != null && Source.IsCancelledFor(Version);

        /// <summary>已取消则抛 <see cref="TGTaskAbortException"/>；否则 no-op。用于纯计算 async 的协作检查点。</summary>
        public void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
                throw new TGTaskAbortException();
        }

        /// <summary>
        /// 注册取消回调。若 token 已取消则立即同步调用 <paramref name="callback"/>。
        /// 返回的 <see cref="TGCancelRegistration"/> 用于注销（<see cref="TGCancelRegistration.Dispose"/>）。
        /// None token 返回 inert registration（Dispose 为 no-op）。
        /// </summary>
        public TGCancelRegistration Register(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (Source == null) return default; // None：永不取消，无需注册
            return Source.Register(Version, callback);
        }
    }

    /// <summary>
    /// 取消信号源（class，池化）。owner（Module / Procedure / 未来 UI / Scene）持有，生命周期结束时 <see cref="Cancel"/>。
    /// 单线程、零锁。见 ADR-0021（D2/D4/D6/D7/D8）。
    ///
    /// **生命周期**：<see cref="Rent"/> → 派发 <see cref="Token"/> 给异步操作 → <see cref="Cancel"/>（一次取消全部 1:N）
    /// → 可选 <see cref="Recycle"/> 回池（<c>_version++</c> 使所有遗留 token/registration 失效）。
    /// </summary>
    public sealed class TGCancelSource
    {
        internal const int MaxPoolSize = 64;
        private static readonly Stack<TGCancelSource> _pool = new Stack<TGCancelSource>();
        private static readonly Stack<Node> _nodePool = new Stack<Node>();

        /// <summary>注册节点（池化）。持回调 + slotVersion（防过期 registration 误注销）+ index（O(1) swap-remove）。</summary>
        internal sealed class Node
        {
            public Action Callback;
            public int SlotVersion;
            public int Index;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private bool _cancelled;
        private int _version;

        /// <summary>派生一个取消句柄。</summary>
        public TGCancelToken Token => new TGCancelToken(this, _version);

        /// <summary>当前是否已取消。</summary>
        public bool IsCancellationRequested => _cancelled;

        /// <summary>从池取一个就绪 source（业务也可直接 <c>new</c>）。</summary>
        public static TGCancelSource Rent()
        {
            return _pool.Count > 0 ? _pool.Pop() : new TGCancelSource();
        }

        /// <summary>
        /// 触发取消：同步调用所有已注册回调（各一次），然后清空。重复 <see cref="Cancel"/> 为 no-op。
        /// 采用 pop-last 遍历，使回调内注销其他 registration（swap-remove）/ 注册新回调（已取消→立即调用）均安全。
        /// </summary>
        public void Cancel()
        {
            if (_cancelled) return;
            _cancelled = true;

            while (_nodes.Count > 0)
            {
                int last = _nodes.Count - 1;
                var node = _nodes[last];
                _nodes.RemoveAt(last);
                var cb = node.Callback; // 先取回调，再归还节点（ReturnNode 会清空 Callback）
                ReturnNode(node);
                try { cb?.Invoke(); }
                catch { /* 单个取消回调异常不阻断其余取消 */ }
            }
        }

        /// <summary>归还到池：清空残留注册（不触发回调）、重置状态、<c>_version++</c> 使遗留 token/registration 失效。</summary>
        public void Recycle()
        {
            for (int i = 0; i < _nodes.Count; i++)
                ReturnNode(_nodes[i]);
            _nodes.Clear();
            _cancelled = false;
            unchecked { _version++; }
            if (_pool.Count < MaxPoolSize) _pool.Push(this);
        }

        /// <summary>
        /// 超时取消（ADR-0021 D10 组合子）：经过 <paramref name="seconds"/> 秒后自动 <see cref="Cancel"/>。
        /// 用 <paramref name="scheduler"/> 的 Delay 实现；若 source 在到时前已被取消 / 回收，则不重复取消（version 守卫）。
        /// </summary>
        public void CancelAfter(float seconds, ITGTaskScheduler scheduler)
        {
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            if (_cancelled) return;
            CancelAfterImpl(scheduler, seconds, _version).Forget();
        }

        private async TGTask CancelAfterImpl(ITGTaskScheduler scheduler, float seconds, int version)
        {
            await scheduler.Delay(seconds);
            // 到时才取消；source 若已被取消/回收（version 变）则不动，避免误取消复用后的新世代
            if (_version == version && !_cancelled)
                Cancel();
        }

        // ---- 内部：供 TGCancelToken / TGCancelRegistration 调用 ----

        internal bool IsCancelledFor(int tokenVersion)
        {
            // version 不匹配 = token 来自已回收的旧世代 = 视为死 token（未取消）
            return tokenVersion == _version && _cancelled;
        }

        internal TGCancelRegistration Register(int tokenVersion, Action callback)
        {
            if (tokenVersion != _version)
                return default; // 过期 token：inert
            if (_cancelled)
            {
                // 已取消：立即同步调用，不入列表
                try { callback(); } catch { }
                return default;
            }

            var node = RentNode();
            node.Callback = callback;
            node.Index = _nodes.Count;
            _nodes.Add(node);
            return new TGCancelRegistration(this, node, node.SlotVersion);
        }

        internal void Deregister(Node node, int slotVersion)
        {
            if (node == null || node.SlotVersion != slotVersion)
                return; // 过期 / 已注销 / 节点已被复用
            int idx = node.Index;
            if (idx < 0 || idx >= _nodes.Count || _nodes[idx] != node)
                return; // 已不在列表（例如已被 Cancel 摘除）

            // O(1) swap-remove
            int last = _nodes.Count - 1;
            var lastNode = _nodes[last];
            _nodes[idx] = lastNode;
            lastNode.Index = idx;
            _nodes.RemoveAt(last);
            ReturnNode(node);
        }

        private static Node RentNode()
        {
            return _nodePool.Count > 0 ? _nodePool.Pop() : new Node();
        }

        private static void ReturnNode(Node node)
        {
            node.Callback = null;
            node.Index = -1;
            unchecked { node.SlotVersion++; } // 失效所有指向它的 registration
            if (_nodePool.Count < MaxPoolSize) _nodePool.Push(node);
        }

        // ---- 诊断（测试用）----

        /// <summary>当前活跃注册数（诊断/测试用，用于校验长寿命 source 无 registration 泄漏）。</summary>
        public int RegistrationCount => _nodes.Count;
    }

    /// <summary>
    /// 取消回调的注销句柄（<see langword="readonly struct"/>）。<see cref="Dispose"/> 注销对应回调，
    /// 避免长寿命 <see cref="TGCancelSource"/> 累积已完成操作的注册（ADR-0021 D7）。
    /// </summary>
    public readonly struct TGCancelRegistration : IDisposable
    {
        private readonly TGCancelSource _source;
        private readonly TGCancelSource.Node _node;
        private readonly int _slotVersion;

        internal TGCancelRegistration(TGCancelSource source, TGCancelSource.Node node, int slotVersion)
        {
            _source = source;
            _node = node;
            _slotVersion = slotVersion;
        }

        /// <summary>注销回调。inert（None / 已取消立即触发）registration 的 Dispose 为 no-op。</summary>
        public void Dispose()
        {
            _source?.Deregister(_node, _slotVersion);
        }
    }
}
