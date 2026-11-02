@echo off
setlocal
title osu!lazer Fix - check the manifest
rem === Author tool. Run AFTER uploading the release files and BEFORE pushing update\manifest.json. ===
rem Downloads every file the manifest points to and checks its SHA-256.
cd /d "%~dp0.."
dotnet run "%~dp0manifest-tool.cs" -- check
if errorlevel 1 goto :fail
pause
exit /b 0

:fail
echo Do not push update\manifest.json yet. Send me the text above.
pause
exit /b 1
