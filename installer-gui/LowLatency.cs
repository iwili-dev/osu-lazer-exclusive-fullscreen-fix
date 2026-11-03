using System;

namespace OsuLazerFix
{
    /// <summary>
    /// Low latency (experimental). The patched ppy.Veldrid.dll reads the user environment variable OSU_D3D11_FRAME_LATENCY when the game starts:
    /// 1 = only one frame may wait in the render queue while the game is in exclusive fullscreen (less input lag, FPS can be a little lower).
    /// Not set = the Windows default, like the original game. Needs the patch 2026.1005.0-3 or newer.
    /// </summary>
    internal static class LowLatency
    {
        public const string Variable = "OSU_D3D11_FRAME_LATENCY";

        public static bool IsOn()
        {
            try
            {
                int n;
                return int.TryParse(Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User), out n) && n > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Can take a moment: Windows tells every open window that the environment changed.</summary>
        public static void Set(bool on)
        {
            Environment.SetEnvironmentVariable(Variable, on ? "1" : null, EnvironmentVariableTarget.User);
        }
    }
}
