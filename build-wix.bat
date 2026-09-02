@echo off
REM WiX v7.0 build script for Auto Theme Tray
REM Requires: dotnet, WiX Toolset v7.0 on PATH

setlocal enabledelayedexpansion

REM 1) Publish the app to a local folder
echo Publishing project (Release)...
dotnet publish -c Release -f net10.0-windows10.0.22621 -o publish
if %ERRORLEVEL% NEQ 0 (
  echo dotnet publish failed!
  exit /b 1
)

if not exist "publish\AutoThemeTray.exe" (
  echo Publish output not found. Check build logs.
  exit /b 1
)

REM 2) Ensure WiX v7.0 tool is available
where wix.exe >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
  echo wix.exe not found in PATH. Install WiX Toolset v7.0 (https://wixtoolset.org/) and add its bin folder to PATH.
  exit /b 1
)

REM 3) Accept WiX OSMF EULA (one-time, idempotent)
wix eula accept wix7 >nul 2>&1

REM 4) Create Output directory if it doesn't exist
if not exist Output mkdir Output

REM 5) Build MSI using WiX v7.0 unified CLI
echo Building MSI with wix.exe...
set PUBDIR=%CD%\publish
wix build -o Output\AutoThemeTray.msi -arch x64 -d PublishDir=%PUBDIR% installer.wxs
if %ERRORLEVEL% NEQ 0 (
  echo wix build failed
  exit /b 1
)

echo.
echo ========================================
echo MSI created successfully!
echo Location: %CD%\Output\AutoThemeTray.msi
echo ========================================
