using System;

namespace TryGet
{
    /// <summary>
    /// World-level 事件派发接口（V0.1 兼容别名）。
    /// V0.2 起请使用 <see cref="IEventBus"/>，本接口仅为兼容保留，签名 100% 相同。
    /// </summary>
    public interface IWorldEventBus : IEventBus
    {
    }
}
