@echo off
setlocal
cd /d "%~dp0"

set "DOTNET=%~dp0.tools\dotnet\dotnet.exe"
if not exist "%DOTNET%" (
    echo [Error] Project-local .NET SDK was not found.
    echo Expected: %DOTNET%
    exit /b 1
)

echo Building WinUI manager...
"%DOTNET%" build "manager-winui\DiscordReactionManager.WinUI.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true
if errorlevel 1 exit /b 1

set "SOURCE=manager-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64"
if not exist "%SOURCE%\DiscordReactionManager.WinUI.exe" (
    echo [Error] Release output was not found: %SOURCE%
    exit /b 1
)
if not exist "dist\winui-manager" mkdir "dist\winui-manager"
xcopy "%SOURCE%\*" "dist\winui-manager\" /E /I /Y /Q >nul
if errorlevel 1 exit /b 1

echo.
echo Build complete:
echo   dist\winui-manager\DiscordReactionManager.WinUI.exe
exit /b 0
