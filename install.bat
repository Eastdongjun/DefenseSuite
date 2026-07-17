@echo off
REM ============================================================
REM DefenseSuite v1.0 — One-click installer
REM Run as Administrator
REM ============================================================

echo.
echo ========================================
echo  DefenseSuite v1.0 — Installation
echo ========================================
echo.

REM Check admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    echo Right-click this file ^> Run as Administrator
    pause
    exit /b 1
)

REM Run the main install
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& { Import-Module '%~dp0DefenseSuite.psm1' -Force; Install-DefenseSuite -InstallDir 'C:\Program Files\DefenseSuite' -WhitelistIPs @('113.132.220.221','220.195.83.129') }"

echo.
echo Done. Check status: run status.bat
pause
