using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace OsuLazerFix
{
    /// <summary>An error whose message is meant to be shown to the user as it is.</summary>
    internal sealed class UserException : Exception
    {
        public readonly bool NeedsAdmin;

        public UserException(string message, bool needsAdmin = false) : base(message)
        {
            NeedsAdmin = needsAdmin;
        }
    }

    internal enum InstallState { NoGame, Unsupported, Original, Online, PartialOffline, Unknown }

    internal sealed class GameInfo
    {
        public string GameDir;     // folder that contains osu!.exe
        public string BackupDir;   // osu-fix-backup (same place the .bat files use)
        public string Version = "";

        public string ExePath { get { return Path.Combine(GameDir, "osu!.exe"); } }
    }

    /// <summary>
    /// The same logic as apply.bat / revert.bat, with the same backup layout
    /// (version.txt, layer-added.flag), so the exe and the .bat files can undo each other's work.
    /// </summary>
    internal static class Patcher
    {
        public const string EditionOnline = "online";
        public const string EditionOffline = "partial-offline";

        private const string Veldrid = "ppy.Veldrid.dll";
        private const string Framework = "osu.Framework.dll";
        private const string LayersKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string LayerValue = "~ DISABLEDXMAXIMIZEDWINDOWEDMODE";

        public static string DefaultRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osulazer");
        }

        /// <summary>Accepts the osulazer folder, the "current" folder, or any folder that contains osu!.exe.</summary>
        public static GameInfo Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            string p = path.Trim().Trim('"');
            string gameDir = null;

            try
            {
                if (File.Exists(Path.Combine(p, "current", "osu!.exe"))) gameDir = Path.Combine(p, "current");
                else if (File.Exists(Path.Combine(p, "osu!.exe"))) gameDir = p;
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (gameDir == null) return null;

            // The backup lives NEXT to "current", because the game replaces "current" when it updates.
            string trimmed = p.TrimEnd('\\', '/');
            string backupRoot = p;
            if (trimmed.EndsWith("\\current", StringComparison.OrdinalIgnoreCase))
                backupRoot = Path.GetDirectoryName(trimmed);

            var info = new GameInfo { GameDir = gameDir, BackupDir = Path.Combine(backupRoot, "osu-fix-backup") };
            info.Version = ShortVersion(FileVersionInfo.GetVersionInfo(info.ExePath).ProductVersion);
            return info;
        }

        /// <summary>"2026.1005.0-lazer+hash" -> "2026.1005.0"</summary>
        public static string ShortVersion(string productVersion)
        {
            if (string.IsNullOrEmpty(productVersion)) return "";
            int i = productVersion.IndexOfAny(new[] { '-', '+' });
            return i < 0 ? productVersion : productVersion.Substring(0, i);
        }

        public static bool IsGameRunning()
        {
            Process[] list = Process.GetProcessesByName("osu!");
            bool running = list.Length > 0;
            foreach (Process p in list) p.Dispose();
            return running;
        }

        public static bool BackupExists(GameInfo g)
        {
            return g != null && File.Exists(Path.Combine(g.BackupDir, Veldrid));
        }

        public static string ReadBackupVersion(GameInfo g)
        {
            try
            {
                return File.ReadAllText(Path.Combine(g.BackupDir, "version.txt")).Trim();
            }
            catch (Exception)
            {
                return "";
            }
        }

        // ------------------------------------------------------------ state

        public static InstallState DetectState(GameInfo g, PatchSet set)
        {
            if (g == null) return InstallState.NoGame;
            if (set == null) return InstallState.Unsupported;

            string veldridPath = Path.Combine(g.GameDir, Veldrid);
            if (!File.Exists(veldridPath)) return InstallState.Unknown;

            try
            {
                // any build of the fix for this game version counts (embedded, online, or an older online build)
                if (set.IsKnown(Veldrid, Payload.HashFile(veldridPath)))
                {
                    string frameworkPath = Path.Combine(g.GameDir, Framework);
                    if (File.Exists(frameworkPath) && set.IsKnown(Framework, Payload.HashFile(frameworkPath)))
                        return InstallState.PartialOffline;

                    return InstallState.Online;
                }

                return IsOfficial(g, set, Veldrid) ? InstallState.Original : InstallState.Unknown;
            }
            catch (IOException)
            {
                return InstallState.Unknown;
            }
        }

        /// <summary>True if the game has exactly the newest files of this edition (false = an older build of the fix is installed).</summary>
        public static bool IsCurrentBuild(GameInfo g, PatchSet set, string edition)
        {
            List<PatchFile> files = set == null ? null : set.Files(edition);
            if (g == null || files == null) return false;

            try
            {
                foreach (PatchFile f in files)
                {
                    string path = Path.Combine(g.GameDir, f.Name);
                    if (!File.Exists(path) || !string.Equals(Payload.HashFile(path), f.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool IsOfficial(GameInfo g, PatchSet set, string file)
        {
            string prefix = file == Veldrid ? set.VeldridPrefix : file == Framework ? set.FrameworkPrefix : null;
            string path = Path.Combine(g.GameDir, file);
            if (string.IsNullOrEmpty(prefix) || !File.Exists(path)) return false;

            string pv = FileVersionInfo.GetVersionInfo(path).ProductVersion;
            return pv != null && pv.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static void EnsureOfficial(GameInfo g, PatchSet set, string file)
        {
            if (!IsOfficial(g, set, file)) throw new UserException(Loc.T("err.dirty"));
        }

        // ------------------------------------------------------------ install

        public static void Install(GameInfo g, PatchSet set, string edition, Action<string> log)
        {
            if (IsGameRunning()) throw new UserException(Loc.T("err.running"));

            List<PatchFile> patchFiles = set.Files(edition);
            if (patchFiles == null || patchFiles.Count == 0)
                throw new UserException(Loc.T("err.noedition"));

            // read (and check) every new file BEFORE anything in the game folder is touched
            var data = new Dictionary<string, byte[]>();
            foreach (PatchFile pf in patchFiles)
            {
                byte[] bytes = Payload.Read(pf);
                if (bytes == null) throw new UserException(Loc.T(pf.Resource == null ? "err.notDownloaded" : "err.noedition"));
                data[pf.Name] = bytes;
            }
            var files = new List<string>(data.Keys);

            string backup = g.BackupDir;
            string layerFlag = Path.Combine(backup, "layer-added.flag");

            try
            {
                // 1. A backup left over from a previous version of the fix is out of date: replace it.
                bool keepFlag = false;
                if (File.Exists(Path.Combine(backup, Veldrid)) && ReadBackupVersion(g) != set.GameVersion)
                {
                    log(Loc.T("log.stale"));
                    foreach (string f in files) EnsureOfficial(g, set, f);   // only delete it if the game files are original
                    keepFlag = File.Exists(layerFlag);
                    Directory.Delete(backup, true);
                    log(Loc.T("log.staleRemoved"));
                }

                // 2. Save the original of every file this edition replaces (only if there is no backup of it yet).
                foreach (string f in files)
                {
                    string b = Path.Combine(backup, f);
                    if (File.Exists(b)) continue;

                    EnsureOfficial(g, set, f);
                    Directory.CreateDirectory(backup);
                    File.Copy(Path.Combine(g.GameDir, f), b, false);
                    log(Loc.T("log.backedUp", f));
                }

                Directory.CreateDirectory(backup);
                File.WriteAllText(Path.Combine(backup, "version.txt"), set.GameVersion);
                if (keepFlag) File.WriteAllText(layerFlag, "1");

                // 3. Switching from PARTIAL OFFLINE to ONLINE: put the official framework back.
                string frameworkBackup = Path.Combine(backup, Framework);
                if (edition == EditionOnline && File.Exists(frameworkBackup))
                {
                    File.Copy(frameworkBackup, Path.Combine(g.GameDir, Framework), true);
                    log(Loc.T("log.framework"));
                }

                // 4. "Disable fullscreen optimizations" for osu!.exe.
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(LayersKey))
                {
                    if (key != null && key.GetValue(g.ExePath) == null)
                    {
                        key.SetValue(g.ExePath, LayerValue, RegistryValueKind.String);
                        File.WriteAllText(layerFlag, "1");
                        log(Loc.T("log.layer"));
                    }
                }

                // 5. Copy the patched files.
                foreach (string f in files)
                {
                    File.WriteAllBytes(Path.Combine(g.GameDir, f), data[f]);
                    log(Loc.T("log.copied", f));
                }
            }
            catch (UserException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                RestoreBackups(g, false);
                throw new UserException(Loc.T("err.access"), true);
            }
            catch (Exception ex)
            {
                RestoreBackups(g, false);
                throw new UserException(Loc.T("err.copy", ex.Message));
            }
        }

        // ------------------------------------------------------------ revert

        public static void Revert(GameInfo g, Action<string> log)
        {
            string backup = g.BackupDir;

            if (!File.Exists(Path.Combine(backup, Veldrid)))
            {
                log(Loc.T("log.nothing"));
                return;
            }

            if (IsGameRunning()) throw new UserException(Loc.T("err.running"));

            try
            {
                // Only touch the game files if the game has not been updated since the fix was installed.
                string saved = ReadBackupVersion(g);
                if (saved.Length > 0 && saved == g.Version)
                {
                    RestoreBackups(g, true);
                    log(Loc.T("log.restored"));
                }
                else
                {
                    log(Loc.T("log.updatedSkip"));
                }

                if (File.Exists(Path.Combine(backup, "layer-added.flag")))
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(LayersKey, true))
                    {
                        if (key != null && key.GetValue(g.ExePath) != null)
                        {
                            key.DeleteValue(g.ExePath, false);
                            log(Loc.T("log.layerRemoved"));
                        }
                    }
                }

                Directory.Delete(backup, true);
                log(Loc.T("log.revertDone"));
            }
            catch (UnauthorizedAccessException)
            {
                throw new UserException(Loc.T("err.access"), true);
            }
            catch (IOException ex)
            {
                throw new UserException(Loc.T("err.revert", ex.Message));
            }
        }

        /// <summary>Copies the saved originals back into the game folder.</summary>
        private static void RestoreBackups(GameInfo g, bool throwOnError)
        {
            foreach (string f in new[] { Veldrid, Framework })
            {
                string b = Path.Combine(g.BackupDir, f);
                if (!File.Exists(b)) continue;

                try
                {
                    File.Copy(b, Path.Combine(g.GameDir, f), true);
                }
                catch (Exception)
                {
                    if (throwOnError) throw;
                }
            }
        }
    }
}
