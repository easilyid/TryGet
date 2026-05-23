using System;

namespace TryGet
{
    /// <summary>
    /// 对象池服务契约（Common Module）。统一管理多个类型的 IObjectPool 实例。
    /// </summary>
    public interface IPoolModule : IModule
    {
        /// <summary>
        /// 为类型 T 创建池（或返回已有池）。同一类型多次调用返回同一池实例。
        /// </summary>
        /// <param name="factory">无可用对象时新建的工厂。必填。</param>
        /// <param name="onReturn">归还时重置状态的钩子。可空。</param>
        /// <param name="initialSize">初始预填充数量。默认 0。</param>
        IObjectPool<T> GetOrCreatePool<T>(Func<T> factory, Action<T> onReturn = null, int initialSize = 0)
            where T : class;

        /// <summary>
        /// 销毁类型 T 的池（如果存在）。
        /// </summary>
        bool DestroyPool<T>() where T : class;
    }
}
