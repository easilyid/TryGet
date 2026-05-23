namespace TryGet
{
    /// <summary>
    /// 指向 Entity 的弱引用句柄，需要时再解析目标。
    /// 目标 Entity 销毁后，Handle 解析返回 null。
    /// </summary>
    public readonly struct Handle
    {
        public static readonly Handle Invalid = new Handle(EntityId.Invalid);

        public readonly EntityId Id;

        public Handle(EntityId id)
        {
            Id = id;
        }

        /// <summary>
        /// 在指定 World 中解析目标 Entity。
        /// </summary>
        /// <returns>目标 Entity，若已销毁则返回 null。</returns>
        public Entity Resolve(World world)
        {
            if (!Id.IsValid || world == null)
                return null;
            return world.GetEntity(Id);
        }

        public bool IsValid => Id.IsValid;

        public override bool Equals(object obj)
        {
            return obj is Handle other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(Handle left, Handle right)
        {
            return left.Id == right.Id;
        }

        public static bool operator !=(Handle left, Handle right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"Handle({Id})";
        }
    }
}
