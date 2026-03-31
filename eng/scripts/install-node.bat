@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Install Node.js LTS on Windows
REM Downloads the official MSI installer
REM ============================================================

echo [node] Checking for existing Node.js ...
where node >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('node --version 2^>nul') do set "NODE_VER=%%v"
    echo [node] Found Node.js !NODE_VER!
    echo [node] Skipping install
    exit /b 0
)

set "NODE_VERSION=22.16.0"
set "NODE_INSTALLER=%TEMP%\seamq-setup\node-installer.msi"
set "NODE_URL=https://nodejs.org/dist/v%NODE_VERSION%/node-v%NODE_VERSION%-x64.msi"

echo [node] Downloading Node.js v%NODE_VERSION% ...
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%NODE_URL%' -OutFile '%NODE_INSTALLER%'" 2>nul
if not exist "%NODE_INSTALLER%" (
    echo [node] Failed to download Node.js
    echo [node] Please install manually from: https://nodejs.org/
    exit /b 1
)

echo [node] Installing Node.js v%NODE_VERSION% (this may take a minute) ...
msiexec /i "%NODE_INSTALLER%" /quiet /norestart
if %ERRORLEVEL% neq 0 (
    echo [node] Silent install failed, launching interactive installer ...
    msiexec /i "%NODE_INSTALLER%"
)

REM Refresh PATH
set "PATH=%ProgramFiles%\nodejs;%PATH%"

echo [node] Verifying installation ...
node --version 2>nul
if %ERRORLEVEL% neq 0 (
    echo [node] Installation may require a restart of your terminal
    exit /b 1
)

echo [node] Node.js installed successfully
exit /b 0
