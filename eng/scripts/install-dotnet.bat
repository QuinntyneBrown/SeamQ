@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Install .NET SDK 8.0 on Windows
REM Downloads the official installer from Microsoft
REM ============================================================

echo [dotnet] Checking for existing .NET SDK ...
where dotnet >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('dotnet --version 2^>nul') do set "DOTNET_VER=%%v"
    echo [dotnet] Found .NET SDK !DOTNET_VER!
    echo !DOTNET_VER! | findstr /b "8\. 9\. 10\." >nul
    if !ERRORLEVEL! equ 0 (
        echo [dotnet] Version is 8.0+ -- skipping install
        exit /b 0
    )
    echo [dotnet] Version is below 8.0 -- installing newer SDK
)

set "DOTNET_INSTALLER=%TEMP%\seamq-setup\dotnet-sdk-installer.exe"
set "DOTNET_URL=https://dot.net/v1/dotnet-install.ps1"
set "DOTNET_SCRIPT=%TEMP%\seamq-setup\dotnet-install.ps1"

echo [dotnet] Downloading .NET SDK 8.0 install script ...
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%DOTNET_URL%' -OutFile '%DOTNET_SCRIPT%'" 2>nul
if not exist "%DOTNET_SCRIPT%" (
    echo [dotnet] Failed to download install script
    echo [dotnet] Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

echo [dotnet] Running .NET SDK 8.0 installer ...
powershell -NoProfile -ExecutionPolicy Bypass -File "%DOTNET_SCRIPT%" -Channel 8.0 -InstallDir "%ProgramFiles%\dotnet"
if %ERRORLEVEL% neq 0 (
    echo [dotnet] Script install failed, trying direct MSI download ...
    set "MSI_URL=https://download.visualstudio.microsoft.com/download/pr/dotnet-sdk-8.0-latest-win-x64.exe"
    powershell -NoProfile -Command "Invoke-WebRequest -Uri 'https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.exe' -OutFile '%DOTNET_INSTALLER%'" 2>nul
    if exist "%DOTNET_INSTALLER%" (
        echo [dotnet] Running SDK installer (this may take a few minutes) ...
        "%DOTNET_INSTALLER%" /install /quiet /norestart
    ) else (
        echo [dotnet] Download failed. Install manually from https://dotnet.microsoft.com/download/dotnet/8.0
        exit /b 1
    )
)

REM Refresh PATH
set "PATH=%ProgramFiles%\dotnet;%PATH%"

echo [dotnet] Verifying installation ...
dotnet --version 2>nul
if %ERRORLEVEL% neq 0 (
    echo [dotnet] Installation may require a restart of your terminal
    exit /b 1
)

echo [dotnet] .NET SDK installed successfully
exit /b 0
