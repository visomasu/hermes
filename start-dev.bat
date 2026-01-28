@echo off
REM Hermes Development Launch Script (Batch Wrapper)
REM This batch file allows double-click launching of start-dev.ps1

powershell.exe -ExecutionPolicy Bypass -File "%~dp0start-dev.ps1" %*
