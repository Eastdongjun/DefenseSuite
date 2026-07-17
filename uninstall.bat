@echo off
echo Uninstalling DefenseSuite...
net session >nul 2>&1 || (echo Must run as Administrator! & pause & exit /b 1)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& { Import-Module 'C:\Program Files\DefenseSuite\DefenseSuite.psm1' -Force; Uninstall-DefenseSuite }"
pause
