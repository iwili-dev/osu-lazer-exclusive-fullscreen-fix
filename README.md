<div align="center">

# osu!lazer Exclusive Fullscreen Fix

**Real exclusive fullscreen mode for osu!lazer on Direct3D 11. Removes input lag.**

[English](README.md) · [Русский](README.ru.md)

<br>

<a href="https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/download/app-1.1.0/osu-lazer-fix.exe"><img src="https://img.shields.io/badge/Download-osu--lazer--fix.exe-ff7eb6?style=for-the-badge&logo=windows&logoColor=white" alt="Download osu-lazer-fix.exe"></a>

<sub>Windows 10 / 11 · no installation, no additional programs needed</sub>

<br>

[![Program](https://img.shields.io/badge/program-1.1.0-ffbad8?style=flat-square)](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/app-1.1.0)
[![Game](https://img.shields.io/badge/osu!lazer-2026.1005.0-ff66aa?style=flat-square)](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/2026.1005.0-2)
![Renderer](https://img.shields.io/badge/renderer-Direct3D%2011-444?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows-444?style=flat-square)

</div>

---

## What does the fix do?

In osu!lazer, the "Fullscreen" mode is not actually exclusive. The game draws into a windowed flip swapchain and never asks Windows for exclusive fullscreen mode, so the picture still goes through the desktop compositor. That is where the extra input lag comes from.

The fix makes the game take real DXGI exclusive fullscreen when Fullscreen and Direct3D 11 are selected in the settings. When the window loses focus, the game leaves exclusive mode and goes back into it when you return, so Alt+Tab works (the screen blinks once). For streamers with 2+ monitors there is an experimental "Two-monitor mode" in the program settings: the game stays in exclusive fullscreen while you click OBS or chat on another monitor.

The fix is installed on top of the regular game by replacing a DLL. `osu!.exe` and `osu.Game.dll` are not touched.

> [!WARNING]
> This is not an official version of the game. You perform all actions at your own risk. The author is not responsible for accounts and devices.

## Installation

1. **Download** [`osu-lazer-fix.exe`](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/download/app-1.1.0/osu-lazer-fix.exe).
2. **Close osu!lazer.**
3. **Run the exe.** The file is not signed, so Windows SmartScreen may show a blue window. Click *More info*, then *Run anyway*. For the same reason, some antivirus programs may complain about the file. The entire source code is in this repository, and [BUILDING.md](BUILDING.md) explains how to build the exe yourself.
4. **Top of the window.** It shows the current game version installed on your device. The program looks for the game in `%LOCALAPPDATA%\osulazer\current`. If the game is located elsewhere, specify the folder in the settings.
5. **Open the tab with your game version** and choose an edition (see the table below).
6. **Click "Install"** and confirm. The files will be downloaded, checked and copied. When the status turns green, you are done.
7. **In the game**, set Renderer to `Direct3D 11` and Layout mode to `Fullscreen`. When exclusive mode turns on, the screen blinks once.

## Editions

|  | **Online** | **Partial offline** |
|---|---|---|
| Replaces | `ppy.Veldrid.dll` | `ppy.Veldrid.dll`, `osu.Framework.dll` |
| FPS limit | 1000 (as it was) | raised to 2000 |
| Online | everything works | logging in, downloading maps in the game, top scores on a map, list of online players |
| Unavailable | nothing | multiplayer, ranked, score submission |

**Partial offline.** In this edition the servers do not accept the game as an official client, so the game shows "Realtime online functionality is not supported" and "SCORE WILL NOT BE SUBMITTED". This is expected.

## Removing the fix

In the program, click **Remove**. The original files will be restored from the backup. The backup is in the `osu-fix-backup` folder next to `current`.

## After a game update

The game updates by replacing its entire folder, so the fix disappears with every update. Until a patch for the new version is available, the game simply works as the official one.

On every launch, the program reads the list of patches from this repository, and the new version appears as a new tab. If an old backup is left, the program will notice it and replace it with a fresh one.

The program itself also updates. With auto-update enabled, it downloads the new version, checks it and replaces itself on the next launch.

## Possible errors

<details>
<summary><b>There is no tab for my game version</b></summary>

The patch for this version is not out yet. Check back later: the list is loaded from the internet. Without a connection, the program uses the last saved copy.
</details>

<details>
<summary><b>The program says the game files are not original</b></summary>

Someone has already modified `ppy.Veldrid.dll` or `osu.Framework.dll`. Reinstall the game and run the program again.
</details>

<details>
<summary><b>"Access denied" during installation</b></summary>

The game is in a protected folder. Click "Restart as administrator" in the program.
</details>

<details>
<summary><b>Exclusive mode does not turn on</b></summary>

Check that Renderer is set to Direct3D 11 and Layout mode is set to Fullscreen. OpenGL is not supported: Windows has no way to request exclusive mode for it, the driver decides everything.
</details>

<details>
<summary><b>The cursor lags with a graphics tablet</b></summary>

This happens on weak devices. Restart the game, it may take several restarts. OpenTabletDriver or another graphics tablet driver must be running before you launch the game. Otherwise, use the built-in driver in the game itself.
</details>

<details>
<summary><b>Alt+Tab works poorly</b></summary>

Alt+Tab works poorly in exclusive fullscreen mode: this is a feature of the mode itself, for the sake of maximum performance.
</details>

## Batch files instead of the exe

If you prefer scripts, the [`2026.1005.0-2` release](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/2026.1005.0-2) contains two zip archives, one per edition. Unpack the one you need, `apply.bat` installs the fix, `revert.bat` removes it. The exe and the batch files share the same backup, so they can undo each other's work.

## How it works

Veldrid, the graphics layer under osu!framework, creates a flip swapchain and never calls `SetFullscreenState`. On such a swapchain the call fails with the error `DXGI_ERROR_INVALID_CALL`. For the duration of exclusive mode, the fix switches to a classic bitblt swapchain, and on exit it returns to flip.

The patch is three files in Veldrid. You can read it as a diff in the fork: [iwili-dev/veldrid](https://github.com/iwili-dev/veldrid/tree/exclusive-fullscreen) (branch `exclusive-fullscreen`). The Partial offline edition additionally changes one line in osu!framework, the FPS limit.

Every downloaded file is checked against the SHA-256 from the manifest [`update/manifest.json`](update/manifest.json). The program can write only `ppy.Veldrid.dll` and `osu.Framework.dll` into the game folder.

Build instructions: [BUILDING.md](BUILDING.md).

## Links

- [Program release 1.1.0](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/app-1.1.0) (exe)
- [Patch release 2026.1005.0-2](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/2026.1005.0-2) (batch files)
- [Veldrid fork](https://github.com/iwili-dev/veldrid)

## Credits and licences

Fix by [iwili](https://github.com/iwili-dev).

Based on a modified [Veldrid](https://github.com/iwili-dev/veldrid) (MIT, Eric Mellino and contributors, ppy fork). The Partial offline edition also contains a modified [osu!framework](https://github.com/ppy/osu-framework) (MIT, ppy Pty Ltd). Licence texts are in [`licenses/`](licenses).

osu! is a trademark of ppy Pty Ltd. This project is not affiliated with ppy.

---

<div align="center">

### Support the author

<a href="https://ko-fi.com/iwili"><img src="https://img.shields.io/badge/Support_on-Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Support on Ko-fi"></a>

<sub>Thanks for using it.</sub>

</div>
