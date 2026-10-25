# Building the fix

The patch against Veldrid 2026.322.0 is available as a readable diff [here](https://github.com/iwili-dev/veldrid/tree/exclusive-fullscreen).

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
5. Zip each `osu-fix-...` folder separately. These zips are the release files.

The assembly version of the built DLLs is raised on purpose (2099): .NET accepts a DLL whose version
is not lower than the one the official game asks for.

How it works: osu!framework sets `AllowTearing` for the swapchain every frame depending on the window
mode. The patched `D3D11Swapchain` in Veldrid uses this as the trigger to enter real DXGI exclusive
fullscreen (the swapchain is recreated in the classic bitblt model while exclusive) and leaves it when the
window loses focus.
