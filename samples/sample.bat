@echo off
cd /D %~dp0..\
echo %CD%

:: 通常は、EXEと同じ場所にあるQuickLauncher.jsonが設定ファイルとなります
start "QL-A" ".\release\QuickLauncher.exe"

:: 複数起動する事が可能で、設定ファイルの場所や、アイコンも変更可能です
start "QL-B" ".\release\QuickLauncher.exe" -json ".\samples\sample.json" -icon ".\samples\sample.ico"

pause
