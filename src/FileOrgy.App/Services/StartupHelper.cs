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
        private const string ElevatedTaskName = "FileOrgy_ElevatedAutoStart";

        public static bool IsStartupEnabled()
        {
            try
            {
                // 1. Check if elevated autostart task exists in Task Scheduler
                if (IsTaskSchedulerAutostartEnabled())
                {
                    return true;
                }

                // 2. Check standard registry Run key in HKCU
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

        private static bool IsTaskSchedulerAutostartEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/query /tn \"{ElevatedTaskName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(1500);
                    return proc.ExitCode == 0;
                }
            }
            catch { }
            return false;
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                string? exePath = GetBestExecutablePath();

                if (enable)
                {
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        bool requiresElevation = IsElevatedOrFlagged(exePath);

                        if (requiresElevation)
                        {
                            // Elevated apps must launch via Task Scheduler (/RL HIGHEST)
                            // because Windows logon silently drops elevated apps from Run keys.
                            RegisterElevatedTask(exePath);

                            // Clean any standard Run keys to prevent conflict
                            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                            key?.DeleteValue(AppName, false);
                        }
                        else
                        {
                            // Standard apps use standard HKCU Run key
                            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                            key?.SetValue(AppName, $"\"{exePath}\" --tray");

                            // Clean scheduled task if switching to standard
                            DeleteElevatedTask();
                        }
                    }
                }
                else
                {
                    // Disable both registry and Task Scheduler
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                    key?.DeleteValue(AppName, false);

                    DeleteElevatedTask();
                }
            }
            catch
            {
                // Silently ignore permissions exceptions
            }
        }

        private static bool IsElevatedOrFlagged(string exePath)
        {
            try
            {
                // Check if executable is flagged with ~ RUNASADMIN in HKCU or HKLM
                using var cuLayers = Registry.CurrentUser.OpenSubKey(LayersKeyPath, false);
                if (cuLayers?.GetValue(exePath)?.ToString()?.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }

                using var lmLayers = Registry.LocalMachine.OpenSubKey(LayersKeyPath, false);
                if (lmLayers?.GetValue(exePath)?.ToString()?.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static void RegisterElevatedTask(string exePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{ElevatedTaskName}\" /tr \"\\\"{exePath}\\\" --tray\" /sc onlogon /rl HIGHEST /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch { }
        }

        private static void DeleteElevatedTask()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{ElevatedTaskName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch { }
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
