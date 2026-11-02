// Author tool for the online list of patches (update/manifest.json) and the program updates.
// Needs the .NET 10 SDK (the same one used to build osu). Run it through the .bat files next to it:
//   publish-patch.bat  - after make-fix.bat: makes the two release zips and adds the patch to the manifest
//   publish-app.bat    - after installer-gui\build.bat: puts the new osu-lazer-fix.exe into the manifest
//   check-manifest.bat - downloads everything the manifest points to and checks the SHA-256 (run after uploading)
// The current folder must be the repository root (the .bat files do that).

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string Repo = "iwili-dev/osu-lazer-exclusive-fullscreen-fix";
const string ManifestPath = "update/manifest.json";

try
{
    string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
    string? tagArg = args.Length > 1 && args[1].Trim().Length > 0 ? args[1].Trim() : null;
    switch (cmd)
    {
        case "patch": return Patch(tagArg);
        case "app": return App(tagArg);
        case "check": return await Check();
        default:
            Console.WriteLine("Usage: manifest-tool patch [tag] | app <tag> | check");
            return 2;
    }
}
catch (ToolError e)
{
    Console.WriteLine();
    Console.WriteLine("ERROR: " + e.Message);
    return 1;
}

// ====================================================================== patch

int Patch(string? tag)
{
    string onDir = "osu-fix-online", offDir = "osu-fix-partial-offline";
    foreach (string f in new[] { onDir + "/files/ppy.Veldrid.dll", offDir + "/files/ppy.Veldrid.dll", offDir + "/files/osu.Framework.dll" })
        if (!File.Exists(f)) throw new ToolError(f + " was not found. Run author-tools\\make-fix.bat first.");

    // the game version and the official-file prefixes are taken from apply.bat, so they always match the zips
    string onBat = File.ReadAllText(onDir + "/apply.bat"), offBat = File.ReadAllText(offDir + "/apply.bat");
    string gv = BatVar(onBat, "FIXVER") ?? throw new ToolError("FIXVER not found in " + onDir + "/apply.bat");
    if (BatVar(offBat, "FIXVER") != gv) throw new ToolError("FIXVER differs between the two apply.bat files. Fix them first.");
    if (!Regex.IsMatch(gv, "^[0-9A-Za-z][0-9A-Za-z.\\-]{0,40}$")) throw new ToolError("Strange game version: " + gv);

    var onPrefixes = BatVars(onBat, "OSU_FIX_PREFIX");
    var offPrefixes = BatVars(offBat, "OSU_FIX_PREFIX");
    string veldridPrefix = onPrefixes.FirstOrDefault() ?? throw new ToolError("OSU_FIX_PREFIX not found in " + onDir + "/apply.bat");
    string frameworkPrefix = offPrefixes.FirstOrDefault(p => p != veldridPrefix) ?? throw new ToolError("The osu.Framework prefix was not found in " + offDir + "/apply.bat");

    JsonObject manifest = LoadManifest();
    JsonArray patches = manifest["patches"] as JsonArray ?? new JsonArray();
    manifest["patches"] = patches;
    JsonObject? old = patches.OfType<JsonObject>().FirstOrDefault(p => (string?)p["game"] == gv);

    if (tag == null)
    {
        // the next free tag for this game version: 2026.1005.0-1, -2, ...
        int n = 1;
        string? oldTag = old == null ? null : (string?)old["release"];
        var m = oldTag == null ? null : Regex.Match(oldTag, "-(\\d+)$");
        if (m != null && m.Success) n = int.Parse(m.Groups[1].Value) + 1;
        tag = gv + "-" + n;
    }
    CheckTag(tag);

    string outDir = Path.Combine("release", tag);
    Directory.CreateDirectory(outDir);
    string onZip = $"osu-lazer-fix-{gv}-online.zip", offZip = $"osu-lazer-fix-{gv}-partial-offline.zip";
    MakeZip(onDir, Path.Combine(outDir, onZip));
    MakeZip(offDir, Path.Combine(outDir, offZip));

    string Url(string asset) => $"https://github.com/{Repo}/releases/download/{tag}/{asset}";
    JsonObject FileNode(string dir, string zip, string name)
    {
        byte[] data = File.ReadAllBytes(dir + "/files/" + name);
        return new JsonObject
        {
            ["name"] = name,
            ["url"] = Url(zip),
            ["entry"] = dir + "/files/" + name,
            ["sha256"] = Sha(data),
            ["size"] = data.Length
        };
    }

    var entry = new JsonObject
    {
        ["game"] = gv,
        ["release"] = tag,
        ["veldridPrefix"] = veldridPrefix,
        ["frameworkPrefix"] = frameworkPrefix,
        ["editions"] = new JsonObject
        {
            ["online"] = new JsonArray(FileNode(onDir, onZip, "ppy.Veldrid.dll")),
            ["partial-offline"] = new JsonArray(FileNode(offDir, offZip, "ppy.Veldrid.dll"), FileNode(offDir, offZip, "osu.Framework.dll"))
        }
    };

    // keep the hashes of the previous build of this game version, so people who installed it still see "installed"
    var known = new JsonArray();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (JsonObject f in AllFiles(entry)) seen.Add((string)f["sha256"]!);
    if (old != null)
    {
        foreach (JsonObject f in AllFiles(old).Concat((old["knownHashes"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()))
        {
            string sha = (string)f["sha256"]!;
            if (seen.Add(sha)) known.Add((JsonNode)new JsonObject { ["name"] = (string)f["name"]!, ["sha256"] = sha });
        }
        patches.Remove(old);
    }
    if (known.Count > 0) entry["knownHashes"] = known;

    patches.Insert(0, entry);   // newest game version first
    SaveManifest(manifest);

    string zipHashes = $"{Sha(File.ReadAllBytes(Path.Combine(outDir, onZip)))}  {onZip}\n{Sha(File.ReadAllBytes(Path.Combine(outDir, offZip)))}  {offZip}\n";
    File.WriteAllText(Path.Combine(outDir, "SHA256.txt"), zipHashes);

    Console.WriteLine();
    Console.WriteLine($"Game version : {gv}");
    Console.WriteLine($"Release tag  : {tag}");
    Console.WriteLine($"Prefixes     : Veldrid {veldridPrefix}, Framework {frameworkPrefix}");
    Console.WriteLine($"Files        : {Path.GetFullPath(outDir)}");
    Console.WriteLine();
    Console.WriteLine("SHA256 for the release notes:");
    Console.Write(zipHashes);
    Console.WriteLine();
    Console.WriteLine("Next:");
    Console.WriteLine($"  1. GitHub - Releases - Draft a new release. Tag: {tag}");
    Console.WriteLine($"  2. Upload BOTH zips from {outDir} and publish the release.");
    Console.WriteLine("  3. Run check-manifest.bat (it downloads the files and checks them).");
    Console.WriteLine($"  4. Commit and push {ManifestPath}. The program shows the new patch after that.");
    return 0;
}

// ====================================================================== app

int App(string? tag)
{
    if (tag == null) throw new ToolError("Give the release tag the exe will be uploaded to, for example: publish-app.bat app-1.0.1");
    CheckTag(tag);

    string exe = "installer-gui/osu-lazer-fix.exe";
    if (!File.Exists(exe)) throw new ToolError(exe + " was not found. Run installer-gui\\build.bat first.");

    Version v = AssemblyName.GetAssemblyName(Path.GetFullPath(exe)).Version ?? throw new ToolError("Couldn't read the exe version.");
    string version = v.ToString(3);

    JsonObject manifest = LoadManifest();
    if (manifest["app"] is JsonObject oldApp && Version.TryParse((string?)oldApp["version"], out Version? oldV) && oldV >= new Version(v.Major, v.Minor, Math.Max(0, v.Build)))
        throw new ToolError($"The exe is version {version}, the manifest already has {oldV}. Raise <Version> in installer-gui\\OsuLazerFix.csproj, run build.bat again, then run this again.");

    string outDir = Path.Combine("release", tag);
    Directory.CreateDirectory(outDir);
    string target = Path.Combine(outDir, "osu-lazer-fix.exe");
    File.Copy(exe, target, true);
    byte[] data = File.ReadAllBytes(target);

    manifest["app"] = new JsonObject
    {
        ["version"] = version,
        ["url"] = $"https://github.com/{Repo}/releases/download/{tag}/osu-lazer-fix.exe",
        ["sha256"] = Sha(data),
        ["size"] = data.Length
    };
    SaveManifest(manifest);

    Console.WriteLine();
    Console.WriteLine($"Program version : {version}");
    Console.WriteLine($"Release tag     : {tag}");
    Console.WriteLine($"SHA256          : {Sha(data)}  osu-lazer-fix.exe");
    Console.WriteLine();
    Console.WriteLine("Next:");
    Console.WriteLine($"  1. GitHub - Releases - Draft a new release (or edit one). Tag: {tag}");
    Console.WriteLine($"  2. Upload {target} as osu-lazer-fix.exe (keep this exact file name) and publish.");
    Console.WriteLine("  3. Run check-manifest.bat.");
    Console.WriteLine($"  4. Commit and push {ManifestPath}. Programs with Auto-update on update themselves at the next start.");
    return 0;
}

// ====================================================================== check

async Task<int> Check()
{
    JsonObject manifest = LoadManifest();
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("osu-lazer-fix-author-tool");
    var cache = new Dictionary<string, byte[]>();
    int bad = 0;

    async Task<byte[]?> Get(string url)
    {
        if (cache.TryGetValue(url, out var d)) return d;
        try { d = await http.GetByteArrayAsync(url); }
        catch (Exception e) { Console.WriteLine($"  FAIL download {url}: {e.Message}"); return null; }
        cache[url] = d;
        return d;
    }

    if (manifest["app"] is JsonObject app)
    {
        Console.WriteLine($"Program {app["version"]}");
        byte[]? d = await Get((string)app["url"]!);
        if (d == null) bad++;
        else if (Sha(d) != ((string)app["sha256"]!).ToUpperInvariant()) { Console.WriteLine("  FAIL sha256 of the exe"); bad++; }
        else Console.WriteLine("  ok");
    }

    foreach (JsonObject p in (manifest["patches"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
    {
        Console.WriteLine($"Game {p["game"]} ({p["release"]})");
        foreach (var (edition, f) in Editions(p))
        {
            byte[]? d = await Get((string)f["url"]!);
            if (d == null) { bad++; continue; }
            string? entry = (string?)f["entry"];
            if (!string.IsNullOrEmpty(entry))
            {
                using var zip = new ZipArchive(new MemoryStream(d));
                var e = zip.GetEntry(entry);
                if (e == null) { Console.WriteLine($"  FAIL {edition}: {entry} is not in the zip"); bad++; continue; }
                using var ms = new MemoryStream();
                using (var s = e.Open()) s.CopyTo(ms);
                d = ms.ToArray();
            }
            bool ok = Sha(d) == ((string)f["sha256"]!).ToUpperInvariant();
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {edition} {f["name"]}");
            if (!ok) bad++;
        }
    }

    Console.WriteLine();
    Console.WriteLine(bad == 0 ? "Everything is fine. You can push update/manifest.json." : $"{bad} problem(s). Do NOT push update/manifest.json until they are fixed.");
    return bad == 0 ? 0 : 1;
}

// ====================================================================== helpers

static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

static string? BatVar(string bat, string name) => BatVars(bat, name).FirstOrDefault();

static List<string> BatVars(string bat, string name) =>
    Regex.Matches(bat, "^\\s*set \"" + Regex.Escape(name) + "=([^\"]*)\"", RegexOptions.Multiline | RegexOptions.IgnoreCase)
         .Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();

static void CheckTag(string tag)
{
    if (!Regex.IsMatch(tag, "^[0-9A-Za-z][0-9A-Za-z.\\-_]{0,60}$")) throw new ToolError("A release tag may only have letters, digits, '.', '-' and '_': " + tag);
}

static IEnumerable<(string, JsonObject)> Editions(JsonObject p)
{
    if (p["editions"] is not JsonObject eds) yield break;
    foreach (var kv in eds)
        foreach (JsonObject f in (kv.Value as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            yield return (kv.Key, f);
}

static IEnumerable<JsonObject> AllFiles(JsonObject p) => Editions(p).Select(x => x.Item2);

static void MakeZip(string folder, string zipPath)
{
    if (File.Exists(zipPath)) File.Delete(zipPath);
    using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
    {
        string rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
        string name = Path.GetFileName(file).ToLowerInvariant();
        // files that apply.bat writes next to itself on the author's PC are not part of the release
        if (name.StartsWith("location-") || name.EndsWith(".log")) continue;
        zip.CreateEntryFromFile(file, Path.GetFileName(folder) + "/" + rel, CompressionLevel.Optimal);
    }
}

static JsonObject LoadManifest()
{
    if (!File.Exists(ManifestPath)) return new JsonObject { ["format"] = 1, ["patches"] = new JsonArray() };
    return JsonNode.Parse(File.ReadAllText(ManifestPath)) as JsonObject ?? throw new ToolError(ManifestPath + " is not a JSON object.");
}

static void SaveManifest(JsonObject m)
{
    m["format"] = 1;
    m["updated"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
    // fixed order of the top-level keys, easier to read in diffs
    var ordered = new JsonObject();
    foreach (string k in new[] { "format", "updated", "app", "patches" })
        if (m[k] != null) ordered[k] = m[k]!.DeepClone();
    foreach (var kv in m)
        if (ordered[kv.Key] == null && kv.Value != null) ordered[kv.Key] = kv.Value.DeepClone();

    Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
    var opt = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    File.WriteAllText(ManifestPath, ordered.ToJsonString(opt) + "\n", new UTF8Encoding(false));
    Console.WriteLine("Updated " + ManifestPath);
}

sealed class ToolError(string message) : Exception(message);
