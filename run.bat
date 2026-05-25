@echo off
setlocal enabledelayedexpansion

set "EXE_PATH=bin\Any CPU\Release\net48\teklaUDA4.8Win.exe"
set "ALT_EXE_PATH=bin\Any CPU\Debug\net48\teklaUDA4.8Win.exe"

if exist "%EXE_PATH%" (
    echo [INFO] Found existing Release build. Launching...
    start "" "%EXE_PATH%"
    exit /b
)

if exist "%ALT_EXE_PATH%" (
    echo [INFO] Found existing Debug build. Launching...
    start "" "%ALT_EXE_PATH%"
    exit /b
)

echo   App not built yet. Building teklaUDA4.8Win...

set "MSBuildPath="
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    set "MSBuildPath=%%i"
)

if not defined MSBuildPath (
    if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBuildPath=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBuildPath=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBuildPath=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )
)

if defined MSBuildPath (
    "!MSBuildPath!" teklaUDA4.8Win.csproj /t:restore,build /p:Configuration=Release /p:Platform="Any CPU"
) else (
    where dotnet >nul 2>nul
    if %errorlevel% equ 0 (
        dotnet build teklaUDA4.8Win.csproj -c Release -p:Platform="Any CPU"
    ) else (
        echo [ERROR] Could not find Visual Studio MSBuild or .NET SDK.
        pause
        exit /b
    )
)

if exist "%EXE_PATH%" (
    echo [SUCCESS] Build completed. Launching app...
    start "" "%EXE_PATH%"
    exit /b
) else (
    echo [ERROR] Build completed but the executable was not found at:
    echo "%EXE_PATH%"
    pause
)