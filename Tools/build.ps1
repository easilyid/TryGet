#!/usr/bin/env pwsh
# 重建 TryGet Source Generator 并把 dll 同步到 Unity。
# 用法：改了 Tools/MyTryGetFramework.SourceGenerator/src 下的生成器源码后，跑这个脚本。
#   pwsh Tools/build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host '[1/2] 运行生成器单元测试 (Debug)...' -ForegroundColor Cyan
dotnet test "$root/MyTryGetFramework.SourceGenerator.sln" -c Debug --nologo
if ($LASTEXITCODE -ne 0) { Write-Error '测试失败，已中止；Unity dll 未更新。'; exit 1 }

Write-Host '[2/2] Release 构建并同步 dll 到 Unity (仅 Release 触发同步)...' -ForegroundColor Cyan
dotnet build "$root/MyTryGetFramework.SourceGenerator/MyTryGetFramework.SourceGenerator.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { Write-Error 'Release 构建失败。'; exit 1 }

Write-Host ''
Write-Host '完成：dll 已同步到 MyTryGetFramework/Assets/MyTryGetFramework/Runtime/Core/Generators/。' -ForegroundColor Green
Write-Host '请回 Unity 让其重新 import，并把更新后的 dll 一起 git commit。' -ForegroundColor Yellow
