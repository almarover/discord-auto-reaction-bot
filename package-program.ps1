$ErrorActionPreference = 'Stop'

$developmentRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspaceRoot = Split-Path -Parent $developmentRoot
$programRoot = Join-Path $workspaceRoot 'Discord Auto Reaction Bot - Program'
$managerSource = Join-Path $developmentRoot 'manager-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64'
$managerTarget = Join-Path $programRoot 'dist\winui-manager'
$botSource = Join-Path $developmentRoot 'dist\DiscordReactionBot.exe'
$botTarget = Join-Path $programRoot 'dist\DiscordReactionBot.exe'

if (-not (Test-Path -LiteralPath (Join-Path $managerSource 'DiscordReactionManager.WinUI.exe'))) {
    throw "WinUI Release 输出不存在：$managerSource"
}
if (-not (Test-Path -LiteralPath $botSource)) {
    throw "Bot 便携版输出不存在：$botSource"
}

New-Item -ItemType Directory -Path $programRoot -Force | Out-Null
New-Item -ItemType Directory -Path $managerTarget -Force | Out-Null
Copy-Item -Path (Join-Path $managerSource '*') -Destination $managerTarget -Recurse -Force
Copy-Item -LiteralPath $botSource -Destination $botTarget -Force
Copy-Item -LiteralPath (Join-Path $developmentRoot '.env.example') -Destination (Join-Path $programRoot '.env.example') -Force
Copy-Item -LiteralPath (Join-Path $developmentRoot 'PORTABLE_README.txt') -Destination (Join-Path $programRoot '使用说明.txt') -Force
New-Item -ItemType File -Path (Join-Path $programRoot '.discord-auto-reaction-app') -Force | Out-Null

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '找不到 Windows C# 编译器。'
}

$launcherSource = Join-Path $developmentRoot 'launcher-src\Launcher.cs'
$iconPath = Join-Path $developmentRoot 'manager-winui\Assets\AppIcon.ico'
$launcherTarget = Join-Path $programRoot 'Discord Auto Reaction Bot.exe'
& $compiler /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll "/win32icon:$iconPath" "/out:$launcherTarget" $launcherSource
if ($LASTEXITCODE -ne 0) {
    throw "启动器编译失败，退出代码：$LASTEXITCODE"
}

Write-Host "便携程序已创建：$programRoot"
