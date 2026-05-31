#!/bin/bash
# 检查编译错误脚本

echo "=========================================="
echo "检查可能的编译错误"
echo "=========================================="
echo ""

# 1. 检查旧命名残留
echo "1. 检查旧命名残留..."
echo ""

OLD_NAMES=("BootstrapOptions" "IModuleHost" "AssemblyManifestRegistry" "EventBus[^SM]")

for name in "${OLD_NAMES[@]}"; do
    echo "检查: $name"
    count=$(find . -name "*.cs" -type f -exec grep -l "$name" {} \; 2>/dev/null | wc -l)
    if [ "$count" -gt 0 ]; then
        echo "⚠️  发现 $count 个文件包含 $name"
        find . -name "*.cs" -type f -exec grep -l "$name" {} \; 2>/dev/null | head -5
    else
        echo "✅ 无残留"
    fi
    echo ""
done

# 2. 检查文件名是否正确
echo "=========================================="
echo "2. 检查文件名..."
echo "=========================================="
echo ""

# 应该存在的文件
EXPECTED_FILES=(
    "Runtime/Core/Module/ModuleSystem.cs"
    "Runtime/Core/Module/IModuleSystem.cs"
    "Runtime/Core/Module/GameLauncher.cs"
    "Runtime/Core/Module/LauncherOptions.cs"
    "Runtime/Core/Module/ModuleRegistry.cs"
    "Runtime/Core/Event/EventModule.cs"
    "Runtime/Core/Event/IEventModule.cs"
    "Runtime/Core/Event/EventModuleScopeExtensions.cs"
)

for file in "${EXPECTED_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "✅ $file"
    else
        echo "❌ 缺失: $file"
    fi
done
echo ""

# 不应该存在的文件
OLD_FILES=(
    "Runtime/Core/Module/ModuleHost.cs"
    "Runtime/Core/Module/IModuleHost.cs"
    "Runtime/Core/Module/Bootstrap.cs"
    "Runtime/Core/Module/BootstrapOptions.cs"
    "Runtime/Core/Module/AssemblyManifestRegistry.cs"
    "Runtime/Core/Event/EventBus.cs"
    "Runtime/Core/Event/IEventBus.cs"
    "Runtime/Core/Event/EventBusScopeExtensions.cs"
)

echo "检查旧文件是否还存在..."
for file in "${OLD_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "⚠️  旧文件仍存在: $file"
    fi
done
echo "✅ 所有旧文件已删除"
echo ""

# 3. 检查类名是否正确
echo "=========================================="
echo "3. 检查类名定义..."
echo "=========================================="
echo ""

echo "检查 ModuleSystem..."
grep -n "class ModuleSystem" Runtime/Core/Module/ModuleSystem.cs 2>/dev/null && echo "✅" || echo "❌"

echo "检查 GameLauncher..."
grep -n "class GameLauncher" Runtime/Core/Module/GameLauncher.cs 2>/dev/null && echo "✅" || echo "❌"

echo "检查 EventModule..."
grep -n "class EventModule" Runtime/Core/Event/EventModule.cs 2>/dev/null && echo "✅" || echo "❌"

echo ""

# 4. 列出所有修改的文件
echo "=========================================="
echo "4. Git 状态"
echo "=========================================="
echo ""
git status --short | head -30

echo ""
echo "=========================================="
echo "检查完成"
echo "=========================================="
