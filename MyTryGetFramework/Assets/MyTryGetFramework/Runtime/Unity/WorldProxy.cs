using UnityEngine;

namespace TryGet.Unity
{
    /// <summary>
    /// Unity 侧的 EntityWorld 驱动器 — 单一 MonoBehaviour 入口（V0.3 版）。
    ///
    /// V0.1 → V0.3 变化：
    /// - 持有的类型从 V0.1 <c>World</c> 改为 V0.3 <see cref="EntityWorld"/>。
    /// - Update 帧驱动从无参 <c>world.Update()</c> 改为带 deltaTime 的
    ///   <see cref="EntityWorld.Update(float, float)"/>。
    /// - 不再依赖 V0.1 的 IWorldAdapter（已删除）。
    /// - 未来 V0.4 可升级为 ModuleHostProxy，承载 EntityWorld + 其他 Module 协同。
    /// </summary>
    public class WorldProxy : MonoBehaviour
    {
        private EntityWorld _world;
        private bool _started;

        /// <summary>
        /// 当前驱动的 EntityWorld 实例。
        /// </summary>
        public EntityWorld World => _world;

        protected virtual void Awake()
        {
            _world = new EntityWorld(gameObject.name);
        }

        protected virtual void Start()
        {
            OnConfigure(_world);
            _world.Start();
            _started = true;
        }

        protected virtual void Update()
        {
            if (_started && _world.State == EntityWorldState.Running)
            {
                _world.Update(Time.deltaTime, Time.unscaledDeltaTime);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_world != null && _world.State != EntityWorldState.Shutdown)
            {
                _world.Shutdown();
            }
            _world = null;
            _started = false;
        }

        /// <summary>
        /// 子类重写此方法来注册 System、SystemGroup 等。
        /// 在 EntityWorld.Start() 之前调用。
        /// </summary>
        protected virtual void OnConfigure(EntityWorld world)
        {
        }
    }
}
