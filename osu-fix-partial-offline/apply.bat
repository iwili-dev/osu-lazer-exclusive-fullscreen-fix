@echo off
setlocal EnableExtensions
title osu!lazer Fix by iwili - PARTIAL OFFLINE edition
set "FIXVER=2026.1005.0"
set "FILES=%~dp0files"
set "LAYERS=HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"
set "OSU_FIX_EXPECT=%FIXVER%"

cls
echo ============================================================
echo   osu!lazer Fix ^(%FIXVER%^) by iwili
echo   PARTIAL OFFLINE edition
echo   True exclusive fullscreen - removes input lag, 2000 FPS limit
echo ============================================================
echo.
echo   WARNING
echo.
echo   This is NOT an official version of the game.
echo   This patch replaces game files with modified ones.
echo   It was originally made for personal use. If anything goes wrong,
echo   the author is not responsible.
echo   The author does not know whether an account can be banned for
echo   using it: it may happen or it may not. You use this patch
echo   entirely at your own risk.
echo.
echo   ONLINE LIMITATIONS OF THIS EDITION
echo   Only these online features work: logging in to your account,
echo   downloading maps inside the game, seeing the top scores on a map
echo   and seeing the list of online players. All other online features
echo   are NOT available ^(for example multiplayer, ranked play and
echo   score submission^).
echo.
choice /c YN /n /m "Do you accept the risks and want to continue? [Y/N]: "
if errorlevel 2 goto :cancel

if not exist "%FILES%\ppy.Veldrid.dll" goto :nofiles
if not exist "%FILES%\osu.Framework.dll" goto :nofiles

rem ---------- locate the game ----------
set "ROOT="
set "GAME="
set "PICKED="
if exist "%LOCALAPPDATA%\osulazer\current\osu!.exe" set "ROOT=%LOCALAPPDATA%\osulazer"
if defined ROOT goto :resolve

echo.
echo The game was not found in the default location:
echo   %LOCALAPPDATA%\osulazer
echo.
echo A window will open. Please select the osu!lazer folder.
echo It is the folder that contains osu!.exe or a folder named "current".
echo.
pause
set "OSU_FIX_PROMPT=Select the osu!lazer folder. It contains osu!.exe or a folder named current."
for /f "usebackq delims=" %%P in (`powershell -NoProfile -STA -Command "Add-Type -AssemblyName System.Windows.Forms; $f=New-Object System.Windows.Forms.Form; $f.TopMost=$true; $d=New-Object System.Windows.Forms.FolderBrowserDialog; $d.Description=$env:OSU_FIX_PROMPT; if($d.ShowDialog($f) -eq 'OK'){$d.SelectedPath}"`) do set "PICKED=%%P"
if not defined PICKED goto :nopick
set "ROOT=%PICKED%"

:resolve
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
if exist "%ROOT%\current\osu!.exe" set "GAME=%ROOT%\current"
if not defined GAME if exist "%ROOT%\osu!.exe" set "GAME=%ROOT%"
if not defined GAME goto :notfound
if not exist "%GAME%\ppy.Veldrid.dll" goto :notfound
set "BACKUP=%ROOT%\osu-fix-backup"
if /I "%ROOT:~-8%"=="\current" set "BACKUP=%ROOT:~0,-8%\osu-fix-backup"
set "OSU_FIX_GAME=%GAME%"

echo.
echo Game folder   : %GAME%
echo Backup folder : %BACKUP%
echo.

tasklist /FI "IMAGENAME eq osu!.exe" 2>nul | find /I "osu!.exe" >nul
if not errorlevel 1 goto :running

powershell -NoProfile -Command "if((Get-Item -LiteralPath (Join-Path $env:OSU_FIX_GAME 'osu!.exe')).VersionInfo.ProductVersion.StartsWith($env:OSU_FIX_EXPECT)){exit 0}else{exit 1}"
if errorlevel 1 goto :badversion

