using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FileOrgy.App.Services
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "FileOrgy";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
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
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\" --tray");
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
    }
}
