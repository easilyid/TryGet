using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// 场景服务契约（V0.5 Common Module）。
    ///
    /// 设计原则：Core 层只定义"哪些场景已加载、哪个是活动场景"的状态机，
    /// **不依赖 UnityEngine.SceneManagement**。Adapters/Unity 层的
    /// UnitySceneModule 接 SceneManager.LoadSceneAsync 实现真实加载。
    ///
    /// 加载语义：Load/Unload 在 Core 是同步状态变更（场景立即标记为已加载/卸载）。
    /// 真实 Unity Adapter 实现可能是异步的（LoadSceneAsync 返回 AsyncOperation），
    /// 接口设计上由 Adapter 自行扩展 LoadAsync（不污染 Core）。
    ///
    /// 加法语义（Additive）：默认 Load 是"叠加加载"（与 Unity LoadSceneMode.Additive 一致）。
    /// 单场景模式（卸载其他场景再加载）由业务自行 UnloadAll + Load 实现。
    /// </summary>
    public interface ISceneModule : IModule
    {
        /// <summary>
        /// 当前活动场景。无场景或未 SetActive 时为 null。
        /// </summary>
        string ActiveScene { get; }

        /// <summary>
        /// 已加载的场景列表，按加载顺序。
        /// </summary>
        IReadOnlyList<string> LoadedScenes { get; }

        /// <summary>
        /// 已加载场景数量。
        /// </summary>
        int LoadedCount { get; }

        /// <summary>
        /// 加载（标记为已加载）一个场景。重复加载同名抛 <see cref="InvalidOperationException"/>。
        /// 第一个加载的场景自动成为 ActiveScene。
        /// </summary>
        void Load(string sceneName);

        /// <summary>
        /// 卸载场景。未加载时返回 false（幂等）。卸载 ActiveScene 后 ActiveScene 置 null，
        /// 业务需 SetActive 选择新活动场景。
        /// </summary>
        bool Unload(string sceneName);

        /// <summary>
        /// 设置活动场景。该场景必须已 Load，否则抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        void SetActive(string sceneName);

        /// <summary>
        /// 场景是否已加载。
        /// </summary>
        bool IsLoaded(string sceneName);

        /// <summary>
        /// 卸载所有场景。
        /// </summary>
        void UnloadAll();
    }
}
