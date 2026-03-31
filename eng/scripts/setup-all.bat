@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM SeamQ Full Environment Setup for Windows
REM Installs all dependencies from scratch (no winget required)
REM Run as Administrator for system-wide installs
REM ============================================================

echo ============================================================
echo  SeamQ Environment Setup
echo ============================================================
echo.

set "SETUP_DIR=%~dp0"
set "TEMP_DIR=%TEMP%\seamq-setup"
if not exist "%TEMP_DIR%" mkdir "%TEMP_DIR%"

REM Check for admin privileges
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [WARN] Not running as Administrator. Some installs may fail.
    echo        Right-click this script and select "Run as administrator".
    echo.
    pause
)

echo [1/6] Installing .NET SDK 8.0 ...
call "%SETUP_DIR%install-dotnet.bat"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] .NET SDK installation failed
    goto :error
)

echo.
echo [2/6] Installing Node.js LTS ...
call "%SETUP_DIR%install-node.bat"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] Node.js installation failed
    goto :error
)

echo.
echo [3/6] Installing Java JDK 21 (Eclipse Temurin) ...
call "%SETUP_DIR%install-java.bat"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] Java installation failed
    goto :error
)

echo.
echo [4/6] Installing Graphviz ...
call "%SETUP_DIR%install-graphviz.bat"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] Graphviz installation failed
    goto :error
)

echo.
echo [5/6] Installing PlantUML ...
call "%SETUP_DIR%install-plantuml.bat"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] PlantUML installation failed
    goto :error
)

echo.
echo [6/6] Installing SeamQ CLI ...
call "%SETUP_DIR%install-seamq.bat"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] SeamQ installation failed
    goto :error
)

echo.
echo ============================================================
echo  Setup Complete!
echo ============================================================
echo.
call "%SETUP_DIR%verify-setup.bat"
goto :end

:error
echo.
echo [ERROR] Setup did not complete successfully.
echo         Review the output above and re-run failed steps individually.
echo.

:end
endlocal
pause
