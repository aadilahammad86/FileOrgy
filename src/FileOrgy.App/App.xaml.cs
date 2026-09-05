using System;
using System.Linq;
using System.Windows;
using WpfApp = System.Windows.Application;

namespace FileOrgy.App
{
    public partial class App : WpfApp
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool startInTray = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                                               a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

            if (startInTray)
            {
                MainWindow?.Hide();
            }
        }
    }
}