if exist "%BACKUP%\ppy.Veldrid.dll" goto :backed1
rem No backup of this file yet: it can only be saved if the game currently has the OFFICIAL file.
set "OSU_FIX_PREFIX=4.9.69+"
powershell -NoProfile -Command "if((Get-Item -LiteralPath (Join-Path $env:OSU_FIX_GAME 'ppy.Veldrid.dll')).VersionInfo.ProductVersion.StartsWith($env:OSU_FIX_PREFIX)){exit 0}else{exit 1}"
if errorlevel 1 goto :dirty
mkdir "%BACKUP%" 2>nul
copy /y "%GAME%\ppy.Veldrid.dll" "%BACKUP%\" >nul
if errorlevel 1 goto :copyfail
:backed1
if exist "%BACKUP%\osu.Framework.dll" goto :backed2
rem No backup of this file yet: it can only be saved if the game currently has the OFFICIAL file.
set "OSU_FIX_PREFIX=2026.921.1+"
powershell -NoProfile -Command "if((Get-Item -LiteralPath (Join-Path $env:OSU_FIX_GAME 'osu.Framework.dll')).VersionInfo.ProductVersion.StartsWith($env:OSU_FIX_PREFIX)){exit 0}else{exit 1}"
if errorlevel 1 goto :dirty
mkdir "%BACKUP%" 2>nul
copy /y "%GAME%\osu.Framework.dll" "%BACKUP%\" >nul
if errorlevel 1 goto :copyfail
:backed2
> "%BACKUP%\version.txt" echo %FIXVER%

reg query "%LAYERS%" /v "%GAME%\osu!.exe" >nul 2>&1
if not errorlevel 1 goto :layerdone
reg add "%LAYERS%" /v "%GAME%\osu!.exe" /t REG_SZ /d "~ DISABLEDXMAXIMIZEDWINDOWEDMODE" /f >nul
> "%BACKUP%\layer-added.flag" echo 1
:layerdone

copy /y "%FILES%\ppy.Veldrid.dll" "%GAME%\" >nul
if errorlevel 1 goto :copyfail
copy /y "%FILES%\osu.Framework.dll" "%GAME%\" >nul
if errorlevel 1 goto :copyfail

> "%~dp0location-game.txt" echo %GAME%
> "%~dp0location-backup.txt" echo %BACKUP%

echo.
echo ============================================================
echo   Done! The fix is installed.
echo ============================================================
echo.
echo   In the game: Settings - Graphics - Renderer: Direct3D 11,
echo   Layout mode: Fullscreen.
echo.
echo   To remove the fix, run revert.bat.
echo   If the game updates itself, the fix disappears: you will need a
echo   new version of this fix.
echo.
pause
exit /b 0

:cancel
echo.
echo Cancelled. Nothing was changed.
pause
exit /b 1

:nofiles
echo.
echo The "files" folder with the fix files was not found next to apply.bat.
echo Please extract the whole archive and run apply.bat from the extracted folder.
pause
exit /b 1

:nopick
echo.
echo No folder was selected. Nothing was changed.
pause
exit /b 1

:notfound
echo.
echo osu!.exe and ppy.Veldrid.dll were not found in the selected folder.
echo Nothing was changed. Run apply.bat again and choose the osu!lazer folder.
pause
exit /b 1

:running
echo osu!lazer is running. Please close the game and run apply.bat again.
pause
exit /b 1

:badversion
echo.
echo The installed game version does not match this fix. Required: osu!lazer %FIXVER%.
echo Nothing was changed.
pause
exit /b 1

:dirty
echo.
echo The game folder already contains a patched file, but there is no backup of the original.
echo Please reinstall osu!lazer with the official installer from osu.ppy.sh ^(your maps and
echo settings are kept^) and run apply.bat again.
pause
exit /b 1

:copyfail
echo.
echo Could not copy the file. If the game is in a protected folder ^(for example Program Files^),
echo right-click apply.bat and choose "Run as administrator". Nothing else was changed.
pause
exit /b 1
