namespace TryGet
{
    /// <summary>
    /// 框架根容器契约（ADR-0011）。承载所有 IModule、负责注册/启停/查询、暴露全局事件总线。
    ///
    /// 非线程安全：所有方法须在主线程（Unity 主线程或测试线程）调用。
    ///
    /// 使用模式：
    /// 1. GameLauncher 阶段：调用 <see cref="Register{T}"/> 注册所有 Module 实例
    /// 2. 调用 <see cref="Initialize"/> 触发拓扑排序 + 依次 OnInit
    /// 3. 运行期：Module 内部通过 <see cref="Get{T}"/> 拉依赖，通过 <see cref="EventModule"/> 收发事件
    /// 4. 帧驱动：EarlyUpdate / FixedUpdate / Update / LateUpdate / EndOfFrame
    /// 5. 关闭：<see cref="Shutdown"/> 按 OnInit 逆序执行
    /// </summary>
    public interface IModuleSystem
    {
        /// <summary>
        /// 全局事件总线。所有 Module 共享。
        /// </summary>
        IEventModule EventModule { get; }

        /// <summary>
        /// 已 Initialize 后为 true。Initialize 前 false；Shutdown 后回到 false。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 注册 Module 实例（必须在 Initialize 前）。
        /// T 必须是 Module 自身定义的服务接口（如 <c>ILogger</c>），不能是框架基础接口
        /// （<see cref="IModule"/> / <see cref="IEarlyUpdateModule"/> / <see cref="IFixedUpdateModule"/> / <see cref="IUpdateModule"/> / <see cref="ILateUpdateModule"/> / <see cref="IEndOfFrameModule"/> / <see cref="IEventModule"/>），
        /// 也不能是具体类。同一接口重复注册抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void Register<T>(T module) where T : class, IModule;

        /// <summary>
        /// 获取已注册的 Module。未注册时抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        T Get<T>() where T : class, IModule;

        /// <summary>
        /// 尝试获取已注册的 Module。未注册返回 false。
        /// </summary>
        bool TryGet<T>(out T module) where T : class, IModule;

        /// <summary>
        /// 按 DependsOn 拓扑序（Priority tie-breaker）依次调用所有 Module 的 OnInit。
        /// 检测循环依赖与未注册依赖，发现时抛 <see cref="System.InvalidOperationException"/>。
        /// 重复调用抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 按 OnInit 顺序依次调用所有 <see cref="IEarlyUpdateModule"/> 实例的 EarlyUpdate。
        /// 仅在 <see cref="IsInitialized"/>=true 时有效，否则抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void EarlyUpdate(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 按 OnInit 顺序依次调用所有 <see cref="IFixedUpdateModule"/> 实例的 FixedUpdate。
        /// 仅在 <see cref="IsInitialized"/>=true 时有效，否则抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void FixedUpdate(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 按 OnInit 顺序依次调用所有 <see cref="IUpdateModule"/> 实例的 Update。
        /// 仅在 <see cref="IsInitialized"/>=true 时有效，否则抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void Update(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 按 OnInit 顺序依次调用所有 <see cref="ILateUpdateModule"/> 实例的 LateUpdate。
        /// 仅在 <see cref="IsInitialized"/>=true 时有效，否则抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void LateUpdate(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 按 OnInit 顺序依次调用所有 <see cref="IEndOfFrameModule"/> 实例的 EndOfFrame。
        /// 仅在 <see cref="IsInitialized"/>=true 时有效，否则抛 <see cref="System.InvalidOperationException"/>。
        /// </summary>
        void EndOfFrame(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 按 OnInit 的逆序依次调用所有 Module 的 Shutdown。允许多次调用（仅首次有效）。
        /// </summary>
        void Shutdown();
    }
}
