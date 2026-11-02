using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace OsuLazerFix
{
    /// <summary>The newest version of this program, as listed in the manifest.</summary>
    internal sealed class AppRelease
    {
        public Version Version;
        public string Url;
        public string Sha256;
        public long Size;
    }

    /// <summary>What was read from update/manifest.json.</summary>
    internal sealed class Manifest
    {
        public AppRelease App;
        public readonly List<PatchSet> Patches = new List<PatchSet>();
    }

    /// <summary>
    /// Everything that talks to the internet:
    /// 1) the online list of patches (update/manifest.json in the GitHub repository),
    /// 2) downloading patch files (checked by SHA-256 before they are used),
    /// 3) updating this exe in place.
    /// Nothing from the internet is used unless its SHA-256 matches the manifest.
    /// </summary>
    internal static class Online
    {
        public const string Repo = "iwili-dev/osu-lazer-exclusive-fullscreen-fix";

        /// <summary>The manifest addresses, tried in this order (the second one is a mirror for networks where raw.githubusercontent.com is blocked).</summary>
        private static readonly string[] ManifestUrls =
        {
            "https://raw.githubusercontent.com/" + Repo + "/main/update/manifest.json",
            "https://cdn.jsdelivr.net/gh/" + Repo + "@main/update/manifest.json"
        };

        /// <summary>The highest manifest format this program understands. A newer format only means "update the program".</summary>
        private const int ManifestFormat = 1;

        private const long MaxManifestBytes = 1024 * 1024;
        private const long MaxFileBytes = 64L * 1024 * 1024;

        /// <summary>Only these file names may ever be written into the game folder.</summary>
        private static readonly string[] AllowedFiles = { "ppy.Veldrid.dll", "osu.Framework.dll" };
        private static readonly string[] AllowedEditions = { Patcher.EditionOnline, Patcher.EditionOffline };

        public static bool IsAllowedFile(string name)
        {
            return Array.IndexOf(AllowedFiles, name) >= 0;
        }

        private static string DataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu-lazer-fix"); }
        }

        private static string ManifestCachePath
        {
            get { return Path.Combine(DataDir, "manifest.json"); }
        }

        public static Version CurrentVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        // ================================================================== http

        private static void PrepareTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
                // the system default is used
            }
        }

        /// <summary>Downloads a URL into memory. progress(received, total) is called from the calling thread; total is -1 when unknown.</summary>
        private static byte[] Get(string url, long maxBytes, Action<long, long> progress)
        {
            PrepareTls();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "osu-lazer-fix/" + CurrentVersion.ToString(3);
            req.AllowAutoRedirect = true;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                long total = resp.ContentLength;
                if (total > maxBytes) throw new IOException("The file is too big.");

                var buf = new byte[81920];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > maxBytes) throw new IOException("The file is too big.");
                    if (progress != null) progress(ms.Length, total);
                }
                return ms.ToArray();
            }
        }

        // ================================================================== manifest

        /// <summary>The manifest saved by the last successful check, so the list works without internet. Null if there is none.</summary>
        public static Manifest LoadCached()
        {
            try
            {
                if (!File.Exists(ManifestCachePath)) return null;
                return Parse(File.ReadAllText(ManifestCachePath, Encoding.UTF8));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Downloads the manifest (first address that works) and saves it for offline use. Throws if every address failed.</summary>
        public static Manifest Fetch()
        {
            Exception last = null;
            foreach (string baseUrl in ManifestUrls)
            {
                try
                {
                    // the query string skips stale CDN copies
                    string url = baseUrl + "?t=" + DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
                    string text = Encoding.UTF8.GetString(Get(url, MaxManifestBytes, null));
                    Manifest m = Parse(text);

                    try
                    {
                        Directory.CreateDirectory(DataDir);
                        File.WriteAllText(ManifestCachePath, text, new UTF8Encoding(false));
                    }
                    catch (Exception)
                    {
                        // the cache is optional
                    }
                    return m;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
            throw last ?? new IOException("No manifest address.");
        }

        private static readonly Regex HexHash = new Regex("^[0-9A-Fa-f]{64}$");
        private static readonly Regex SafeVersion = new Regex("^[0-9A-Za-z][0-9A-Za-z.\\-]{0,40}$");

        private static bool IsHttps(string url)
        {
            Uri u;
            return Uri.TryCreate(url, UriKind.Absolute, out u) && u.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>Reads the manifest text. Entries that look wrong are skipped instead of trusted.</summary>
        public static Manifest Parse(string text)
        {
            object root = Json.Parse(text);
            if (!(root is Dictionary<string, object>)) throw new FormatException("The manifest is not a JSON object.");

            var m = new Manifest();

            Dictionary<string, object> app = Json.Obj(root, "app");
            if (app != null)
            {
                Version v;
                string url = Json.Str(app, "url");
                string sha = Json.Str(app, "sha256");
                if (Version.TryParse(Json.Str(app, "version") ?? "", out v) && IsHttps(url) && sha != null && HexHash.IsMatch(sha))
                    m.App = new AppRelease { Version = v, Url = url, Sha256 = sha.ToUpperInvariant(), Size = (long)Json.Num(app, "size", 0) };
            }

            // a newer manifest format may describe patches differently: then only the program update is used
            if (Json.Num(root, "format", 1) > ManifestFormat) return m;

            List<object> patches = Json.Arr(root, "patches");
            if (patches == null) return m;

            foreach (object p in patches)
            {
                string game = Json.Str(p, "game");
                if (game == null || !SafeVersion.IsMatch(game)) continue;
                if (m.Patches.Exists(x => x.GameVersion == game)) continue;

                var set = new PatchSet
                {
                    GameVersion = game,
                    VeldridPrefix = Json.Str(p, "veldridPrefix") ?? "",
                    FrameworkPrefix = Json.Str(p, "frameworkPrefix") ?? "",
                    FromInternet = true
                };

                Dictionary<string, object> editions = Json.Obj(p, "editions");
                if (editions == null) continue;

                foreach (string edition in AllowedEditions)
                {
                    object list;
                    if (!editions.TryGetValue(edition, out list) || !(list is List<object>)) continue;

                    var files = new List<PatchFile>();
                    bool ok = true;
                    foreach (object f in (List<object>)list)
                    {
                        string name = Json.Str(f, "name");
                        string url = Json.Str(f, "url");
                        string sha = Json.Str(f, "sha256");
                        if (name == null || !IsAllowedFile(name) || !IsHttps(url) || sha == null || !HexHash.IsMatch(sha) || files.Exists(x => x.Name == name))
                        {
                            ok = false;
                            break;
                        }
                        files.Add(new PatchFile { Name = name, Url = url, Entry = Json.Str(f, "entry"), Sha256 = sha.ToUpperInvariant(), Size = (long)Json.Num(f, "size", 0) });
                    }

                    // every edition must replace ppy.Veldrid.dll; the online edition must not replace osu.Framework.dll
                    if (!ok || !files.Exists(x => x.Name == "ppy.Veldrid.dll")) continue;
                    if (edition == Patcher.EditionOnline && files.Exists(x => x.Name == "osu.Framework.dll")) continue;

                    set.Editions[edition] = files;
                    foreach (PatchFile pf in files) set.AddKnown(pf.Name, pf.Sha256);
                }

                // older builds of the fix for this game version, so an installed older build is still recognised
                List<object> older = Json.Arr(p, "knownHashes");
                if (older != null)
                {
                    foreach (object k in older)
                    {
                        string name = Json.Str(k, "name");
                        string sha = Json.Str(k, "sha256");
                        if (name != null && IsAllowedFile(name) && sha != null && HexHash.IsMatch(sha)) set.AddKnown(name, sha.ToUpperInvariant());
                    }
                }

                if (set.Editions.Count > 0) m.Patches.Add(set);
            }
            return m;
        }

        // ================================================================== patch files

        /// <summary>
        /// Downloads every missing file of the edition into the cache and checks it.
        /// progress(percent) is called from the worker thread. Throws IOException with a readable message on failure.
        /// </summary>
        public static void DownloadEdition(PatchSet set, string edition, Action<int> progress)
        {
            List<PatchFile> files = set.Files(edition);
            if (files == null) throw new IOException("No such edition.");

            var todo = files.FindAll(f => !Payload.IsLocal(f));
            if (todo.Count == 0)
            {
                if (progress != null) progress(100);
                return;
            }

            // one archive may contain several files: download each address once
            var urls = new List<string>();
            foreach (PatchFile f in todo)
                if (!urls.Contains(f.Url)) urls.Add(f.Url);

            var downloaded = new Dictionary<string, byte[]>();
            for (int u = 0; u < urls.Count; u++)
            {
                int index = u;
                downloaded[urls[u]] = Get(urls[u], MaxFileBytes, (got, total) =>
                {
                    if (progress == null) return;
                    double part = total > 0 ? (double)got / total : 0.5;
                    progress((int)Math.Min(99, (index + part) * 100 / urls.Count));
                });
            }

            foreach (PatchFile f in todo)
            {
                byte[] data = downloaded[f.Url];
                if (!string.IsNullOrEmpty(f.Entry)) data = FromZip(data, f.Entry, f.Name);

                if (!string.Equals(Payload.HashBytes(data), f.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(Loc.T("err.hash", f.Name));

                string path = Payload.CachePath(set.GameVersion, edition, f);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path + ".part", data);
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".part", path);
            }

            if (progress != null) progress(100);
        }

        /// <summary>Takes one file out of a .zip. The entry path is matched exactly first, then by its end (folder names may differ).</summary>
        private static byte[] FromZip(byte[] zip, string entryPath, string fileName)
        {
            using (var ms = new MemoryStream(zip))
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                string want = entryPath.Replace('\\', '/').TrimStart('/');
                ZipArchiveEntry found = null;
                foreach (ZipArchiveEntry e in archive.Entries)
                {
                    string n = e.FullName.Replace('\\', '/');
                    if (string.Equals(n, want, StringComparison.OrdinalIgnoreCase)) { found = e; break; }
                    if (found == null && n.EndsWith("/files/" + fileName, StringComparison.OrdinalIgnoreCase)) found = e;
                }
                if (found == null) throw new IOException(Loc.T("err.hash", fileName));
                if (found.Length > MaxFileBytes) throw new IOException("The file is too big.");

                using (Stream s = found.Open())
                using (var outMs = new MemoryStream())
                {
                    s.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
        }

        // ================================================================== self-update

        public static bool IsNewer(AppRelease app)
        {
            return app != null && app.Version > CurrentVersion;
        }

        /// <summary>Path of the running exe, read once at start (before an update renames the file).</summary>
        private static string ExePath
        {
            get
            {
                if (exePath == null) exePath = Process.GetCurrentProcess().MainModule.FileName;
                return exePath;
            }
        }

        private static string exePath;

        /// <summary>
        /// Downloads the new exe, checks it and swaps it with the running one:
        /// the running exe is renamed to *.old (Windows allows that) and the new file takes its name.
        /// The new version starts the next time the program is opened (or after Restart()).
        /// Throws UnauthorizedAccessException when the folder is protected, IOException on other problems.
        /// </summary>
        public static void InstallAppUpdate(AppRelease app, Action<int> progress)
        {
            byte[] data = Get(app.Url, MaxFileBytes, (got, total) =>
            {
                if (progress != null) progress(total > 0 ? (int)Math.Min(99, got * 100 / total) : 50);
            });

            ReplaceExe(ExePath, data, app.Sha256);
            if (progress != null) progress(100);
        }

        /// <summary>Checks the new exe and swaps it with the file at exe (the running program).</summary>
        internal static void ReplaceExe(string exe, byte[] data, string sha256)
        {
            string dir = Path.GetDirectoryName(exe);
            string name = Path.GetFileName(exe);

            if (!string.Equals(Payload.HashBytes(data), sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException(Loc.T("err.hash", name));

            // write next to the exe (same disk, so the renames below are instant)
            string fresh = Path.Combine(dir, name + ".new");
            File.WriteAllBytes(fresh, data);

            string old = Path.Combine(dir, name + ".old");
            try
            {
                if (File.Exists(old)) File.Delete(old);
            }
            catch (Exception)
            {
                old = Path.Combine(dir, name + "." + DateTime.UtcNow.Ticks + ".old");   // the old one is still locked
            }

            try
            {
                File.Move(exe, old);
            }
            catch (Exception)
            {
                try { File.Delete(fresh); } catch (Exception) { }
                throw;
            }

            try
            {
                File.Move(fresh, exe);
            }
            catch (Exception)
            {
                File.Move(old, exe);   // put the running version back
                try { File.Delete(fresh); } catch (Exception) { }
                throw;
            }
        }

        /// <summary>Starts the (new) exe and closes this one.</summary>
        public static void Restart()
        {
            Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true });
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>Removes the *.old / *.new files left by an update. Called at start; the old process may still be closing, so it retries.</summary>
        public static void CleanupAfterUpdate()
        {
            try
            {
                CleanupAfterUpdate(ExePath);
            }
            catch (Exception)
            {
                // leftovers are harmless
            }
        }

        internal static void CleanupAfterUpdate(string exe)
        {
            try
            {
                string dir = Path.GetDirectoryName(exe);
                string name = Path.GetFileName(exe);

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    bool left = false;
                    foreach (string f in Directory.GetFiles(dir, name + "*"))
                    {
                        // *.new only on the first pass: later an update of this session may be writing one
                        bool isOld = f.EndsWith(".old", StringComparison.OrdinalIgnoreCase);
                        bool isNew = attempt == 0 && f.EndsWith(".new", StringComparison.OrdinalIgnoreCase);
                        if (!isOld && !isNew) continue;
                        try { File.Delete(f); }
                        catch (Exception) { left = true; }
                    }
                    if (!left) return;
                    System.Threading.Thread.Sleep(1000);
                }
            }
            catch (Exception)
            {
                // leftovers are harmless
            }
        }
    }
}
