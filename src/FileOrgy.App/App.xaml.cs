using System;
using System.Linq;
using System.Windows;
using WpfApp = System.Windows.Application;

namespace FileOrgy.App
{
    public partial class App : WpfApp
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                string details = FormatExceptionDetails(e.Exception);
                System.Windows.MessageBox.Show(
                    $"FileOrgy Error:\n\n{details}",
                    "FileOrgy Exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    string details = FormatExceptionDetails(ex);
                    System.Windows.MessageBox.Show(
                        $"FileOrgy Fatal Error:\n\n{details}",
                        "FileOrgy Fatal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
        }

        private static string FormatExceptionDetails(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            var current = ex;
            int depth = 0;
            while (current != null)
            {
                if (depth > 0)
                {
                    sb.AppendLine($"\n--- Inner Exception ({depth}) ---");
                }
                sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
                current = current.InnerException;
                depth++;
            }
            sb.AppendLine("\n--- Stack Trace ---");
            sb.AppendLine(ex.StackTrace);
            return sb.ToString();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            bool startInTray = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                                               a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

            string? scanTarget = null;
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i].Equals("--scan", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    scanTarget = e.Args[i + 1];
                    break;
                }
            }

            if (!string.IsNullOrEmpty(scanTarget))
            {
                mainWindow.Show();
                mainWindow.Activate();
                _ = mainWindow.ScanDirectoryAsync(scanTarget);
            }
            else if (!startInTray)
            {
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
            }
        }
    }
}
