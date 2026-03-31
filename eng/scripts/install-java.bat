@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Install Eclipse Temurin JDK 21 on Windows
REM Downloads the official MSI installer from Adoptium
REM ============================================================

echo [java] Checking for existing Java ...
where java >nul 2>&1
if %ERRORLEVEL% equ 0 (
    for /f "tokens=3" %%v in ('java -version 2^>^&1 ^| findstr /i "version"') do set "JAVA_VER=%%~v"
    echo [java] Found Java !JAVA_VER!
    echo [java] Skipping install
    exit /b 0
)

set "JDK_VERSION=21.0.7"
set "JDK_BUILD=6"
set "JDK_INSTALLER=%TEMP%\seamq-setup\temurin-jdk-installer.msi"
set "JDK_URL=https://github.com/adoptium/temurin21-binaries/releases/download/jdk-%JDK_VERSION%+%JDK_BUILD%/OpenJDK21U-jdk_x64_windows_hotspot_%JDK_VERSION%_%JDK_BUILD%.msi"

echo [java] Downloading Eclipse Temurin JDK %JDK_VERSION% ...
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%JDK_URL%' -OutFile '%JDK_INSTALLER%'" 2>nul
if not exist "%JDK_INSTALLER%" (
    echo [java] Primary download failed, trying alternate URL ...
    set "JDK_URL_ALT=https://api.adoptium.net/v3/installer/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse?project=jdk"
    powershell -NoProfile -Command "Invoke-WebRequest -Uri '!JDK_URL_ALT!' -OutFile '%JDK_INSTALLER%'" 2>nul
)
if not exist "%JDK_INSTALLER%" (
    echo [java] Failed to download Java JDK
    echo [java] Please install manually from: https://adoptium.net/
    exit /b 1
)

echo [java] Installing Eclipse Temurin JDK 21 (this may take a few minutes) ...
msiexec /i "%JDK_INSTALLER%" ADDLOCAL=FeatureMain,FeatureEnvironment,FeatureJarFileRunWith,FeatureJavaHome /quiet /norestart
if %ERRORLEVEL% neq 0 (
    echo [java] Silent install failed, launching interactive installer ...
    msiexec /i "%JDK_INSTALLER%"
)

REM Refresh PATH
for /f "tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "SYS_PATH=%%b"
set "PATH=%SYS_PATH%;%PATH%"

echo [java] Verifying installation ...
java -version 2>&1
if %ERRORLEVEL% neq 0 (
    echo [java] Installation may require a restart of your terminal
    exit /b 1
)

echo [java] Java JDK installed successfully
exit /b 0
