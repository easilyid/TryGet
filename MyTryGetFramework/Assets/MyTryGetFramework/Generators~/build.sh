#!/usr/bin/env bash
# 重建 TryGet Source Generator 并把 dll 同步到 Unity（Git Bash / macOS / Linux）。
# 用法：改了 Tools/MyTryGetFramework.SourceGenerator/src 下的生成器源码后，跑这个脚本。
#   ./Tools/build.sh
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "[1/2] 运行生成器单元测试 (Debug)..."
dotnet test "$root/MyTryGetFramework.SourceGenerator.sln" -c Debug --nologo

echo "[2/2] Release 构建并同步 dll 到 Unity (仅 Release 触发同步)..."
dotnet build "$root/MyTryGetFramework.SourceGenerator/MyTryGetFramework.SourceGenerator.csproj" -c Release --nologo

echo ""
echo "完成：dll 已同步到 MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Generators/。"
echo "请回 Unity 让其重新 import，并把更新后的 dll 一起 git commit。"
