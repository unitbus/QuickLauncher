@echo off
cd /D %~dp0..\
echo %CD%

start "QL-A" ".\release\QuickLauncher.exe"
start "QL-B" ".\release\QuickLauncher.exe" -json ".\samples\sample.json" -icon ".\samples\sample.ico"
pause
