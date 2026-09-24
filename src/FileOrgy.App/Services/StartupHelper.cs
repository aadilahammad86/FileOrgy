using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FileOrgy.App.Services
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string LayersKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string AppName = "FileOrgy";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                var val = key?.GetValue(AppName)?.ToString();
                if (string.IsNullOrWhiteSpace(val))
                {
                    // Also check HKLM in case machine-wide installer registered it
                    using var lmKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, false);
                    val = lmKey?.GetValue(AppName)?.ToString();
                }

                if (string.IsNullOrWhiteSpace(val)) return false;

                // Extract path and verify file actually exists
                string cleanPath = val.Trim().Trim('\"');
                int exeIdx = cleanPath.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIdx > 0)
                {
                    cleanPath = cleanPath.Substring(0, exeIdx + 4).Trim('\"');
                }

                return File.Exists(cleanPath);
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = GetBestExecutablePath();
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\" --tray");

                        // Remove ~ RUNASADMIN from HKCU layers if present, as it blocks Windows startup
                        try
                        {
                            using var layersKey = Registry.CurrentUser.OpenSubKey(LayersKeyPath, true);
                            layersKey?.DeleteValue(exePath, false);
                        }
                        catch { }
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch
            {
                // Silently ignore registry permissions exceptions
            }
        }

        private static string? GetBestExecutablePath()
        {
            // If running from Program Files or installed location, use that
            string? currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                // If not running from bin/Debug or tests, current is best
                if (!currentExe.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase) &&
                    !currentExe.Contains(@"\tests\", StringComparison.OrdinalIgnoreCase))
                {
                    return currentExe;
                }
            }

            // Fallback to standard installed location
            string standardInstallPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "FileOrgy",
                "FileOrgy.exe");

            if (File.Exists(standardInstallPath))
            {
                return standardInstallPath;
            }

            return currentExe;
        }
    }
}
