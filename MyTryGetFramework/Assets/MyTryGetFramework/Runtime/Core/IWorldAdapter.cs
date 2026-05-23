namespace TryGet
{
    /// <summary>
    /// 外部驱动器契约：将 World 生命周期接入到 Unity 之外的运行环境（测试 Fake、headless 模拟、非 Unity 宿主）。
    ///
    /// Unity 侧通过 <c>WorldProxy</c>（MonoBehaviour）天然映射 Awake/Update/OnDestroy 到 World，
    /// 不需要实现此接口；本接口的存在是为了证明并约束"核心运行时可以脱离 Unity 被驱动"
    /// （Issue-18 Fake Adapter 的契约表达，ADR-0002）。
    /// </summary>
    public interface IWorldAdapter
    {
        /// <summary>
        /// 初始化 Adapter 并关联 World。调用方在调用前应已对 World 完成 System 注册。
        /// </summary>
        void Initialize(World world);

        /// <summary>
        /// 驱动 World 的一次 Update tick。
        /// </summary>
        void Tick();

        /// <summary>
        /// 关闭 Adapter 并清理资源。允许重入。
        /// </summary>
        void Dispose();
    }
}
