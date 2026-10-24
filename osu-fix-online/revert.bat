@echo off
setlocal EnableExtensions
title osu!lazer Fix by iwili - uninstall
set "LAYERS=HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"

cls
echo ============================================================
echo   osu!lazer Fix by iwili - uninstall
echo ============================================================
echo.

rem ---------- locate the installation (saved by apply.bat) ----------
set "GAME="
set "BACKUP="
if exist "%~dp0location-game.txt" set /p GAME=<"%~dp0location-game.txt"
if exist "%~dp0location-backup.txt" set /p BACKUP=<"%~dp0location-backup.txt"
if defined GAME if defined BACKUP if exist "%BACKUP%\ppy.Veldrid.dll" goto :located

if exist "%LOCALAPPDATA%\osulazer\osu-fix-backup\ppy.Veldrid.dll" if exist "%LOCALAPPDATA%\osulazer\current\osu!.exe" (
    set "GAME=%LOCALAPPDATA%\osulazer\current"
    set "BACKUP=%LOCALAPPDATA%\osulazer\osu-fix-backup"
    goto :located
)

echo The installation was not found automatically.
echo A window will open. Please select the osu!lazer folder that you used when installing the fix.
echo.
pause
set "OSU_FIX_PROMPT=Select the osu!lazer folder. It contains osu!.exe or a folder named current."
set "PICKED="
for /f "usebackq delims=" %%P in (`powershell -NoProfile -STA -Command "Add-Type -AssemblyName System.Windows.Forms; $f=New-Object System.Windows.Forms.Form; $f.TopMost=$true; $d=New-Object System.Windows.Forms.FolderBrowserDialog; $d.Description=$env:OSU_FIX_PROMPT; if($d.ShowDialog($f) -eq 'OK'){$d.SelectedPath}"`) do set "PICKED=%%P"
if not defined PICKED goto :nopick
set "ROOT=%PICKED%"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "GAME="
if exist "%ROOT%\current\osu!.exe" set "GAME=%ROOT%\current"
if not defined GAME if exist "%ROOT%\osu!.exe" set "GAME=%ROOT%"
if not defined GAME goto :nothing
set "BACKUP=%ROOT%\osu-fix-backup"
if /I "%ROOT:~-8%"=="\current" set "BACKUP=%ROOT:~0,-8%\osu-fix-backup"

:located
set "OSU_FIX_GAME=%GAME%"
echo Game folder   : %GAME%
echo Backup folder : %BACKUP%
echo.
if not exist "%BACKUP%\ppy.Veldrid.dll" goto :nothing

choice /c YN /n /m "Restore the original game files and remove the fix? [Y/N]: "
if errorlevel 2 goto :cancel

tasklist /FI "IMAGENAME eq osu!.exe" 2>nul | find /I "osu!.exe" >nul
if not errorlevel 1 goto :running

set "OSU_FIX_EXPECT="
if exist "%BACKUP%\version.txt" set /p OSU_FIX_EXPECT=<"%BACKUP%\version.txt"
powershell -NoProfile -Command "if((Get-Item -LiteralPath (Join-Path $env:OSU_FIX_GAME 'osu!.exe')).VersionInfo.ProductVersion.StartsWith($env:OSU_FIX_EXPECT)){exit 0}else{exit 1}"
if errorlevel 1 goto :updated

copy /y "%BACKUP%\ppy.Veldrid.dll" "%GAME%\" >nul
if errorlevel 1 goto :copyfail
if exist "%BACKUP%\osu.Framework.dll" copy /y "%BACKUP%\osu.Framework.dll" "%GAME%\" >nul
echo The original files were restored.
goto :cleanup

:updated
echo The game was updated after the fix was installed. The game files were NOT touched.

:cleanup
if exist "%BACKUP%\layer-added.flag" reg delete "%LAYERS%" /v "%GAME%\osu!.exe" /f >nul 2>&1
rd /s /q "%BACKUP%"
if exist "%~dp0location-game.txt" del "%~dp0location-game.txt"
if exist "%~dp0location-backup.txt" del "%~dp0location-backup.txt"
echo.
echo Done. The fix has been removed.
pause
exit /b 0

:nopick
echo.
echo No folder was selected. Nothing was changed.
pause
exit /b 1

:nothing
echo.
echo No backup was found, so there is nothing to restore.
pause
exit /b 0

:cancel
echo.
echo Cancelled. Nothing was changed.
pause
exit /b 1

:running
echo osu!lazer is running. Please close the game and run revert.bat again.
pause
exit /b 1

:copyfail
echo.
echo Could not restore the files. The backup was kept in: %BACKUP%
echo If the game is in a protected folder, right-click revert.bat and choose "Run as administrator".
pause
exit /b 1
