@echo off
setlocal enabledelayedexpansion
title DefenseSuite v1.0 Setup
color 0B

:: Check admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Administrator privileges required!
    echo Right-click this file ^> Run as Administrator
    pause
    exit /b 1
)

:: Dispatch
if /I "%1"=="/status"   goto :status
if /I "%1"=="/uninstall" goto :uninstall
if /I "%1"=="/silent"    goto :silent
goto :menu

:menu
cls
echo.
echo   ========================================
echo     DefenseSuite v1.0
echo     Windows Server Attack Defense System
echo   ========================================
echo.
echo   [1] Install DefenseSuite (full protection)
echo   [2] View Status
echo   [3] Uninstall
echo   [4] Exit
echo.
choice /c 1234 /n /m "Choose [1-4]: "
if %errorLevel%==1 goto :install
if %errorLevel%==2 goto :status
if %errorLevel%==3 goto :uninstall
exit /b 0

:install
echo.
echo Installing DefenseSuite to C:\Program Files\DefenseSuite...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DefenseSuite-Setup.ps1"
goto :end

:silent
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DefenseSuite-Setup.ps1" -Silent -WhitelistIPs "%2"
goto :end

:status
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DefenseSuite-Setup.ps1" -Status
goto :end

:uninstall
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DefenseSuite-Setup.ps1" -Uninstall
goto :end

:end
pause
