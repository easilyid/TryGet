namespace TryGet
{
    /// <summary>
    /// 内置插点：<see cref="IModuleHost.Update"/> 入口（在 IUpdateModule 调度之前）触发。
    /// </summary>
    public interface IModuleHostBeforeUpdate : IPlugPoint
    {
        void OnBeforeUpdate(IModuleHost host, float deltaTime, float unscaledDeltaTime);
    }

    /// <summary>
    /// 内置插点：<see cref="IModuleHost.Update"/> 出口（在 IUpdateModule 调度之后）触发。
    /// </summary>
    public interface IModuleHostAfterUpdate : IPlugPoint
    {
        void OnAfterUpdate(IModuleHost host, float deltaTime, float unscaledDeltaTime);
    }
}
