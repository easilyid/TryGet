#!/bin/bash
# 命名重构执行脚本 — 2026/05/31
# 基于 TEngine 专业标准重构命名

set -e

echo "=========================================="
echo "TryGet 框架命名重构"
echo "=========================================="
echo ""

# 统计当前命名出现次数
echo "1. 统计当前命名出现次数..."
echo "   ModuleHost: $(grep -r "ModuleHost" --include="*.cs" | wc -l) 次"
echo "   Bootstrap: $(grep -r "Bootstrap" --include="*.cs" | wc -l) 次"
echo "   EventBus: $(grep -r "EventBus" --include="*.cs" | wc -l) 次"
echo "   AssemblyManifestRegistry: $(grep -r "AssemblyManifestRegistry" --include="*.cs" | wc -l) 次"
echo ""

# Phase 1: ModuleHost → ModuleSystem
echo "=========================================="
echo "Phase 1: ModuleHost → ModuleSystem"
echo "=========================================="
echo ""

echo "正在重命名 ModuleHost → ModuleSystem..."
find . -name "*.cs" -type f -exec sed -i 's/ModuleHost/ModuleSystem/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名 IModuleHost → IModuleSystem..."
find . -name "*.cs" -type f -exec sed -i 's/IModuleSystem/IModuleSystem/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名文件..."
if [ -f "Runtime/Core/Module/ModuleHost.cs" ]; then
    git mv "Runtime/Core/Module/ModuleHost.cs" "Runtime/Core/Module/ModuleSystem.cs"
    echo "✅ ModuleHost.cs → ModuleSystem.cs"
fi

if [ -f "Runtime/Core/Module/IModuleHost.cs" ]; then
    git mv "Runtime/Core/Module/IModuleHost.cs" "Runtime/Core/Module/IModuleSystem.cs"
    echo "✅ IModuleHost.cs → IModuleSystem.cs"
fi

# 重命名测试文件
for file in Tests/EditMode/ModuleHost*.cs; do
    if [ -f "$file" ]; then
        newfile=$(echo "$file" | sed 's/ModuleHost/ModuleSystem/g')
        git mv "$file" "$newfile"
        echo "✅ $(basename $file) → $(basename $newfile)"
    fi
done
echo ""

# Phase 2: Bootstrap → GameLauncher
echo "=========================================="
echo "Phase 2: Bootstrap → GameLauncher"
echo "=========================================="
echo ""

echo "正在重命名 Bootstrap → GameLauncher..."
find . -name "*.cs" -type f -exec sed -i 's/Bootstrap/GameLauncher/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名 BootstrapOptions → LauncherOptions..."
find . -name "*.cs" -type f -exec sed -i 's/LauncherOptions/LauncherOptions/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名文件..."
if [ -f "Runtime/Core/Module/Bootstrap.cs" ]; then
    git mv "Runtime/Core/Module/Bootstrap.cs" "Runtime/Core/Module/GameLauncher.cs"
    echo "✅ Bootstrap.cs → GameLauncher.cs"
fi

if [ -f "Runtime/Core/Module/BootstrapOptions.cs" ]; then
    git mv "Runtime/Core/Module/BootstrapOptions.cs" "Runtime/Core/Module/LauncherOptions.cs"
    echo "✅ BootstrapOptions.cs → LauncherOptions.cs"
fi
echo ""

# Phase 3: AssemblyManifestRegistry → ModuleRegistry
echo "=========================================="
echo "Phase 3: AssemblyManifestRegistry → ModuleRegistry"
echo "=========================================="
echo ""

echo "正在重命名 AssemblyManifestRegistry → ModuleRegistry..."
find . -name "*.cs" -type f -exec sed -i 's/AssemblyManifestRegistry/ModuleRegistry/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名文件..."
if [ -f "Runtime/Core/Module/AssemblyManifestRegistry.cs" ]; then
    git mv "Runtime/Core/Module/AssemblyManifestRegistry.cs" "Runtime/Core/Module/ModuleRegistry.cs"
    echo "✅ AssemblyManifestRegistry.cs → ModuleRegistry.cs"
fi
echo ""

# Phase 4: EventBus → EventModule
echo "=========================================="
echo "Phase 4: EventBus → EventModule"
echo "=========================================="
echo ""

echo "正在重命名 EventBus → EventModule..."
find . -name "*.cs" -type f -exec sed -i 's/EventBus/EventModule/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名 IEventBus → IEventModule..."
find . -name "*.cs" -type f -exec sed -i 's/IEventModule/IEventModule/g' {} \;
echo "✅ 完成"
echo ""

echo "正在重命名文件..."
if [ -f "Runtime/Core/Event/EventBus.cs" ]; then
    git mv "Runtime/Core/Event/EventBus.cs" "Runtime/Core/Event/EventModule.cs"
    echo "✅ EventBus.cs → EventModule.cs"
fi

if [ -f "Runtime/Core/Event/IEventBus.cs" ]; then
    git mv "Runtime/Core/Event/IEventBus.cs" "Runtime/Core/Event/IEventModule.cs"
    echo "✅ IEventBus.cs → IEventModule.cs"
fi

# 重命名测试文件
for file in Tests/EditMode/Event*.cs; do
    if [ -f "$file" ]; then
        newfile=$(echo "$file" | sed 's/EventBus/EventModule/g')
        if [ "$file" != "$newfile" ]; then
            git mv "$file" "$newfile"
            echo "✅ $(basename $file) → $(basename $newfile)"
        fi
    fi
done
echo ""

# 验证
echo "=========================================="
echo "验证重构结果"
echo "=========================================="
echo ""

echo "检查旧命名是否还存在..."
OLD_NAMES=("ModuleHost" "Bootstrap" "AssemblyManifestRegistry" "EventBus")
for name in "${OLD_NAMES[@]}"; do
    count=$(grep -r "$name" --include="*.cs" 2>/dev/null | grep -v "// " | wc -l)
    if [ "$count" -gt 0 ]; then
        echo "⚠️  $name 仍然存在 $count 次"
    else
        echo "✅ $name 已完全替换"
    fi
done
echo ""

echo "=========================================="
echo "重构完成！"
echo "=========================================="
echo ""
echo "下一步："
echo "1. 在 Unity Editor 中检查编译错误"
echo "2. 运行测试套件（Test Runner → Run All）"
echo "3. 更新文档（ARCHITECTURE.md, README.md, CHANGELOG.md）"
echo "4. 提交代码"
echo ""
