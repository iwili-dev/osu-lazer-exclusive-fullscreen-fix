# osu!lazer Fix - true exclusive fullscreen (removes input lag)

Unofficial fix for **osu!lazer 2026.1005.0** on Windows. It adds real exclusive fullscreen
(Direct3D 11 renderer), which removes input lag. Made by **iwili**.

[Русская версия](README.ru.md)

[![Support me on Ko-fi](https://shields.io)](https://ko-fi.com/iwili)

> **Read this first.** This is NOT an official version of the game. The fix was originally made for
> personal use. If something goes wrong, the author is not responsible. I do not know whether an
> account can be banned for using modified game files: it may happen or it may not.
> Everything is done at your own risk.

## Two editions

| | **Online** | **Partial offline** |
|---|---|---|
| Replaced game files | `ppy.Veldrid.dll` | `ppy.Veldrid.dll`, `osu.Framework.dll` |
| Exclusive fullscreen | yes | yes |
| FPS limit | normal game limit (1000) | **2000** |
| Online features | not blocked | only: logging in, downloading maps inside the game, top scores on a map, list of online players. No multiplayer, no ranked play, no score submission |

## Download

Get the `.zip` of the edition you want from the **[Releases](../../releases)** page.

## Install

1. Extract the whole archive, close osu!lazer.
2. Run `apply.bat`, read the warning, press `Y`.
3. If the game is not in the default folder (for example it is on drive D:), a window opens: select the osu!lazer folder.
4. In the game: Settings - Graphics - Renderer: **Direct3D 11**, Layout mode: **Fullscreen**.

To remove the fix, run `revert.bat`. Full instructions (English and Russian) are inside each archive
(`README_EN.txt`, `README_RU.txt`) and in the `osu-fix-*` folders of this repository.

If osu!lazer updates itself, the fix disappears and a new version of the fix is needed.

### Graphics tablet

If the cursor lags when you play with a graphics tablet, it is most likely caused by OpenTabletDriver running outside the game. Use the built-in tablet driver of osu!lazer instead (Settings - Input - Tablet).

### Multiple monitors

If you use 2 or more monitors, it is better not to use Alt+Tab while the game is in exclusive fullscreen.

## Build it yourself

See [BUILDING.md](BUILDING.md).

## Licences

Contains modified builds of [Veldrid](https://github.com/ppy/veldrid) (MIT) and, in the partial offline edition,
[osu!framework](https://github.com/ppy/osu-framework) (MIT). Licence texts are in `licenses/`.
osu! is a trademark of ppy Pty Ltd. This project is not affiliated with ppy.
