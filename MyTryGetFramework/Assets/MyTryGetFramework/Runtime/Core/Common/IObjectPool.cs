namespace TryGet
{
    /// <summary>
    /// 单类型对象池。由 <see cref="IPoolModule.CreatePool{T}"/> 创建。
    /// 不是 IModule，是 IPoolModule 管理的资源。
    /// </summary>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// 取出一个对象。池内无可用对象时调用 factory 新建。
        /// </summary>
        T Rent();

        /// <summary>
        /// 归还对象。归还前会调用注册时提供的 onReturn 回调（用于重置状态）。
        ///
        /// **重复 Return 同一对象是未定义行为**：会导致同一实例被 Rent 两次分发给不同 caller，引发隐蔽 bug。
        /// 调用方负责保证每个对象只 Return 一次。V0.3+ 可选 DEBUG HashSet 检测。
        /// </summary>
        void Return(T item);

        /// <summary>
        /// 池内当前空闲对象数量（用于诊断）。
        /// </summary>
        int IdleCount { get; }
    }
}
