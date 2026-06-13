namespace TryGet
{
    /// <summary>
    /// C9：对象池诊断快照（只读计数器）。
    ///
    /// 用于诊断池命中率、泄漏、峰值等问题。所有字段单调递增或反映当前状态。
    /// </summary>
    public readonly struct PoolDiagnostics
    {
        /// <summary>累计 Rent 次数（包含 hit 和 miss）。</summary>
        public readonly long TotalRented;

        /// <summary>累计 Return 次数。</summary>
        public readonly long TotalReturned;

        /// <summary>当前活跃对象数（Rent 未 Return）= TotalRented - TotalReturned。</summary>
        public readonly int CurrentActive;

        /// <summary>当前池内空闲对象数（等价 IObjectPool.IdleCount）。</summary>
        public readonly int IdleCount;

        /// <summary>历史峰值活跃对象数（用于判断池容量是否足够）。</summary>
        public readonly int PeakActive;

        /// <summary>从池取出的次数（hit from idle，Rent 时 idle > 0）。</summary>
        public readonly long HitCount;

        /// <summary>从 factory 新建的次数（miss，Rent 时 idle = 0）。</summary>
        public readonly long MissCount;

        public PoolDiagnostics(long totalRented, long totalReturned, int currentActive, int idleCount, int peakActive, long hitCount, long missCount)
        {
            TotalRented = totalRented;
            TotalReturned = totalReturned;
            CurrentActive = currentActive;
            IdleCount = idleCount;
            PeakActive = peakActive;
            HitCount = hitCount;
            MissCount = missCount;
        }
    }
}
