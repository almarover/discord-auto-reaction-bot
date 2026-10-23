@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [Error] Windows C# compiler was not found.
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll /win32icon:"manager-winui\Assets\AppIcon.ico" /out:"Discord Auto Reaction Bot.exe" "launcher-src\Launcher.cs"
if errorlevel 1 exit /b 1

echo Build complete:
echo   Discord Auto Reaction Bot.exe
exit /b 0
