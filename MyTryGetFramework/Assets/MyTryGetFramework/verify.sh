#!/bin/bash
# 验证脚本 — 2026/05/31
# 用于验证 TryGet 框架修复的正确性

set -e

echo "=========================================="
echo "TryGet 框架验证脚本"
echo "=========================================="
echo ""

# 1. 检查编译状态
echo "1. 检查代码编译状态..."
echo "   请在 Unity Editor 中确认："
echo "   - 打开 Unity Editor"
echo "   - 查看 Console 窗口"
echo "   - 确认无编译错误"
echo ""

# 2. 列出所有测试文件
echo "2. 测试文件清单："
find ./Tests/EditMode -name "*.cs" -type f | sort | while read file; do
    echo "   - $(basename $file)"
done
echo ""

# 3. 统计测试数量
echo "3. 测试统计："
total_files=$(find ./Tests/EditMode -name "*Tests.cs" -type f | wc -l)
echo "   - 测试文件数量: $total_files"
echo ""

# 4. 检查新增测试
echo "4. 新增测试验证："
if grep -q "WaitForFrames_MultiplePhases_DoesNotCountMultipleTimes" ./Tests/EditMode/TGTaskSchedulerTests.cs; then
    echo "   ✅ WaitForFrames_MultiplePhases_DoesNotCountMultipleTimes 存在"
else
    echo "   ❌ WaitForFrames_MultiplePhases_DoesNotCountMultipleTimes 缺失"
fi

if grep -q "Delay_MultiplePhases_DoesNotAccumulateMultipleTimes" ./Tests/EditMode/TGTaskSchedulerTests.cs; then
    echo "   ✅ Delay_MultiplePhases_DoesNotAccumulateMultipleTimes 存在"
else
    echo "   ❌ Delay_MultiplePhases_DoesNotAccumulateMultipleTimes 缺失"
fi
echo ""

# 5. 检查修复的代码
echo "5. 修复验证："
if grep -q "phase <= _lastProcessedPhase" ./Runtime/Core/Async/TGTaskScheduler.cs; then
    echo "   ✅ TGTaskScheduler phase-aware frameCount 修复存在"
else
    echo "   ❌ TGTaskScheduler phase-aware frameCount 修复缺失"
fi

if grep -q "protected virtual void FixedUpdate" ./Runtime/Unity/TryGetMonoEntry.cs; then
    echo "   ✅ TryGetMonoEntry FixedUpdate 存在"
else
    echo "   ❌ TryGetMonoEntry FixedUpdate 缺失"
fi

if grep -q "EndOfFrameCoroutine" ./Runtime/Unity/TryGetMonoEntry.cs; then
    echo "   ✅ TryGetMonoEntry EndOfFrame 协程存在"
else
    echo "   ❌ TryGetMonoEntry EndOfFrame 协程缺失"
fi
echo ""

# 6. Unity 测试运行指南
echo "=========================================="
echo "Unity 测试运行指南"
echo "=========================================="
echo ""
echo "请在 Unity Editor 中执行以下步骤："
echo ""
echo "步骤 1：打开 Test Runner"
echo "   - Window → General → Test Runner"
echo "   - 或按快捷键 Ctrl+Alt+T (Windows) / Cmd+Option+T (Mac)"
echo ""
echo "步骤 2：运行 EditMode 测试"
echo "   - 切换到 EditMode 标签"
echo "   - 点击 'Run All' 按钮"
echo "   - 等待所有测试完成"
echo ""
echo "步骤 3：验证测试结果"
echo "   - 确认所有测试通过（绿色勾号）"
echo "   - 特别关注新增的 2 个测试："
echo "     * WaitForFrames_MultiplePhases_DoesNotCountMultipleTimes"
echo "     * Delay_MultiplePhases_DoesNotAccumulateMultipleTimes"
echo "   - 如果有失败，查看错误信息"
echo ""
echo "步骤 4：运行时验证（可选）"
echo "   - 创建新场景"
echo "   - 添加 GameObject"
echo "   - 添加测试脚本（见下方）"
echo "   - 运行场景，查看 Console 输出"
echo ""
echo "=========================================="
echo "运行时验证测试脚本"
echo "=========================================="
echo ""
cat << 'EOF'
using UnityEngine;
using TryGet;
using TryGet.Async;
using TryGet.Unity;

public class RuntimeVerificationEntry : TryGetMonoEntry
{
    protected override void Setup(IModuleSystem host)
    {
        // ITGTaskScheduler 已由 GameLauncher.CreateHost 默认注册。
    }

    private async void Start()
    {
        var scheduler = Host.Get<ITGTaskScheduler>();

        Debug.Log("=== 开始运行时验证 ===");

        // 测试 1：WaitForFrames
        Debug.Log("测试 1：WaitForFrames(1)");
        var startFrame = Time.frameCount;
        await scheduler.WaitForFrames(1);
        var endFrame = Time.frameCount;
        var frameDiff = endFrame - startFrame;
        Debug.Log($"  结果：等待了 {frameDiff} 帧");
        Debug.Log($"  预期：1 帧");
        Debug.Log($"  状态：{(frameDiff == 1 ? "✅ 通过" : "❌ 失败")}");

        // 测试 2：Delay
        Debug.Log("测试 2：Delay(1.0f)");
        var startTime = Time.time;
        await scheduler.Delay(1.0f);
        var endTime = Time.time;
        var timeDiff = endTime - startTime;
        Debug.Log($"  结果：等待了 {timeDiff:F2} 秒");
        Debug.Log($"  预期：~1.00 秒");
        Debug.Log($"  状态：{(Mathf.Abs(timeDiff - 1.0f) < 0.1f ? "✅ 通过" : "❌ 失败")}");

        Debug.Log("=== 运行时验证完成 ===");
    }
}
EOF
echo ""
echo "=========================================="
echo "验证完成后"
echo "=========================================="
echo ""
echo "如果所有测试通过："
echo "   1. 更新 CHANGELOG.md"
echo "   2. 提交代码"
echo "   3. 准备发布 V2.0"
echo ""
echo "如果有测试失败："
echo "   1. 查看失败原因"
echo "   2. 修复问题"
echo "   3. 重新运行测试"
echo ""
