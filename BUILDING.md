# Building the fix

Requirements: Windows, [.NET SDK 10](https://dotnet.microsoft.com/download), PowerShell.

1. Get the sources from GitHub and put them into these folders (the paths are set at the top of
   `author-tools/prepare-sources.bat` and `author-tools/make-fix.bat`, change them if you want):
   - `C:\src\osu` - [ppy/osu](https://github.com/ppy/osu), version **2026.1005.0**
   - `C:\src\osu-framework` - [ppy/osu-framework](https://github.com/ppy/osu-framework), version **2026.921.1**
   - `C:\src\veldrid` - [ppy/veldrid](https://github.com/ppy/veldrid), version **2026.322.0**
2. In PowerShell, connect the local copies (these scripts come with the osu sources):
   ```
   cd C:\src\osu-framework
   .\UseLocalVeldrid.ps1
   cd C:\src\osu
   .\UseLocalFramework.ps1
   ```
3. Run `author-tools\prepare-sources.bat`. It overwrites a few files in the sources:
   - `veldrid-patch/` - the fix itself (3 files in Veldrid);
   - `framework-fps-only/` - original osu-framework files; only one line in `GameHost.cs` is changed
     (FPS limit 2000);
   - `osu-clean/` - original osu files.
4. Run `author-tools\make-fix.bat`. One build creates the files for both editions:
   - `ppy.Veldrid.dll` goes into `osu-fix-online/files` and `osu-fix-partial-offline/files`;
   - `osu.Framework.dll` goes into `osu-fix-partial-offline/files`.
5. Run `author-tools\publish-patch.bat`. It zips both `osu-fix-...` folders into `release/<tag>/`
   (these zips are the release files), writes `SHA256.txt` for the release notes and adds the patch to
   `update/manifest.json` (see below).

The assembly version of the built DLLs is raised on purpose (2099): .NET accepts a DLL whose version
is not lower than the one the official game asks for.

How it works: osu!framework sets `AllowTearing` for the swapchain every frame depending on the window
mode. The patched `D3D11Swapchain` in Veldrid uses this as the trigger to enter real DXGI exclusive
fullscreen (the swapchain is recreated in the classic bitblt model while exclusive) and leaves it when the
window loses focus.

## Online list and program updates

The installer program (`installer-gui`, `osu-lazer-fix.exe`) reads `update/manifest.json` from this
repository at start. The manifest lists the patches (game version, edition, download address, SHA-256 of every
DLL) and the newest version of the program. New game versions therefore reach every user without a new exe,
and the program can update itself. Everything downloaded is checked by SHA-256 before it is used.

Publishing a new patch:

1. `author-tools\make-fix.bat`, then `author-tools\publish-patch.bat`.
2. Create a GitHub release with the tag it prints and upload both zips from `release/<tag>/`.
3. `author-tools\check-manifest.bat` (downloads every file listed in the manifest and checks it).
4. Commit and push `update/manifest.json`.

Publishing a new version of the program:

1. Raise `<Version>` in `installer-gui/OsuLazerFix.csproj`, run `installer-gui\build.bat`.
2. `author-tools\publish-app.bat <tag>`, create a release with that tag and upload `osu-lazer-fix.exe`.
3. `author-tools\check-manifest.bat`, then commit and push `update/manifest.json`.

The author tools need the .NET 10 SDK (`dotnet run` of `author-tools/manifest-tool.cs`).
