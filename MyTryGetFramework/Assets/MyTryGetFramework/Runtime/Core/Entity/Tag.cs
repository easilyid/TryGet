using System;

namespace TryGet
{
    /// <summary>
    /// 只表达存在与否的轻量标记，不承载状态。
    /// Tag 通过类型区分：每个 Tag 子类型代表一种标记。
    /// </summary>
    public abstract class Tag
    {
        /// <summary>
        /// 此 Tag 的类型标识，用于 Query 匹配。
        /// </summary>
        public virtual Type TagType => GetType();
    }
}
