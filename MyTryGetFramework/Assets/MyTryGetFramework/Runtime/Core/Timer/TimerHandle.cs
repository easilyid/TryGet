namespace TryGet
{
    /// <summary>
    /// 定时器句柄。由 <see cref="ITimerModule.Schedule"/> 返回，可通过 <see cref="ITimerModule.Cancel"/> 取消。
    /// 已触发或已取消的句柄上再次调用 Cancel 返回 false。
    /// </summary>
    public readonly struct TimerHandle : System.IEquatable<TimerHandle>
    {
        public static readonly TimerHandle Invalid = new TimerHandle(0);

        internal readonly long Id;

        internal TimerHandle(long id) { Id = id; }

        public bool IsValid => Id != 0;

        public bool Equals(TimerHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TimerHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(TimerHandle a, TimerHandle b) => a.Id == b.Id;
        public static bool operator !=(TimerHandle a, TimerHandle b) => a.Id != b.Id;
        public override string ToString() => $"TimerHandle({Id})";
    }
}
