using System;

namespace TryGet.Async
{
    /// <summary>
    /// TGTask body 已过期时抛出。典型情形：struct 实例被复制到别处后，原始 body 已被 Pool 回收，
    /// 复制的 struct 仍持旧 Version 试图 await，会触发本异常。
    /// </summary>
    public sealed class TGTaskExpiredException : InvalidOperationException
    {
        public TGTaskExpiredException()
            : base("TGTask body has expired (version mismatch). " +
                   "This typically means the struct was copied after the body was reset.")
        {
        }

        public TGTaskExpiredException(string message) : base(message) { }
    }
}
