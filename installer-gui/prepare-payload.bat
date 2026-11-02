@echo off
setlocal
rem === Copies the built DLLs into the payload folder so that build.bat embeds them into the exe. ===
rem Run make-fix.bat (author-tools) first. This folder must lie next to osu-fix-online and osu-fix-partial-offline.
set "GV=2026.1005.0"
set "ON=%~dp0..\osu-fix-online\files"
set "OFF=%~dp0..\osu-fix-partial-offline\files"
set "DEST=%~dp0payload\%GV%"

if not exist "%ON%\ppy.Veldrid.dll" goto :missing
if not exist "%OFF%\ppy.Veldrid.dll" goto :missing
if not exist "%OFF%\osu.Framework.dll" goto :missing

mkdir "%DEST%\online" 2>nul
mkdir "%DEST%\partial-offline" 2>nul
copy /y "%ON%\ppy.Veldrid.dll" "%DEST%\online\" >nul
if errorlevel 1 goto :fail
copy /y "%OFF%\ppy.Veldrid.dll" "%DEST%\partial-offline\" >nul
if errorlevel 1 goto :fail
copy /y "%OFF%\osu.Framework.dll" "%DEST%\partial-offline\" >nul
if errorlevel 1 goto :fail

echo Done. The DLLs are in %DEST%
echo Now run build.bat.
pause
exit /b 0

:missing
echo The built DLLs were not found in osu-fix-online\files and osu-fix-partial-offline\files.
echo Run author-tools\make-fix.bat first.
pause
exit /b 1

:fail
echo Copying failed.
pause
exit /b 1
