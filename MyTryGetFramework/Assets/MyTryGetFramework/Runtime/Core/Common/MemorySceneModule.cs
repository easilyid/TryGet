using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// ISceneModule 的内存实现（跨端、零外部依赖、状态机 stub）。
    ///
    /// 用途：单元测试、Procedure 流程测试、Headless 服务端。
    /// **不实际加载场景**，只维护"哪些场景已加载、哪个活动"的状态。
    /// Production Unity 由 Adapters/Unity 层 UnitySceneModule（接 SceneManager.LoadSceneAsync）替换。
    /// </summary>
    public sealed class MemorySceneModule : ISceneModule
    {
        private readonly List<string> _ordered = new List<string>();
        private readonly HashSet<string> _index = new HashSet<string>();
        private string _active;

        // 介于 UI (-300) 与 Procedure (-200) 之间：场景切换由 Procedure 触发，
        // 但 UI / Audio 可能在场景切换前后做清理 / 重启，Scene 在它们之后初始化是合理的。
        public int Priority => -250;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string ActiveScene => _active;
        public IReadOnlyList<string> LoadedScenes => _ordered;
        public int LoadedCount => _ordered.Count;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
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

            _ordered.Add(sceneName);
            _index.Add(sceneName);

            // 第一个加载的场景自动成为 active
            if (_active == null)
                _active = sceneName;
        }

        public bool Unload(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;
            if (!_index.Remove(sceneName))
                return false;

            _ordered.Remove(sceneName); // O(n) acceptable，场景数量小

            // 卸载的是 active，置 null，业务需 SetActive 选新的
            if (_active == sceneName)
                _active = null;

            return true;
        }

        public void SetActive(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("Scene name must be non-empty.", nameof(sceneName));
            if (!_index.Contains(sceneName))
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not loaded. Load it first.");

            _active = sceneName;
        }

        public bool IsLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;
            return _index.Contains(sceneName);
        }

        public void UnloadAll()
        {
            _ordered.Clear();
            _index.Clear();
            _active = null;
        }
    }
}
