@echo off
setlocal
title osu!lazer Fix - publish the program
rem === Author tool. Run AFTER installer-gui\build.bat. ===
rem Copies osu-lazer-fix.exe into release\<tag>\ and writes its version and SHA-256 into update\manifest.json.
rem Usage: publish-app.bat app-1.0.1
if "%~1"=="" goto :usage
cd /d "%~dp0.."
dotnet run "%~dp0manifest-tool.cs" -- app %1
if errorlevel 1 goto :fail
pause
exit /b 0

:usage
echo Give the release tag, for example: publish-app.bat app-1.0.1
pause
exit /b 1

:fail
echo Something went wrong. Send me the text above.
pause
exit /b 1
