@echo off
setlocal
title FileOrgy Elevated Autostart Setup
echo ==============================================================
echo FileOrgy Elevated Autostart Setup (Task Scheduler /RL HIGHEST)
echo ==============================================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [UAC] Requesting Administrator privileges...
    powershell -NoProfile -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [OK] Running with Administrator privileges.
echo [OK] Executing setup script...
echo.
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "C:\Users\swalih\FileOrgy\tools\setup_elevated_autostart.ps1"
echo.
echo ==============================================================
echo Setup script execution finished.
echo ==============================================================
pause
