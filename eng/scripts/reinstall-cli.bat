@echo off
setlocal

set TOOL_NAME=seamq
set PACKAGE_ID=SeamQ
set CLI_PROJECT=src\SeamQ.Cli\SeamQ.Cli.csproj
set NUPKG_DIR=src\SeamQ.Cli\nupkg

:: Resolve repo root (two levels up from this script)
set REPO_ROOT=%~dp0..\..
pushd "%REPO_ROOT%"

echo ========================================
echo  SeamQ CLI Reinstall
echo ========================================

:: 1. Uninstall if already installed
echo.
echo [1/4] Checking for existing installation...
dotnet tool list -g | findstr /i "%TOOL_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo       Uninstalling %TOOL_NAME%...
    dotnet tool uninstall -g %PACKAGE_ID%
) else (
    echo       %TOOL_NAME% is not installed. Skipping uninstall.
)

:: 2. Build
echo.
echo [2/4] Building %CLI_PROJECT%...
dotnet build "%CLI_PROJECT%" -c Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed.
    popd
    exit /b 1
)

:: 3. Pack
echo.
echo [3/4] Packing %CLI_PROJECT%...
if exist "%NUPKG_DIR%" rmdir /s /q "%NUPKG_DIR%"
dotnet pack "%CLI_PROJECT%" -c Release -o "%NUPKG_DIR%" --no-build
if %errorlevel% neq 0 (
    echo ERROR: Pack failed.
    popd
    exit /b 1
)

:: 4. Install from local package
echo.
echo [4/4] Installing %TOOL_NAME% globally...
for %%f in ("%NUPKG_DIR%\%PACKAGE_ID%.*.nupkg") do set NUPKG=%%f
dotnet tool install -g %PACKAGE_ID% --add-source "%NUPKG_DIR%"
if %errorlevel% neq 0 (
    echo ERROR: Install failed.
    popd
    exit /b 1
)

echo.
echo ========================================
echo  Done! Run '%TOOL_NAME%' to verify.
echo ========================================

popd
endlocal
