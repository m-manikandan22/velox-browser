@echo off
echo ============================================
echo   Building Velox Browser (Self-Contained)
echo   Works on ANY Windows PC - no .NET needed!
echo ============================================
echo.

dotnet publish VeloxBrowser.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish_standalone

echo.
if %ERRORLEVEL% EQU 0 (
    echo ============================================
    echo   BUILD SUCCESSFUL!
    echo   Your EXE is in: publish_standalone\
    echo   File: publish_standalone\VeloxBrowser.exe
    echo ============================================
    explorer publish_standalone
) else (
    echo ============================================
    echo   BUILD FAILED! Check errors above.
    echo ============================================
)

pause
