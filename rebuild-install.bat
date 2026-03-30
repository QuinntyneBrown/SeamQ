@echo off
REM Force full rebuild and reinstall of seamq CLI tool

cd /d C:\projects\SeamQ

REM Clean build outputs to force full rebuild
rmdir /s /q src\SeamQ.Cli\bin 2>nul
rmdir /s /q src\SeamQ.Cli\obj 2>nul
rmdir /s /q src\SeamQ.Renderer\bin 2>nul
rmdir /s /q src\SeamQ.Renderer\obj 2>nul
rmdir /s /q src\SeamQ.Core\bin 2>nul
rmdir /s /q src\SeamQ.Core\obj 2>nul
rmdir /s /q nupkg 2>nul

REM Full rebuild
dotnet build src\SeamQ.Cli -c Release --no-incremental
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    exit /b 1
)

REM Pack
dotnet pack src\SeamQ.Cli -c Release -o nupkg
if %ERRORLEVEL% NEQ 0 (
    echo PACK FAILED
    exit /b 1
)

REM Reinstall
dotnet tool uninstall --global seamq 2>nul
dotnet tool install --global --add-source nupkg seamq
if %ERRORLEVEL% NEQ 0 (
    echo INSTALL FAILED
    exit /b 1
)

echo SEAMQ REINSTALLED SUCCESSFULLY
