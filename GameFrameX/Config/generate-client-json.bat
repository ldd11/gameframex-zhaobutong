@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0generate-client-json.ps1" %*
if errorlevel 1 pause
