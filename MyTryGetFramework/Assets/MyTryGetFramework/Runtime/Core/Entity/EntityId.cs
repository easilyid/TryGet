namespace TryGet
{
    /// <summary>
    /// 在单个 World 内唯一的实体标识。
    /// </summary>
    public readonly struct EntityId
    {
        public static readonly EntityId Invalid = new EntityId(0, 0);

        public readonly int Index;
        public readonly int Version;

        public EntityId(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public bool IsValid => Index > 0 && Version > 0;

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Index == other.Index && Version == other.Version;
        }

        public override int GetHashCode()
        {
            return (Index * 397) ^ Version;
        }

        public static bool operator ==(EntityId left, EntityId right)
        {
            return left.Index == right.Index && left.Version == right.Version;
        }

        public static bool operator !=(EntityId left, EntityId right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"EntityId({Index}:{Version})";
        }
    }
}
