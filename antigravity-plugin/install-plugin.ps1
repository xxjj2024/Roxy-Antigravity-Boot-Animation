# 一键将「洛琪希开场」MCP App 插件安装到 Antigravity 本地插件目录
# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File install-plugin.ps1
$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:USERPROFILE ".gemini\config\plugins\roxy-intro"
New-Item -ItemType Directory -Force -Path "$dest\assets" | Out-Null

Copy-Item -Force "$PSScriptRoot\plugin.json" "$dest\"
Copy-Item -Force "$PSScriptRoot\mcp_config.json" "$dest\"
Copy-Item -Force "$PSScriptRoot\server.py" "$dest\"
Copy-Item -Force "$PSScriptRoot\intro.html" "$dest\"
Copy-Item -Force "$PSScriptRoot\assets\logo.svg" "$dest\assets\"

Write-Host ""
Write-Host "✦ 已安装 Antigravity 内置插件至：$dest"
Write-Host "  重启或刷新 Antigravity 后，即可在左侧 Plugins 栏看到「洛琪希开场」。"
