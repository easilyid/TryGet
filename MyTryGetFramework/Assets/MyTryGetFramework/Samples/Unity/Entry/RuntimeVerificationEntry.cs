using TryGet;
using TryGet.Async;
using UnityEngine;

using TryGet.Unity;


/// <summary>
/// 运行时验证脚本 — 验证 Critical 修复的正确性
///
/// 使用方法：
/// 1. 创建新场景
/// 2. 添加 GameObject
/// 3. 添加此脚本
/// 4. 运行场景
/// 5. 查看 Console 输出
/// </summary>
public class RuntimeVerificationEntry : TryGetMonoEntry
{
    protected override void Setup(IModuleSystem host)
    {
        // ITGTaskScheduler 已由 GameLauncher.CreateHost 默认注册（见 GameLauncher.CreateHost）。
        // 这里不再重复注册——TryGetMonoEntry.Awake 先 CreateHost 再调本方法，重复 Register 同一
        // 服务接口会抛 ModuleAlreadyRegisteredException。本验证脚本直接用框架默认 scheduler 即可。
    }

    private async void Start()
    {
        var scheduler = Host.Get<ITGTaskScheduler>();

        Debug.Log("==========================================");
        Debug.Log("TryGet 框架运行时验证");
        Debug.Log("==========================================");
        Debug.Log("");

        // 测试 1：WaitForFrames(1) 应该等待 1 帧
        Debug.Log("测试 1：WaitForFrames(1)");
        var startFrame = Time.frameCount;
        await scheduler.WaitForFrames(1);
        var endFrame = Time.frameCount;
        var frameDiff = endFrame - startFrame;
        Debug.Log($"  开始帧: {startFrame}");
        Debug.Log($"  结束帧: {endFrame}");
        Debug.Log($"  等待帧数: {frameDiff}");
        Debug.Log($"  预期: 1 帧");
        Debug.Log($"  状态: {(frameDiff == 1 ? "✅ 通过" : "❌ 失败")}");
        Debug.Log("");

        // 测试 2：Delay(1.0f) 应该等待约 1 秒
        Debug.Log("测试 2：Delay(1.0f)");
        var startTime = Time.time;
        await scheduler.Delay(1.0f);
        var endTime = Time.time;
        var timeDiff = endTime - startTime;
        Debug.Log($"  开始时间: {startTime:F2}s");
        Debug.Log($"  结束时间: {endTime:F2}s");
        Debug.Log($"  等待时间: {timeDiff:F2}s");
        Debug.Log($"  预期: ~1.00s");
        Debug.Log($"  状态: {(Mathf.Abs(timeDiff - 1.0f) < 0.1f ? "✅ 通过" : "❌ 失败")}");
        Debug.Log("");

        Debug.Log("==========================================");
        Debug.Log("运行时验证完成");
        Debug.Log("==========================================");
    }
}
