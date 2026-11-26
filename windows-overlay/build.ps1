# 用 Windows 自带的 .NET Framework 编译器把 src\RoxyAntigravityBoot.cs 编成 exe，不需要装任何 SDK
# 基于 wynneceles/ai-boot-animations (GPL-3.0) 适配洛琪希主题
# 用法：powershell -ExecutionPolicy Bypass -File build.ps1 [-Out 输出目录]
param([string]$Out = "$PSScriptRoot\build")
$ErrorActionPreference = 'Stop'
$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path "$fw\csc.exe")) { $fw = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319" }
$csc = "$fw\csc.exe"
if (-not (Test-Path $csc)) { throw "找不到系统自带的 C# 编译器（$csc）。需要 Windows 10/11。" }
$wpf = "$fw\WPF"
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$refs = @("$wpf\PresentationFramework.dll", "$wpf\PresentationCore.dll", "$wpf\WindowsBase.dll",
          "$fw\System.Xaml.dll", "$fw\System.Windows.Forms.dll", "$fw\System.Drawing.dll",
          "$fw\System.Web.Extensions.dll", "$fw\System.Core.dll", "$fw\System.Management.dll") | ForEach-Object { "/r:$_" }
$cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu', "/win32manifest:$PSScriptRoot\src\app.manifest", "/out:$Out\RoxyAntigravityBoot.exe") + $refs + @("$PSScriptRoot\src\RoxyAntigravityBoot.cs")
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败" }
New-Item -ItemType Directory -Force -Path "$Out\assets" | Out-Null
Copy-Item "$PSScriptRoot\assets\*" "$Out\assets\" -Force
Copy-Item "$PSScriptRoot\uninstall.ps1" "$Out\" -Force
Copy-Item "$PSScriptRoot\LICENSE" "$Out\" -Force
Write-Host "编译完成：$Out\RoxyAntigravityBoot.exe"
