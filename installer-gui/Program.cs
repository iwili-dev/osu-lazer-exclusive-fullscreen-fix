using System;

namespace OsuLazerFix
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // remove the *.old exe left by a self-update (the old process may still be closing)
            System.Threading.Tasks.Task.Run(() => Online.CleanupAfterUpdate());

            var app = new System.Windows.Application();
            app.Run(new MainWindow());
        }
    }
}
