@echo off
setlocal
cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
    echo [Error] Virtual environment not found.
    pause
    exit /b 1
)

echo Building DiscordReactionBot.exe...
".venv\Scripts\python.exe" -m PyInstaller --noconfirm --clean --onefile --console --name DiscordReactionBot --distpath dist --workpath build\bot bot.py
if errorlevel 1 goto :failed

echo.
echo Build complete:
echo   dist\DiscordReactionBot.exe
exit /b 0

:failed
echo.
echo [Error] Bot EXE build failed.
pause
exit /b 1
