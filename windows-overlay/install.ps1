# 编译 → 装到非系统盘（剩余空间最大的那块，比如 D:\AIBootAnimations\AntigravityBootAnimation）→ 设为开机自启 → 弹出设置窗口
# 只有 C 盘、或别的盘写不进去时才装到 %LOCALAPPDATA%\Programs。想自己指定：-Dir E:\某目录 或先设 $env:AGBA_DIR
# 不会修改 Antigravity 的任何文件
param([string]$Dir)
$ErrorActionPreference = 'Stop'
$name = 'RoxyAntigravityBoot'
function Test-Writable([string]$p) {
  try { New-Item -ItemType Directory -Force -Path $p | Out-Null; $f = Join-Path $p ('.w' + [guid]::NewGuid().ToString('N')); [IO.File]::WriteAllText($f, ''); Remove-Item $f -Force; return $true } catch { return $false }
}
if (-not $Dir) { $Dir = $env:AGBA_DIR }
if (-not $Dir) {
  $disks = @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' -ErrorAction SilentlyContinue | Where-Object { $_.DeviceID -ne $env:SystemDrive -and $_.FreeSpace -gt 1GB } | Sort-Object FreeSpace -Descending)
  foreach ($d in $disks) { $p = "$($d.DeviceID)\AIBootAnimations"; if (Test-Writable $p) { $Dir = $p; break } }
  if (-not $Dir) { $Dir = "$env:LOCALAPPDATA\Programs" }
}
if (-not (Test-Writable $Dir)) { throw "装不进 $Dir（没有写入权限）" }
$dest = Join-Path $Dir $name
# 只删我们自己的东西：文件夹名就是程序名才整个删；别的文件夹（用户自己挑的、里面可能有别的东西）只删我们放进去的文件，删完空了再删文件夹
function Remove-OurInstall([string]$d) {
  if (-not $d -or -not (Test-Path "$d\$name.exe")) { return }
  if ((Split-Path $d -Leaf) -eq $name) { Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue; return }
  foreach ($f in @("$name.exe", 'assets', 'uninstall.ps1')) {
    # 刚关掉的程序，Windows 可能还要一会儿才放开文件：删不掉就等一下再试
    for ($i = 0; $i -lt 10 -and (Test-Path (Join-Path $d $f)); $i++) { Remove-Item (Join-Path $d $f) -Recurse -Force -ErrorAction SilentlyContinue; if (Test-Path (Join-Path $d $f)) { Start-Sleep -Milliseconds 500 } }
  }
  if (-not (Get-ChildItem $d -Force -ErrorAction SilentlyContinue)) { Remove-Item $d -Force -ErrorAction SilentlyContinue }
}
Get-Process $name -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process $name -ErrorAction SilentlyContinue | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
# 以前装在别处（比如老版本装在 C 盘）：先删掉旧的，免得留两份
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (-not (Test-Path $run)) { New-Item -Path $run -Force | Out-Null }   # 有的电脑上这个键本来不存在
$old = (Get-ItemProperty -Path $run -Name $name -ErrorAction SilentlyContinue).$name
if ($old) {
  $oldDir = Split-Path ($old.Trim('"')) -Parent
  if ($oldDir -and ($oldDir.TrimEnd('\') -ne $dest.TrimEnd('\')) -and (Test-Path "$oldDir\$name.exe")) {
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' -Name "$oldDir\$name.exe" -ErrorAction SilentlyContinue
    Remove-OurInstall $oldDir
    Write-Host "已清掉旧版（$oldDir）"
  }
}
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
& "$PSScriptRoot\build.ps1" -Out $dest
Set-ItemProperty -Path $run -Name $name -Value "`"$dest\$name.exe`""
Start-Process "$dest\$name.exe" -ArgumentList '--settings'
Write-Host ''
Write-Host "✦ 装好了！装在 $dest"
Write-Host '   右下角托盘出现蓝紫色四芒星图标，也弹出了设置窗口。'
Write-Host '   下次打开 Antigravity 就会播放开机动画。想马上看：托盘四芒星图标右键 →「播放一次开屏动画」。'
