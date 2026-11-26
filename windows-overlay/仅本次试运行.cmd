@echo off
cd /d "%~dp0"
if not exist "%~dp0build\RoxyAntigravityBoot.exe" (
    echo 首次运行，正在使用 Windows 自带 .NET 编译器快速编译...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
    if errorlevel 1 (
        pause
        exit /b 1
    )
)
start "" "%~dp0build\RoxyAntigravityBoot.exe" --settings --play-now
