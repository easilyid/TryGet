namespace TryGet
{
    /// <summary>
    /// 帧循环阶段。
    ///
    /// 枚举值的大小顺序代表执行顺序，不可更改（TGTaskScheduler 依赖此顺序判断帧边界）。
    /// </summary>
    public enum FramePhase
    {
        EarlyUpdate = 0,
        FixedUpdate = 10,
        Update = 20,
        LateUpdate = 30,
        EndOfFrame = 40,
    }
}
