namespace TryGet.Shared
{
    /// <summary>
    /// V1.0 Iter 1 — Samples/Shared csproj 标识。
    ///
    /// 此 csproj 是"双端共享业务代码"承载层（详见 <c>docs/adr/0018-shared-code-boundary.md</c>）。
    /// V1.0 期间仅含此 marker 类；后续业务 Aspect / 网络消息 / Module 接口陆续填入。
    /// </summary>
    public static class SharedInfo
    {
        /// <summary>Shared csproj 的版本标识。与框架 minor 版本同步更新。</summary>
        public const string Version = "1.0.0";
    }
}
