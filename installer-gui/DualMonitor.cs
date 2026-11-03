using System;
using System.Collections.Generic;
using System.IO;

namespace OsuLazerFix
{
    /// <summary>
    /// Two-monitor mode (experimental). The patched ppy.Veldrid.dll reads the user environment variable
    /// OSU_D3D11_KEEP_ON_OTHER_MONITOR=1 when the game starts and then stays in exclusive fullscreen while a window
    /// on another monitor is active. It only does that while the game setting "Minimise osu! when switching to another app"
    /// (MinimiseOnFocusLossInFullscreen in %AppData%\osu\framework.ini) is off, so turning the mode on also turns that setting off.
    /// Turning the mode off puts the setting back only if this program changed it.
    /// </summary>
    internal static class DualMonitor
    {
        public const string Variable = "OSU_D3D11_KEEP_ON_OTHER_MONITOR";
        private const string MinimiseKey = "MinimiseOnFocusLossInFullscreen";

        private static string FrameworkIni
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "osu", "framework.ini"); }
        }

        public static bool IsOn()
        {
            try
            {
                return Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User) == "1";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Can take a moment: Windows tells every open window that the environment changed.</summary>
        public static void Set(bool on)
        {
            if (on)
            {
                if (SetMinimise(false))
                {
                    Settings.DualChangedMinimise = true;
                    Settings.Save();
                }

                Environment.SetEnvironmentVariable(Variable, "1", EnvironmentVariableTarget.User);
            }
            else
            {
                Environment.SetEnvironmentVariable(Variable, null, EnvironmentVariableTarget.User);

                if (Settings.DualChangedMinimise)
                {
                    SetMinimise(true);
                    Settings.DualChangedMinimise = false;
                    Settings.Save();
                }
            }
        }

        /// <summary>Writes the game setting. Returns true if the file was changed.</summary>
        private static bool SetMinimise(bool value)
        {
            string path = FrameworkIni;
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) return false;   // the game was never started with the default data folder

            string want = value ? "True" : "False";
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            bool found = false, changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq <= 0 || lines[i].Substring(0, eq).Trim() != MinimiseKey) continue;

                found = true;
                if (!string.Equals(lines[i].Substring(eq + 1).Trim(), want, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = MinimiseKey + " = " + want;
                    changed = true;
                }
            }

            if (!found)
            {
                lines.Add(MinimiseKey + " = " + want);
                changed = true;
            }

            if (changed) File.WriteAllLines(path, lines);
            return changed;
        }
    }
}
