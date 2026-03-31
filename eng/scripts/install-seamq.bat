@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Install or update SeamQ CLI tool from NuGet
REM ============================================================

echo [seamq] Checking for existing SeamQ ...
where seamq >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('seamq --version 2^>nul') do set "SQ_VER=%%v"
    echo [seamq] Found SeamQ !SQ_VER!
    echo [seamq] Updating to latest ...
    dotnet tool update --global SeamQ 2>nul
    if !ERRORLEVEL! equ 0 (
        for /f "tokens=*" %%v in ('seamq --version 2^>nul') do echo [seamq] Now at %%v
        exit /b 0
    )
)

echo [seamq] Installing SeamQ from NuGet ...
dotnet tool install --global SeamQ
if %ERRORLEVEL% neq 0 (
    echo [seamq] NuGet install failed
    echo [seamq] Ensure .NET SDK 8.0+ is installed and on PATH
    exit /b 1
)

echo [seamq] Verifying installation ...
seamq --version 2>nul
if %ERRORLEVEL% neq 0 (
    echo [seamq] Installation may require a restart of your terminal
    exit /b 1
)

echo [seamq] SeamQ installed successfully
exit /b 0
