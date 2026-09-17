@echo off
echo ========================================
echo  GamePort Cashier - Publish Script
echo ========================================
echo.

set PROJECT=src\GamePort.Cashier\GamePort.Cashier.csproj
set OUTDIR=publish\output
set ZIPNAME=WingPort-Cashier-v1.0.0.zip

echo [1/4] Clean previous publish...
if exist %OUTDIR% rmdir /s /q %OUTDIR%

echo [2/4] Publishing self-contained (Win-x64)...
dotnet publish %PROJECT% -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o %OUTDIR%

if %ERRORLEVEL% neq 0 (
    echo PUBLISH FAILED
    exit /b 1
)

echo [3/4] Copying files...
copy appsettings.json %OUTDIR%\appsettings.json >nul 2>&1
copy LICENSE %OUTDIR%\LICENSE >nul 2>&1

echo [4/4] Creating archive...
powershell -Command "Compress-Archive -Path '%OUTDIR%\*' -DestinationPath '%ZIPNAME%' -Force"

echo.
echo DONE: %ZIPNAME%
echo Size: 
powershell -Command "(Get-Item '%ZIPNAME%').Length / 1MB"
echo.
pause
