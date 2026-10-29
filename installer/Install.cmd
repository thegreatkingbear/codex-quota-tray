@echo off
setlocal
echo Codex Quota Tray - Install or Update
echo.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
if errorlevel 1 echo Installation did not complete. See the message above.
echo.
pause
