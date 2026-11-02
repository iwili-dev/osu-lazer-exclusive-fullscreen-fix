@echo off
setlocal
rem === Builds osu-lazer-fix.exe (one file, runs on Windows 10/11 without installing anything). ===
rem The DLLs in payload\ are optional now: without them the program downloads the patches from the online list
rem (update\manifest.json). With them it also works without internet for those game versions.
cd /d "%~dp0"

if not exist "payload\2026.1005.0\online\ppy.Veldrid.dll" echo Note: no DLLs in payload\, the exe will download the patches from the internet.

dotnet build OsuLazerFix.csproj -c Release
if errorlevel 1 goto :fail

copy /y "bin\Release\net48\osu-lazer-fix.exe" "%~dp0osu-lazer-fix.exe" >nul
if errorlevel 1 goto :fail

echo.
echo Done: %~dp0osu-lazer-fix.exe
echo To publish it as an update: author-tools\publish-app.bat ^<tag^>
pause
exit /b 0

:fail
echo Build failed. Send me the error text.
pause
exit /b 1
