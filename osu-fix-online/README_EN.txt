osu!lazer Fix (2026.1005.0) by iwili
ONLINE edition - true exclusive fullscreen for Direct3D 11
REMOVES INPUT LAG
==========================================================

1. WHAT IT DOES
---------------
Removes input lag by adding real exclusive fullscreen to osu!lazer on Windows
(Direct3D 11 renderer).
One game file is replaced: ppy.Veldrid.dll.
Everything else stays official.

2. REQUIREMENTS
---------------
- Windows 10 or 11, 64-bit.
- Official osu!lazer version 2026.1005.0, installed from osu.ppy.sh.
  Any other version is refused by the installer.
- The game must be closed while you install or remove the fix.

3. WARNING - READ THIS FIRST
----------------------------
- This is NOT an official version of the game.
- This fix was originally made for personal use. If something goes wrong,
  the author (iwili) is not responsible.
- The author does not know whether an account can be banned for using
  modified game files: it may happen or it may not. Everything is done
  at your own risk.

4. ONLINE FEATURES AND FPS LIMIT
--------------------------------
- Online: this edition does not block the online features of the game.
- FPS limit: the normal game limit (up to 1000 FPS) is kept.

5. INSTALLATION
---------------
1) Extract the whole archive to any folder. Do not run apply.bat from inside
   the archive.
2) Close osu!lazer.
3) Double-click apply.bat. Read the warning and press Y to continue
   (N cancels, nothing is changed).
4) The installer looks for the game in %LOCALAPPDATA%\osulazer. If the game is
   installed somewhere else (for example on drive D:), a window opens: select
   the osu!lazer folder - the one that contains osu!.exe, or a folder named
   "current".
5) Wait for the message "Done! The fix is installed."
6) Start the game: Settings - Graphics - Renderer: Direct3D 11,
   Layout mode: Fullscreen. The screen blinks once when the game enters
   exclusive fullscreen.

6. BACKUP
---------
The original files are saved in a folder named "osu-fix-backup": next to the
game's "current" folder, or inside the folder you selected. Do not delete it
while the fix is installed.

7. UNINSTALL
------------
Close the game, run revert.bat and press Y. The original files are restored
and the backup is removed.

8. AFTER A GAME UPDATE
----------------------
If osu!lazer updates itself, the fix disappears (the game replaces its files).
The installer works only with version 2026.1005.0, so a new version of the fix
is needed for newer game versions.
When a new version of the fix is released, just run its apply.bat. It removes
the out-of-date backup of the previous version automatically and makes a new one.
(You can also run the old revert.bat first: it does not touch the files of the
updated game.) Until a new version of the fix is released, the game simply
works as the official one.

9. TROUBLESHOOTING
------------------
- Windows SmartScreen warning: click "More info", then "Run anyway"
  (the scripts are not signed).
- "Could not copy the file": right-click apply.bat and choose
  "Run as administrator" (only needed if the game is in a protected folder).
- "already contains a patched file, but there is no backup": reinstall
  osu!lazer with the official installer, then run apply.bat again.
- No exclusive mode: check that Renderer is Direct3D 11 and Layout mode is
  Fullscreen.
- To turn exclusive mode off without uninstalling, set the environment
  variable OSU_D3D11_EXCLUSIVE=0.
- Cursor lags when playing with a graphics tablet: most likely it is caused by
  OpenTabletDriver running outside the game. Use the built-in tablet driver of
  osu!lazer instead (Settings - Input - Tablet).
- If you use 2 or more monitors, it is better not to use Alt+Tab while the game
  is in exclusive fullscreen.
- Switching between the ONLINE and PARTIAL OFFLINE editions: run apply.bat of
  the other edition. revert.bat of either edition removes the fix completely.

10. CREDITS AND LICENCE
-----------------------
Fix by iwili. Contains a modified build of Veldrid (MIT licence, Eric Mellino
and contributors, ppy fork).
osu! is a trademark of ppy Pty Ltd. This project is not affiliated with ppy.
