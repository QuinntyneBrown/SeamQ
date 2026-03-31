@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Verify SeamQ environment setup
REM Checks all dependencies and reports status
REM ============================================================

echo ============================================================
echo  SeamQ Environment Verification
echo ============================================================
echo.

set "PASS=0"
set "FAIL=0"
set "WARN=0"

REM --- .NET SDK ---
where dotnet >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('dotnet --version 2^>nul') do set "DOTNET_VER=%%v"
    echo [PASS] .NET SDK          !DOTNET_VER!
    set /a PASS+=1
) else (
    echo [FAIL] .NET SDK          not found
    set /a FAIL+=1
)

REM --- Node.js ---
where node >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('node --version 2^>nul') do set "NODE_VER=%%v"
    echo [PASS] Node.js           !NODE_VER!
    set /a PASS+=1
) else (
    echo [WARN] Node.js           not found (needed for Angular workspaces)
    set /a WARN+=1
)

REM --- npm ---
where npm >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('npm --version 2^>nul') do set "NPM_VER=%%v"
    echo [PASS] npm               !NPM_VER!
    set /a PASS+=1
) else (
    echo [WARN] npm               not found
    set /a WARN+=1
)

REM --- Java ---
where java >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=3" %%v in ('java -version 2^>^&1 ^| findstr /i "version"') do set "JAVA_VER=%%~v"
    echo [PASS] Java              !JAVA_VER!
    set /a PASS+=1
) else (
    echo [WARN] Java              not found (needed for PlantUML rendering)
    set /a WARN+=1
)

REM --- Graphviz ---
where dot >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=5" %%v in ('dot -V 2^>^&1') do set "GV_VER=%%v"
    echo [PASS] Graphviz          !GV_VER!
    set /a PASS+=1
) else (
    echo [WARN] Graphviz          not found (needed for PlantUML class diagrams)
    set /a WARN+=1
)

REM --- PlantUML ---
set "PUML_FOUND=0"
if defined PLANTUML_JAR (
    if exist "%PLANTUML_JAR%" (
        echo [PASS] PlantUML          %PLANTUML_JAR%
        set /a PASS+=1
        set "PUML_FOUND=1"
    )
)
if "!PUML_FOUND!"=="0" (
    if exist "%USERPROFILE%\.plantuml\plantuml.jar" (
        echo [PASS] PlantUML          %USERPROFILE%\.plantuml\plantuml.jar
        set /a PASS+=1
        set "PUML_FOUND=1"
    )
)
if "!PUML_FOUND!"=="0" (
    echo [WARN] PlantUML          not found (needed for diagram rendering)
    set /a WARN+=1
)

REM --- Git ---
where git >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=3" %%v in ('git --version 2^>nul') do set "GIT_VER=%%v"
    echo [PASS] Git               !GIT_VER!
    set /a PASS+=1
) else (
    echo [WARN] Git               not found
    set /a WARN+=1
)

REM --- SeamQ ---
where seamq >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=*" %%v in ('seamq --version 2^>nul') do set "SQ_VER=%%v"
    echo [PASS] SeamQ             !SQ_VER!
    set /a PASS+=1
) else (
    echo [FAIL] SeamQ             not found
    set /a FAIL+=1
)

echo.
echo ============================================================
echo  Results:  %PASS% passed, %FAIL% failed, %WARN% warnings
echo ============================================================

if %FAIL% gtr 0 (
    echo.
    echo  Required dependencies are missing. Run setup-all.bat to install.
    exit /b 1
)

if %WARN% gtr 0 (
    echo.
    echo  Optional dependencies missing. SeamQ will work but some features
    echo  ^(diagram rendering^) will be limited.
)

echo.
exit /b 0
