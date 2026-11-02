using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace OsuLazerFix
{
    /// <summary>One file of a patch. It comes from inside the exe (Resource) or from the internet (Url).</summary>
    internal sealed class PatchFile
    {
        public string Name;       // ppy.Veldrid.dll or osu.Framework.dll
        public string Sha256;     // upper-case hex
        public string Resource;   // embedded resource name, or null
        public string Url;        // download address (a .dll or a .zip), or null
        public string Entry;      // path of the file inside the .zip, or null when Url is the file itself
        public long Size;
    }

    /// <summary>One game version the program knows how to patch, with its editions and files.</summary>
    internal sealed class PatchSet
    {
        public string GameVersion;
        public string VeldridPrefix = "";
        public string FrameworkPrefix = "";
        public bool FromInternet;   // at least one edition comes from the online list

        /// <summary>edition name -> files that edition replaces</summary>
        public readonly Dictionary<string, List<PatchFile>> Editions = new Dictionary<string, List<PatchFile>>();

        /// <summary>file name -> SHA-256 of every build of the fix for this game version (embedded and online), to recognise an installed fix.</summary>
        public readonly Dictionary<string, HashSet<string>> Known = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public void AddKnown(string file, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return;
            HashSet<string> set;
            if (!Known.TryGetValue(file, out set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Known[file] = set;
            }
            set.Add(hash);
        }

        public bool IsKnown(string file, string hash)
        {
            HashSet<string> set;
            return hash != null && Known.TryGetValue(file, out set) && set.Contains(hash);
        }

        public List<PatchFile> Files(string edition)
        {
            List<PatchFile> files;
            return Editions.TryGetValue(edition, out files) ? files : null;
        }

        /// <summary>True if some file of the edition still has to be downloaded.</summary>
        public bool NeedsDownload(string edition)
        {
            List<PatchFile> files = Files(edition);
            return files != null && files.Any(f => !Payload.IsLocal(f));
        }
    }

    /// <summary>
    /// The list of patches: the ones embedded into this exe (payload folder) plus the online list (update/manifest.json on GitHub).
    /// Online files are downloaded into %LOCALAPPDATA%\osu-lazer-fix\cache and checked by SHA-256 every time they are used.
    /// </summary>
    internal static class Payload
    {
        private static List<PatchSet> embedded;
        private static List<PatchSet> online = new List<PatchSet>();
        private static List<PatchSet> merged;

        public static string CacheRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu-lazer-fix", "cache"); }
        }

        public static List<PatchSet> Sets
        {
            get
            {
                if (merged == null) merged = Merge();
                return merged;
            }
        }

        public static PatchSet Find(string gameVersion)
        {
            foreach (PatchSet s in Sets)
                if (s.GameVersion == gameVersion) return s;
            return null;
        }

        /// <summary>Replaces the online part of the list (called with the sets from the manifest).</summary>
        public static void SetOnline(List<PatchSet> sets)
        {
            online = sets ?? new List<PatchSet>();
            merged = null;
        }

        // ------------------------------------------------------------ merge

        private static List<PatchSet> Merge()
        {
            if (embedded == null) embedded = LoadEmbedded();

            var result = new List<PatchSet>();
            var versions = embedded.Select(s => s.GameVersion).Concat(online.Select(s => s.GameVersion)).Distinct().ToList();

            foreach (string v in versions)
            {
                PatchSet e = embedded.FirstOrDefault(s => s.GameVersion == v);
                PatchSet o = online.FirstOrDefault(s => s.GameVersion == v);
                var set = new PatchSet { GameVersion = v };

                foreach (PatchSet src in new[] { e, o })
                {
                    if (src == null) continue;
                    if (!string.IsNullOrEmpty(src.VeldridPrefix)) set.VeldridPrefix = src.VeldridPrefix;
                    if (!string.IsNullOrEmpty(src.FrameworkPrefix)) set.FrameworkPrefix = src.FrameworkPrefix;
                    foreach (var kv in src.Known)
                        foreach (string h in kv.Value) set.AddKnown(kv.Key, h);
                }

                // embedded editions first; the online list wins for an edition it also has (it may be a newer build)
                if (e != null)
                    foreach (var kv in e.Editions) set.Editions[kv.Key] = kv.Value;

                if (o != null)
                {
                    foreach (var kv in o.Editions)
                    {
                        var files = new List<PatchFile>();
                        foreach (PatchFile f in kv.Value)
                        {
                            // the same file is already inside the exe: no need to download it
                            PatchFile same = e == null ? null : e.Editions.Values.SelectMany(x => x)
                                .FirstOrDefault(x => x.Name == f.Name && string.Equals(x.Sha256, f.Sha256, StringComparison.OrdinalIgnoreCase));
                            files.Add(new PatchFile { Name = f.Name, Sha256 = f.Sha256, Url = f.Url, Entry = f.Entry, Size = f.Size, Resource = same == null ? null : same.Resource });
                        }
                        set.Editions[kv.Key] = files;
                    }
                    set.FromInternet = true;
                }

                result.Add(set);
            }
            return result;
        }

        // ------------------------------------------------------------ embedded files

        private static List<PatchSet> LoadEmbedded()
        {
            var sets = new List<PatchSet>();
            Assembly asm = Assembly.GetExecutingAssembly();
            var byVersion = new Dictionary<string, PatchSet>();

            foreach (string name in asm.GetManifestResourceNames())
            {
                string n = name.Replace('\\', '/');
                if (!n.StartsWith("payload/", StringComparison.Ordinal)) continue;

                string[] parts = n.Split('/');
                if (parts.Length < 3) continue;

                PatchSet set;
                if (!byVersion.TryGetValue(parts[1], out set))
                {
                    set = new PatchSet { GameVersion = parts[1] };
                    byVersion[parts[1]] = set;
                    sets.Add(set);
                }

                if (parts.Length == 3 && parts[2].Equals("patch.ini", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] ini = ReadResource(name);
                    string text = ini == null ? "" : System.Text.Encoding.UTF8.GetString(ini).TrimStart('\uFEFF');
                    foreach (string line in text.Split('\n'))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();
                        if (key == "VeldridPrefix") set.VeldridPrefix = val;
                        else if (key == "FrameworkPrefix") set.FrameworkPrefix = val;
                    }
                }
                else if (parts.Length == 4 && Online.IsAllowedFile(parts[3]))
                {
                    byte[] data = ReadResource(name);
                    if (data == null) continue;

                    List<PatchFile> files;
                    if (!set.Editions.TryGetValue(parts[2], out files))
                    {
                        files = new List<PatchFile>();
                        set.Editions[parts[2]] = files;
                    }
                    var pf = new PatchFile { Name = parts[3], Resource = name, Sha256 = HashBytes(data), Size = data.Length };
                    files.Add(pf);
                    set.AddKnown(pf.Name, pf.Sha256);
                }
            }

            // a version folder with only patch.ini and no DLLs is not a usable patch
            sets.RemoveAll(s => s.Editions.Count == 0);
            return sets;
        }

        private static byte[] ReadResource(string resourceName)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (s == null) return null;
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        // ------------------------------------------------------------ reading files

        public static string CachePath(string gameVersion, string edition, PatchFile f)
        {
            return Path.Combine(CacheRoot, gameVersion, edition, f.Name);
        }

        /// <summary>True if the file is inside the exe or already downloaded (and its SHA-256 is right).</summary>
        public static bool IsLocal(PatchFile f)
        {
            if (f.Resource != null) return true;
            return FindCached(f) != null;
        }

        private static string FindCached(PatchFile f)
        {
            // the same DLL may have been downloaded for another edition or game version: any copy with the right hash is fine
            try
            {
                if (!Directory.Exists(CacheRoot)) return null;
                foreach (string path in Directory.GetFiles(CacheRoot, f.Name, SearchOption.AllDirectories))
                {
                    var info = new FileInfo(path);
                    if (f.Size > 0 && info.Length != f.Size) continue;
                    if (string.Equals(CachedHash(path, info), f.Sha256, StringComparison.OrdinalIgnoreCase)) return path;
                }
            }
            catch (Exception)
            {
                // a broken cache only means the file is downloaded again
            }
            return null;
        }

        private static readonly Dictionary<string, string> hashMemo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>SHA-256 of a cached file, remembered while its size and time stay the same (the list is redrawn often).</summary>
        private static string CachedHash(string path, FileInfo info)
        {
            string key = path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            string h;
            lock (hashMemo)
                if (hashMemo.TryGetValue(key, out h)) return h;
            h = HashFile(path);
            lock (hashMemo) hashMemo[key] = h;
            return h;
        }

        /// <summary>Bytes of a patch file, checked by SHA-256. Null if it is not available locally.</summary>
        public static byte[] Read(PatchFile f)
        {
            byte[] data = null;
            if (f.Resource != null) data = ReadResource(f.Resource);
            if (data == null)
            {
                string path = FindCached(f);
                if (path != null)
                {
                    try { data = File.ReadAllBytes(path); }
                    catch (IOException) { data = null; }
                }
            }
            if (data == null) return null;
            return string.Equals(HashBytes(data), f.Sha256, StringComparison.OrdinalIgnoreCase) ? data : null;
        }

        // ------------------------------------------------------------ hashes

        public static string HashBytes(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
        }

        public static string HashFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
        }
    }
}
