# 彻底卸载：停掉程序、删开机自启、删程序、删设置和日志。Antigravity 本来就没被改过。
param([switch]$Quiet)
$name = 'RoxyAntigravityBoot'
Get-Process $name -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
# 程序装在哪：看开机自启登记的路径；没有就看本脚本所在目录；再没有就是老默认位置（C 盘）
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$reg = (Get-ItemProperty -Path $run -Name $name -ErrorAction SilentlyContinue).$name
$dest = $null
if ($reg) { $dest = Split-Path ($reg.Trim('"')) -Parent }
if (-not $dest -or -not (Test-Path "$dest\$name.exe")) { if (Test-Path "$PSScriptRoot\$name.exe") { $dest = $PSScriptRoot } else { $dest = "$env:LOCALAPPDATA\Programs\$name" } }
Remove-ItemProperty -Path $run -Name $name -ErrorAction SilentlyContinue
Remove-Item "$env:APPDATA\$name" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\$name" -Recurse -Force -ErrorAction SilentlyContinue
# 删掉启动时写的「用高性能显卡」偏好
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' -Name "$dest\$name.exe" -ErrorAction SilentlyContinue
# 卸载脚本本身可能就在程序目录里，延后删；上一级 AIBootAnimations 空了也顺手删掉（里面还有别的动画就不动）
$parent = Split-Path $dest -Parent
if ((Split-Path $dest -Leaf) -eq $name) { $cmd = "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dest`"" }
else { $cmd = "/c timeout /t 2 /nobreak >nul & del /f /q `"$dest\$name.exe`" `"$dest\uninstall.ps1`" & rmdir /s /q `"$dest\assets`" & rmdir `"$dest`" 2>nul" }   # 用户自己挑的文件夹：只删我们的文件
if ((Split-Path $parent -Leaf) -eq 'AIBootAnimations') { $cmd += " & rmdir `"$parent`" 2>nul" }
Start-Process cmd.exe -ArgumentList $cmd -WindowStyle Hidden
if (-not $Quiet) { Write-Host "已卸载干净（$dest）。" }
