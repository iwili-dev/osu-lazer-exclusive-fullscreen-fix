@echo off
setlocal
title osu!lazer Fix - publish a patch
rem === Author tool. Run AFTER make-fix.bat. ===
rem Makes the two release zips in release\<tag>\ and adds the patch to update\manifest.json.
rem The game version and the prefixes are read from osu-fix-online\apply.bat and osu-fix-partial-offline\apply.bat.
rem Usage: publish-patch.bat            - the tag is chosen by itself, for example 2026.1005.0-1
rem        publish-patch.bat 2026.1105.0-1
cd /d "%~dp0.."
dotnet run "%~dp0manifest-tool.cs" -- patch %1
if errorlevel 1 goto :fail
pause
exit /b 0

:fail
echo Something went wrong. Send me the text above.
pause
exit /b 1
