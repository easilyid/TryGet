namespace TryGet
{
    /// <summary>
    /// 指向 <see cref="Entity"/> 的弱引用句柄。
    /// 目标 Entity 销毁后，<see cref="Resolve"/> 返回 null —— 这是与"强引用 Entity"的本质区别。
    ///
    /// 命名：用 <c>EntityHandle</c> 而非泛化的 <c>Handle</c>（V0.6 Iter 8 改名），明确表达"指向 Entity 的句柄"语义，
    /// 避免与 <see cref="System.IntPtr"/> 等其它 Handle 概念混淆。对标：ET <c>EntityRef</c> / Unity <c>WeakReference&lt;Entity&gt;</c>。
    /// </summary>
    public readonly struct EntityHandle
    {
        public static readonly EntityHandle Invalid = new EntityHandle(EntityId.Invalid);

        public readonly EntityId Id;

        public EntityHandle(EntityId id)
        {
            Id = id;
        }

        /// <summary>
        /// 在指定 EntityWorld 中解析目标 Entity。
        /// </summary>
        /// <returns>目标 Entity，若已销毁则返回 null。</returns>
        public Entity Resolve(EntityWorld world)
        {
            if (!Id.IsValid || world == null)
                return null;
            return world.GetEntity(Id);
        }

        public bool IsValid => Id.IsValid;

        public override bool Equals(object obj)
        {
            return obj is EntityHandle other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(EntityHandle left, EntityHandle right)
        {
            return left.Id == right.Id;
        }

        public static bool operator !=(EntityHandle left, EntityHandle right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"EntityHandle({Id})";
        }
    }
}
