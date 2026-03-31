@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Install PlantUML on Windows
REM Downloads plantuml.jar and creates a wrapper script
REM ============================================================

set "PUML_DIR=%USERPROFILE%\.plantuml"
set "PUML_JAR=%PUML_DIR%\plantuml.jar"

echo [plantuml] Checking for existing PlantUML ...
if exist "%PUML_JAR%" (
    echo [plantuml] Found PlantUML at %PUML_JAR%
    echo [plantuml] Skipping install
    exit /b 0
)

REM Also check common locations
for %%p in (
    "%ProgramFiles%\plantuml\plantuml.jar"
    "%USERPROFILE%\plantuml.jar"
    "%USERPROFILE%\.dotnet\tools\plantuml.jar"
) do (
    if exist %%p (
        echo [plantuml] Found PlantUML at %%~p
        echo [plantuml] Skipping install
        exit /b 0
    )
)

if not exist "%PUML_DIR%" mkdir "%PUML_DIR%"

set "PUML_URL=https://github.com/plantuml/plantuml/releases/download/v1.2025.2/plantuml-1.2025.2.jar"

echo [plantuml] Downloading PlantUML ...
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%PUML_URL%' -OutFile '%PUML_JAR%'" 2>nul
if not exist "%PUML_JAR%" (
    echo [plantuml] Primary download failed, trying latest release ...
    powershell -NoProfile -Command "$r = Invoke-RestMethod 'https://api.github.com/repos/plantuml/plantuml/releases/latest'; $a = $r.assets | Where-Object { $_.name -match 'plantuml-.*\.jar$' -and $_.name -notmatch 'javadoc|sources|asl' } | Select-Object -First 1; if ($a) { Invoke-WebRequest -Uri $a.browser_download_url -OutFile '%PUML_JAR%' }" 2>nul
)
if not exist "%PUML_JAR%" (
    echo [plantuml] Failed to download PlantUML
    echo [plantuml] Please download manually from: https://plantuml.com/download
    echo [plantuml] Place plantuml.jar in: %PUML_DIR%
    exit /b 1
)

REM Create a wrapper batch file on PATH
set "PUML_CMD=%PUML_DIR%\plantuml.bat"
(
    echo @echo off
    echo java -jar "%PUML_JAR%" %%*
) > "%PUML_CMD%"

REM Add to PATH if not already there
echo %PATH% | findstr /i ".plantuml" >nul
if %ERRORLEVEL% neq 0 (
    echo [plantuml] Adding PlantUML to user PATH ...
    powershell -NoProfile -Command "[Environment]::SetEnvironmentVariable('Path', [Environment]::GetEnvironmentVariable('Path','User') + ';%PUML_DIR%', 'User')" 2>nul
    set "PATH=%PUML_DIR%;%PATH%"
)

REM Set PLANTUML_JAR env var for SeamQ
echo [plantuml] Setting PLANTUML_JAR environment variable ...
powershell -NoProfile -Command "[Environment]::SetEnvironmentVariable('PLANTUML_JAR', '%PUML_JAR%', 'User')" 2>nul
set "PLANTUML_JAR=%PUML_JAR%"

echo [plantuml] Verifying installation ...
java -jar "%PUML_JAR%" -version 2>nul
if %ERRORLEVEL% neq 0 (
    echo [plantuml] PlantUML downloaded but Java is required to run it
    echo [plantuml] Make sure Java is installed and on PATH
    exit /b 1
)

echo [plantuml] PlantUML installed successfully at %PUML_JAR%
exit /b 0
