@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Install Graphviz on Windows
REM Downloads the official MSI installer from graphviz.org
REM ============================================================

echo [graphviz] Checking for existing Graphviz ...
where dot >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=5" %%v in ('dot -V 2^>^&1') do set "GV_VER=%%v"
    echo [graphviz] Found Graphviz !GV_VER!
    echo [graphviz] Skipping install
    exit /b 0
)

set "GV_VERSION=12.2.1"
set "GV_INSTALLER=%TEMP%\seamq-setup\graphviz-installer.exe"
set "GV_URL=https://gitlab.com/api/v4/projects/4207231/packages/generic/graphviz-releases/%GV_VERSION%/windows_10_cmake_Release_graphviz-install-%GV_VERSION%-win64.exe"

echo [graphviz] Downloading Graphviz %GV_VERSION% ...
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%GV_URL%' -OutFile '%GV_INSTALLER%'" 2>nul
if not exist "%GV_INSTALLER%" (
    echo [graphviz] Failed to download Graphviz
    echo [graphviz] Please install manually from: https://graphviz.org/download/
    exit /b 1
)

echo [graphviz] Installing Graphviz %GV_VERSION% ...
"%GV_INSTALLER%" /S
if %ERRORLEVEL% neq 0 (
    echo [graphviz] Silent install failed, launching interactive installer ...
    "%GV_INSTALLER%"
)

REM Add to PATH if not already there
set "GV_DIR=%ProgramFiles%\Graphviz\bin"
if not exist "%GV_DIR%" set "GV_DIR=%ProgramFiles(x86)%\Graphviz\bin"
if exist "%GV_DIR%" (
    echo %PATH% | findstr /i "graphviz" >nul
    if !ERRORLEVEL! neq 0 (
        echo [graphviz] Adding Graphviz to system PATH ...
        powershell -NoProfile -Command "[Environment]::SetEnvironmentVariable('Path', [Environment]::GetEnvironmentVariable('Path','Machine') + ';%GV_DIR%', 'Machine')" 2>nul
        set "PATH=%GV_DIR%;%PATH%"
    )
)

echo [graphviz] Verifying installation ...
dot -V 2>&1
if %ERRORLEVEL% neq 0 (
    echo [graphviz] Installation may require a restart of your terminal
    exit /b 1
)

echo [graphviz] Graphviz installed successfully
exit /b 0
