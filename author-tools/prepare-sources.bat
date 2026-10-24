@echo off
title osu!lazer Fix - prepare clean sources
rem === Author tool: puts the sources into the exact state needed to build the fix. ===
rem Edit the three paths below if your sources are somewhere else.
set "OSU=C:\src\osu"
set "FW=C:\src\osu-framework"
set "VELDRID=C:\src\veldrid"
set "KIT=%~dp0.."

if not exist "%OSU%\osu.Desktop" goto :nodir
if not exist "%FW%\osu.Framework" goto :nodir
if not exist "%VELDRID%\src\Veldrid" goto :nodir

echo This will overwrite these files in your sources:
echo   osu-framework : GameHost.cs ^(only the FPS limit line is changed, 2000^),
echo                   VeldridRenderer.cs, VeldridDevice.cs, ExtensionMethods.cs ^(original versions^)
echo   osu           : Program.cs, OsuGame.cs, SubmittingPlayer.cs ^(original versions^)
echo                   and DELETES osu.Desktop\Windows\FullscreenOptimisationsHelper.cs
echo   veldrid       : D3D11Swapchain.cs, D3D11GraphicsDevice.cs, BackendInfoD3D11.cs ^(the fix^)
echo.
choice /c YN /n /m "Continue? [Y/N]: "
if errorlevel 2 goto :cancel

xcopy /e /y /i "%KIT%\osu-clean\osu" "%OSU%" >nul
if errorlevel 1 goto :fail
xcopy /e /y /i "%KIT%\framework-fps-only\osu-framework\osu.Framework" "%FW%\osu.Framework" >nul
if errorlevel 1 goto :fail
xcopy /e /y /i "%KIT%\veldrid-patch\src" "%VELDRID%\src" >nul
if errorlevel 1 goto :fail
if exist "%OSU%\osu.Desktop\Windows\FullscreenOptimisationsHelper.cs" del "%OSU%\osu.Desktop\Windows\FullscreenOptimisationsHelper.cs"

echo.
echo Done. The sources are clean. Now run make-fix.bat.
pause
exit /b 0

:nodir
echo One of the source folders was not found. Open this file in Notepad and fix the paths at the top.
pause
exit /b 1

:cancel
echo Cancelled. Nothing was changed.
pause
exit /b 1

:fail
echo Copying failed. Send me the error text.
pause
exit /b 1
