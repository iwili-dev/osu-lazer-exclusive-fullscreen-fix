@echo off
title osu!lazer Fix - build both editions
rem === Author tool: ONE build produces the files for both editions. ===
rem   ppy.Veldrid.dll    -> osu-fix-online\files and osu-fix-partial-offline\files
rem   osu.Framework.dll  -> osu-fix-partial-offline\files
rem Run prepare-sources.bat first. Edit SRC if your osu sources are somewhere else.
set "SRC=C:\src\osu"
set "OUT=%SRC%\osu.Desktop\bin\Release\net10.0"
set "ONLINE=%~dp0..\osu-fix-online\files"
set "OFFLINE=%~dp0..\osu-fix-partial-offline\files"

if not exist "%SRC%\osu.Desktop" goto :nosrc

echo Building from %SRC% ... this can take a few minutes.
pushd "%SRC%"
rem The assembly version is raised on purpose: .NET accepts a DLL with a version NOT LOWER than the one the official game asks for.
dotnet build osu.Desktop -c Release --no-incremental -p:AssemblyVersion=2099.0.0.0 -p:FileVersion=2026.921.1.0
if errorlevel 1 goto :fail
popd

if not exist "%ONLINE%" mkdir "%ONLINE%"
if not exist "%OFFLINE%" mkdir "%OFFLINE%"
copy /y "%OUT%\ppy.Veldrid.dll" "%ONLINE%\" >nul
if errorlevel 1 goto :fail
copy /y "%OUT%\ppy.Veldrid.dll" "%OFFLINE%\" >nul
if errorlevel 1 goto :fail
copy /y "%OUT%\osu.Framework.dll" "%OFFLINE%\" >nul
if errorlevel 1 goto :fail

echo.
echo Done.
echo   osu-fix-online          : ppy.Veldrid.dll
echo   osu-fix-partial-offline : ppy.Veldrid.dll and osu.Framework.dll
echo Now zip each osu-fix-... folder separately and share it.
pause
exit /b 0

:nosrc
echo Source folder %SRC% was not found. Open this file in Notepad and fix the SRC path.
pause
exit /b 1

:fail
echo Build failed. Send me the error text.
pause
exit /b 1
