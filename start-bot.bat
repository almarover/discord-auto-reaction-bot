@echo off
cd /d "%~dp0"

if exist "Discord Auto Reaction Bot.exe" (
    start "" "Discord Auto Reaction Bot.exe" %*
    exit /b 0
)

echo [Error] Discord Auto Reaction Bot.exe was not found.
echo Run build-launcher.bat first.
pause
exit /b 1
