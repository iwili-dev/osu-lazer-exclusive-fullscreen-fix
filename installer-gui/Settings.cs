using System;
using System.IO;

namespace OsuLazerFix
{
    /// <summary>Remembers the language, the last chosen game folder and a few switches.</summary>
    internal static class Settings
    {
        public static string Folder = "";
        public static bool AutoUpdate = true;
        public static bool DualChangedMinimise;   // two-monitor mode turned off "Minimise osu! when switching to another app"
        public static bool Animations = true;     // moving background and smooth transitions

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
                // without a saved choice, follow "Animation effects" in the Windows settings
                Animations = System.Windows.SystemParameters.ClientAreaAnimation;
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
                    else if (key == "DualChangedMinimise") DualChangedMinimise = val == "1";
                    else if (key == "Animations") Animations = val != "0";
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
                File.WriteAllLines(FilePath, new[] { "Lang=" + (Loc.Lang == 1 ? "ru" : "en"), "Folder=" + Folder, "AutoUpdate=" + (AutoUpdate ? "1" : "0"), "DualChangedMinimise=" + (DualChangedMinimise ? "1" : "0"), "Animations=" + (Animations ? "1" : "0") });
            }
            catch (Exception)
            {
                // settings are optional
            }
        }
    }
}
