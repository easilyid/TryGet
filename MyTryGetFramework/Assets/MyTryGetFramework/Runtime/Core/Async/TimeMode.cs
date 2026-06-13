namespace TryGet.Async
{
    /// <summary>
    /// C3：时间模式（受 Time.timeScale 影响与否）。
    ///
    /// 用于 <see cref="ITGTaskScheduler.Delay"/> 等方法区分 scaled time（受暂停/慢动作影响）
    /// 和 unscaled time（真实时间流逝，不受暂停影响）。
    /// </summary>
    public enum TimeMode
    {
        /// <summary>Scaled time（受 Time.timeScale 影响）。对应 Unity Time.deltaTime。</summary>
        Scaled = 0,

        /// <summary>Unscaled time（真实时间，不受 Time.timeScale 影响）。对应 Unity Time.unscaledDeltaTime。</summary>
        Unscaled = 1,
    }
}
