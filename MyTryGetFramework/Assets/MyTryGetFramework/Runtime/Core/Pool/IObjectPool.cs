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
        /// onReturn 抛异常时会被池隔离：对象仍完成归还并更新诊断计数，避免进入既非 active 也非 idle 的泄漏态。
        ///
        /// **重复 Return 同一对象是未定义行为**：会导致同一实例被 Rent 两次分发给不同 caller，引发隐蔽 bug。
        /// 调用方负责保证每个对象只 Return 一次。C9+ DEBUG/UNITY_ASSERTIONS 模式下通过 HashSet 检测重复 Return 并抛异常。
        /// </summary>
        void Return(T item);

        /// <summary>
        /// 池内当前空闲对象数量（用于诊断）。
        /// </summary>
        int IdleCount { get; }

        /// <summary>
        /// C9：获取诊断快照（计数器 + 峰值，用于分析命中率/泄漏/容量）。
        /// </summary>
        PoolDiagnostics GetDiagnostics();
    }
}
