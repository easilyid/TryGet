using UnityEngine;

namespace TryGet.Unity
{
    /// <summary>
    /// Unity 侧的 World 驱动器 — 唯一的 MonoBehaviour 入口。
    /// 通过 Awake/Start/Update/OnDestroy 自然映射 World 生命周期；不实现 IWorldAdapter，
    /// 因为 MonoBehaviour 本身就是 Unity 的驱动契约（IWorldAdapter 用于非 Unity 宿主）。
    ///
    /// 职责：创建 World、转发 Unity 帧回调、在销毁时关闭 World。
    /// 核心运行时不依赖此类 — 此类依赖核心运行时。
    /// </summary>
    public class WorldProxy : MonoBehaviour
    {
        private World _world;
        private bool _started;

        /// <summary>
        /// 当前驱动的 World 实例。
        /// </summary>
        public World World => _world;

        protected virtual void Awake()
        {
            _world = new World(gameObject.name);
        }

        protected virtual void Start()
        {
            OnConfigure(_world);
            _world.Start();
            _started = true;
        }

        protected virtual void Update()
        {
            if (_started && _world.State == WorldState.Running)
            {
                _world.Update();
            }
        }

        protected virtual void OnDestroy()
        {
            if (_world != null && _world.State != WorldState.Shutdown)
            {
                _world.Shutdown();
            }
            _world = null;
            _started = false;
        }

        /// <summary>
        /// 子类重写此方法来注册 System、SystemGroup 等。
        /// 在 World.Start() 之前调用。
        /// </summary>
        protected virtual void OnConfigure(World world)
        {
        }
    }
}
