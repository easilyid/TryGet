using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace TryGet.Unity
{
    /// <summary>
    /// ISceneModule 的 Unity Adapter，接 <see cref="SceneManager"/>。
    ///
    /// 设计权衡：
    /// - **Load 同步、Unload 异步 fire-and-forget**：SceneManager 的卸载 API（UnloadSceneAsync）
    ///   是异步的，但 ISceneModule.Unload 是同步签名。Adapter 调 UnloadSceneAsync 后立即从
    ///   内部状态移除（不等真正卸载完成），保证 IsLoaded 立即返回 false。真实卸载在下一帧 frame end。
    /// - **内部 List 维护"已请求加载"状态**：与 <see cref="MemorySceneModule"/> 行为契约一致；
    ///   SceneManager 是底层 truth，Adapter 是 UI 视角的状态机。
    /// - **LoadSceneMode.Additive**：默认加法加载，与 Memory 实现"additive 语义"对齐。
    ///   单场景模式（卸载其他场景再加载）由业务 UnloadAll + Load 实现。
    /// - 业务的真实场景文件必须在 BuildSettings 的 Scenes in Build 中（这是 SceneManager 的硬约束）。
    /// </summary>
    public sealed class UnitySceneModule : ISceneModule
    {
        private readonly List<string> _ordered = new List<string>();
        private readonly HashSet<string> _index = new HashSet<string>();
        private string _active;

        public int Priority => -250;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string ActiveScene => _active;
        public IReadOnlyList<string> LoadedScenes => _ordered;
        public int LoadedCount => _ordered.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            // Adapter Shutdown：不卸载已加载的场景（与 Save Adapter 一致，运行时资源不应被 Shutdown 擦除）
            _ordered.Clear();
            _index.Clear();
            _active = null;
        }

        public void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("Scene name must be non-empty.", nameof(sceneName));
            if (_index.Contains(sceneName))
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' is already loaded. Unload first or use IsLoaded.");

            // 同步加载（Additive）。真实场景必须在 BuildSettings 中。
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);

            _ordered.Add(sceneName);
            _index.Add(sceneName);

            if (_active == null)
                _active = sceneName;
        }

        public bool Unload(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (!_index.Remove(sceneName)) return false;

            _ordered.Remove(sceneName);
            if (_active == sceneName) _active = null;

            // 异步卸载 fire-and-forget。即使 UnloadSceneAsync 返回 null（场景未找到），
            // Adapter 内部状态已经更新，IsLoaded 立即返回 false（符合 UI 视角语义）。
            try
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid())
                    SceneManager.UnloadSceneAsync(scene);
            }
            catch (Exception)
            {
                // SceneManager 未找到场景或在不合适时机卸载：忽略，Adapter 状态已清
            }
            return true;
        }

        public void SetActive(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("Scene name must be non-empty.", nameof(sceneName));
            if (!_index.Contains(sceneName))
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not loaded. Load it first.");

            // 真正切活动场景（Unity API）
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
            }
            _active = sceneName;
        }

        public bool IsLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return _index.Contains(sceneName);
        }

        public void UnloadAll()
        {
            // 反向遍历快照避免修改时索引错位
            var snapshot = new List<string>(_ordered);
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                Unload(snapshot[i]);
            }
        }
    }
}
