@echo off

chcp 65001 >nul

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0同学安装入口.ps1"

pause

