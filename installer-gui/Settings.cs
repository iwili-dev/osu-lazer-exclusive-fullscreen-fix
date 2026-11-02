using System;
using System.IO;

namespace OsuLazerFix
{
    /// <summary>Remembers the language and the last chosen game folder.</summary>
    internal static class Settings
    {
        public static string Folder = "";
        public static bool AutoUpdate = true;

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu-lazer-fix");
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "Lang") Loc.Lang = val == "ru" ? 1 : 0;
                    else if (key == "Folder") Folder = val;
                    else if (key == "AutoUpdate") AutoUpdate = val != "0";
                }
            }
            catch (Exception)
            {
                // settings are optional
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new[] { "Lang=" + (Loc.Lang == 1 ? "ru" : "en"), "Folder=" + Folder, "AutoUpdate=" + (AutoUpdate ? "1" : "0") });
            }
            catch (Exception)
            {
                // settings are optional
            }
        }
    }
}
