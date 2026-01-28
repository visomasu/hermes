@echo off
REM Hermes Production Launch Script (Batch Wrapper)
REM This batch file allows double-click launching of start-prod.ps1

powershell.exe -ExecutionPolicy Bypass -File "%~dp0start-prod.ps1" %*
