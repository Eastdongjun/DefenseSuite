@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& { Import-Module 'C:\Program Files\DefenseSuite\DefenseSuite.psm1' -Force; Get-DefenseStatus }"
pause
