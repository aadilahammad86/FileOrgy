using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using FileOrgy.Core.Utils;
using Microsoft.Win32;

namespace FileOrgy.App.Services
{
    public enum StartupMode
    {
        Disabled,
        RegistryCurrentUser,
        TaskSchedulerElevated,
        BlockedOrForeignTask
    }

    public sealed class StartupStatusInfo
    {
        public StartupMode Mode { get; set; } = StartupMode.Disabled;
        public bool IsEnabled { get; set; }
        public bool IsCurrentProcessElevated { get; set; }
        public bool HasRunAsAdminFlag { get; set; }
        public bool HasHkcuRunAsAdminFlag { get; set; }
        public bool HasHklmRunAsAdminFlag { get; set; }
        public bool HasHkcuRunEntry { get; set; }
        public bool HasHklmRunEntry { get; set; }
        public bool IsDisabledInTaskManager { get; set; }
        public bool HasScheduledTask { get; set; }
        public bool IsScheduledTaskForeignOrInaccessible { get; set; }
        public string? ScheduledTaskPrincipal { get; set; }
        public string? ScheduledTaskCommand { get; set; }
        public string? ScheduledTaskStatus { get; set; }
        public string? RegistryCommand { get; set; }
        public string CurrentWindowsUser { get; set; } = string.Empty;
        public string TargetExecutablePath { get; set; } = string.Empty;
        public bool VisibleInTaskManagerStartupApps { get; set; }
        public bool HasDiagnosticWarning { get; set; }
        public string StatusTitle { get; set; } = string.Empty;
        public string DiagnosticDetail { get; set; } = string.Empty;
    }

    public sealed class StartupOperationResult
    {
        public bool Success { get; set; }
        public bool CancelledByUser { get; set; }
        public string Message { get; set; } = string.Empty;
        public StartupStatusInfo? Status { get; set; }
    }

    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupApprovedRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string LayersKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string AppName = "FileOrgy";
        private const string ElevatedTaskName = "FileOrgy_ElevatedAutoStart";

        public static string? ParseSchtasksPrincipal(string? output) =>
            StartupDiagnosticsParser.ParseSchtasksPrincipal(output);

        public static string? ParseSchtasksTaskToRun(string? output) =>
            StartupDiagnosticsParser.ParseSchtasksTaskToRun(output);

        public static bool IsAccessDeniedMessage(string? message) =>
            StartupDiagnosticsParser.IsAccessDeniedMessage(message);

        public static bool IsStartupApprovedDisabled(byte[]? data) =>
            StartupDiagnosticsParser.IsStartupApprovedDisabled(data);

        public static string? ExtractExePathFromCommand(string? command) =>
            StartupDiagnosticsParser.ExtractExePathFromCommand(command);

        public static bool IsStartupEnabled()
        {
            return GetStartupStatus().IsEnabled;
        }

        public static void SetStartup(bool enable)
        {
            var status = GetStartupStatus();
            bool preferElevated = status.HasRunAsAdminFlag && status.Mode != StartupMode.RegistryCurrentUser;
            ApplyStartupConfiguration(enable, preferElevated, startMinimized: true);
        }

        public static StartupStatusInfo GetStartupStatus()
        {
            string currentUser = GetCurrentWindowsUserName();
            bool isProcessElevated = IsCurrentProcessAdministrator();
            string exePath = GetBestExecutablePath() ?? string.Empty;
            var candidateExePaths = GetCandidateExecutablePaths(exePath);

            bool hasHkcuFlag = HasRunAsAdminInHive(Registry.CurrentUser, candidateExePaths);
            bool hasHklmFlag = HasRunAsAdminInHive(Registry.LocalMachine, candidateExePaths);
            bool hasRunAsAdminFlag = hasHkcuFlag || hasHklmFlag;

            string? hkcuRunVal = ReadRegistryString(Registry.CurrentUser, RunKeyPath, AppName);
            string? hklmRunVal = ReadRegistryString(Registry.LocalMachine, RunKeyPath, AppName);
            bool hasHkcuRun = !string.IsNullOrWhiteSpace(hkcuRunVal);
            bool hasHklmRun = !string.IsNullOrWhiteSpace(hklmRunVal);
            string? activeRegistryCmd = hasHkcuRun ? hkcuRunVal : hklmRunVal;

            bool isDisabledInTaskManager = false;
            if (hasHkcuRun)
            {
                byte[]? approvedBytes = ReadRegistryBytes(Registry.CurrentUser, StartupApprovedRunKeyPath, AppName);
                isDisabledInTaskManager = IsStartupApprovedDisabled(approvedBytes);
            }

            var taskQuery = QueryElevatedScheduledTask();

            var info = new StartupStatusInfo
            {
                IsCurrentProcessElevated = isProcessElevated,
                HasRunAsAdminFlag = hasRunAsAdminFlag,
                HasHkcuRunAsAdminFlag = hasHkcuFlag,
                HasHklmRunAsAdminFlag = hasHklmFlag,
                HasHkcuRunEntry = hasHkcuRun,
                HasHklmRunEntry = hasHklmRun,
                IsDisabledInTaskManager = isDisabledInTaskManager,
                HasScheduledTask = taskQuery.Exists,
                IsScheduledTaskForeignOrInaccessible = taskQuery.IsForeignOrInaccessible,
                ScheduledTaskPrincipal = taskQuery.Principal,
                ScheduledTaskCommand = taskQuery.TaskToRun,
                ScheduledTaskStatus = taskQuery.Status,
                RegistryCommand = activeRegistryCmd,
                CurrentWindowsUser = currentUser,
                TargetExecutablePath = exePath
            };

            if (taskQuery.IsForeignOrInaccessible)
            {
                info.Mode = StartupMode.BlockedOrForeignTask;
                info.IsEnabled = false;
                info.VisibleInTaskManagerStartupApps = false;
                info.HasDiagnosticWarning = true;
                info.StatusTitle = "Blocked or Foreign Elevated Task";
                info.DiagnosticDetail = string.IsNullOrWhiteSpace(taskQuery.Principal)
                    ? $"Scheduled task '{ElevatedTaskName}' exists under another administrator account and cannot be read without elevation. Click Repair Startup to reassign or clean it."
                    : $"Scheduled task '{ElevatedTaskName}' is bound to '{taskQuery.Principal}' instead of '{currentUser}', so it will not launch at your logon. Click Repair Startup to fix.";
                return info;
            }

            if (taskQuery.Exists)
            {
                string? taskExe = ExtractExePathFromCommand(taskQuery.TaskToRun);
                bool taskTargetMissing = !string.IsNullOrWhiteSpace(taskExe) && !File.Exists(taskExe);

                info.Mode = StartupMode.TaskSchedulerElevated;
                info.IsEnabled = !taskTargetMissing;
                info.VisibleInTaskManagerStartupApps = false;
                info.HasDiagnosticWarning = taskTargetMissing;
                info.StatusTitle = taskTargetMissing
                    ? "Broken Elevated Startup Path"
                    : "Active (Elevated Task Scheduler)";
                info.DiagnosticDetail = taskTargetMissing
                    ? $"Scheduled task points to missing file '{taskExe}'. Click Repair Startup to update the path."
                    : $"Runs with Highest Privileges at logon for '{taskQuery.Principal ?? currentUser}'. Task Scheduler entries do not appear in Task Manager > Startup Apps.";
                return info;
            }

            if (hasHkcuRun || hasHklmRun)
            {
                string? regExe = ExtractExePathFromCommand(activeRegistryCmd);
                bool regTargetMissing = string.IsNullOrWhiteSpace(regExe) || !File.Exists(regExe);

                if (hasRunAsAdminFlag)
                {
                    info.Mode = StartupMode.BlockedOrForeignTask;
                    info.IsEnabled = false;
                    info.VisibleInTaskManagerStartupApps = hasHkcuRun;
                    info.HasDiagnosticWarning = true;
                    info.StatusTitle = "Blocked by Run-As-Administrator Flag";
                    info.DiagnosticDetail = "Windows silently drops Registry Run entries when 'Run as administrator' compatibility flags are set. Click Repair Startup to either clear the admin flag (Task-Manager visible) or register an Elevated Task.";
                    return info;
                }

                if (regTargetMissing)
                {
                    info.Mode = StartupMode.BlockedOrForeignTask;
                    info.IsEnabled = false;
                    info.VisibleInTaskManagerStartupApps = hasHkcuRun;
                    info.HasDiagnosticWarning = true;
                    info.StatusTitle = "Broken Startup Executable Path";
                    info.DiagnosticDetail = $"Registry startup entry points to missing file '{regExe ?? activeRegistryCmd}'. Click Repair Startup to update.";
                    return info;
                }

                if (isDisabledInTaskManager)
                {
                    info.Mode = StartupMode.RegistryCurrentUser;
                    info.IsEnabled = false;
                    info.VisibleInTaskManagerStartupApps = true;
                    info.HasDiagnosticWarning = true;
                    info.StatusTitle = "Disabled in Task Manager";
                    info.DiagnosticDetail = "FileOrgy is registered in Windows Startup Apps, but is currently turned off in Task Manager. Enable it in Task Manager or click Repair Startup.";
                    return info;
                }

                info.Mode = StartupMode.RegistryCurrentUser;
                info.IsEnabled = true;
                info.VisibleInTaskManagerStartupApps = true;
                info.HasDiagnosticWarning = false;
                info.StatusTitle = "Active (Standard User Startup)";
                info.DiagnosticDetail = "Registered in HKCU\\Run. Visible and toggleable in Windows Task Manager > Startup Apps.";
                return info;
            }

            info.Mode = StartupMode.Disabled;
            info.IsEnabled = false;
            info.VisibleInTaskManagerStartupApps = false;
            info.HasDiagnosticWarning = false;
            info.StatusTitle = "Startup Disabled";
            info.DiagnosticDetail = hasRunAsAdminFlag
                ? "Automatic startup is off. Note: 'Run as administrator' flag is currently set."
                : "FileOrgy will not launch automatically when you sign in to Windows.";
            return info;
        }

        public static StartupOperationResult ApplyStartupConfiguration(bool enableStartup, bool runAsAdmin, bool startMinimized = true)
        {
            string? exePath = GetBestExecutablePath();
            if (enableStartup && (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)))
            {
                return new StartupOperationResult
                {
                    Success = false,
                    Message = "Could not locate a valid FileOrgy executable on disk.",
                    Status = GetStartupStatus()
                };
            }

            exePath ??= string.Empty;
            string currentUser = GetCurrentWindowsUserName();
            bool isAdmin = IsCurrentProcessAdministrator();
            var candidatePaths = GetCandidateExecutablePaths(exePath);

            try
            {
                if (!runAsAdmin)
                {
                    // 1. Remove RUNASADMIN from HKCU
                    RemoveRunAsAdminFromHkcu(candidatePaths);

                    // 2. Clean HKLM RUNASADMIN, HKLM Run, and any Elevated Scheduled Task
                    bool needsElevatedCleanup =
                        HasRunAsAdminInHive(Registry.LocalMachine, candidatePaths) ||
                        !string.IsNullOrWhiteSpace(ReadRegistryString(Registry.LocalMachine, RunKeyPath, AppName));

                    var taskQuery = QueryElevatedScheduledTask();
                    if (taskQuery.Exists)
                    {
                        if (!taskQuery.IsForeignOrInaccessible && isAdmin)
                        {
                            DeleteElevatedTaskDirect();
                        }
                        else
                        {
                            bool deletedWithoutElevation = !taskQuery.IsForeignOrInaccessible && DeleteElevatedTaskDirect();
                            if (!deletedWithoutElevation)
                            {
                                needsElevatedCleanup = true;
                            }
                        }
                    }

                    if (needsElevatedCleanup)
                    {
                        var elevResult = RunElevatedCleanup(candidatePaths, isAdmin);
                        if (!elevResult.Success)
                        {
                            return new StartupOperationResult
                            {
                                Success = false,
                                CancelledByUser = elevResult.CancelledByUser,
                                Message = elevResult.Message,
                                Status = GetStartupStatus()
                            };
                        }
                    }

                    // 3. Configure HKCU Run and clear any Task Manager disabled override
                    using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
                                       Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                    if (enableStartup)
                    {
                        string launchCmd = startMinimized ? $"\"{exePath}\" --tray" : $"\"{exePath}\"";
                        runKey?.SetValue(AppName, launchCmd, RegistryValueKind.String);
                        ClearStartupApprovedDisableByte();
                    }
                    else
                    {
                        runKey?.DeleteValue(AppName, throwOnMissingValue: false);
                    }

                    var finalStatus = GetStartupStatus();
                    return new StartupOperationResult
                    {
                        Success = true,
                        Message = enableStartup
                            ? "Configured standard startup (visible in Windows Task Manager > Startup Apps)."
                            : "Disabled Windows startup.",
                        Status = finalStatus
                    };
                }
                else
                {
                    // runAsAdmin == true
                    SetRunAsAdminInHkcu(exePath);

                    // Remove HKCU Run so Windows doesn't attempt and silently drop an elevated Run key
                    using (var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                    {
                        runKey?.DeleteValue(AppName, throwOnMissingValue: false);
                    }

                    if (enableStartup)
                    {
                        string args = startMinimized ? " --tray" : string.Empty;
                        string taskRunCommand = $"\\\"{exePath}\\\"{args}";

                        if (isAdmin)
                        {
                            DeleteHklmRunDirect();
                            if (!CreateElevatedTaskDirect(taskRunCommand, currentUser, out string err))
                            {
                                return new StartupOperationResult
                                {
                                    Success = false,
                                    Message = string.IsNullOrWhiteSpace(err) ? "Failed to create elevated startup task." : err,
                                    Status = GetStartupStatus()
                                };
                            }
                        }
                        else
                        {
                            var elevResult = RunElevatedTaskCreate(taskRunCommand, currentUser);
                            if (!elevResult.Success)
                            {
                                return new StartupOperationResult
                                {
                                    Success = false,
                                    CancelledByUser = elevResult.CancelledByUser,
                                    Message = elevResult.Message,
                                    Status = GetStartupStatus()
                                };
                            }
                        }

                        var finalStatus = GetStartupStatus();
                        return new StartupOperationResult
                        {
                            Success = true,
                            Message = $"Configured elevated Task Scheduler startup for '{currentUser}'.",
                            Status = finalStatus
                        };
                    }
                    else
                    {
                        var taskQuery = QueryElevatedScheduledTask();
                        if (taskQuery.Exists)
                        {
                            bool deletedDirect = !taskQuery.IsForeignOrInaccessible && DeleteElevatedTaskDirect();
                            if (!deletedDirect)
                            {
                                var elevResult = RunElevatedCleanup(Array.Empty<string>(), isAdmin, removeRunAsAdminFromHklm: false);
                                if (!elevResult.Success)
                                {
                                    return new StartupOperationResult
                                    {
                                        Success = false,
                                        CancelledByUser = elevResult.CancelledByUser,
                                        Message = elevResult.Message,
                                        Status = GetStartupStatus()
                                    };
                                }
                            }
                        }

                        return new StartupOperationResult
                        {
                            Success = true,
                            Message = "Disabled elevated Windows startup (Run as administrator flag retained).",
                            Status = GetStartupStatus()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new StartupOperationResult
                {
                    Success = false,
                    Message = $"Failed to update startup configuration: {ex.Message}",
                    Status = GetStartupStatus()
                };
            }
        }

        public static StartupOperationResult RepairStartup()
        {
            var status = GetStartupStatus();

            if (status.IsScheduledTaskForeignOrInaccessible)
            {
                // Reassign to current user if admin mode is active; otherwise remove foreign task
                return ApplyStartupConfiguration(
                    enableStartup: true,
                    runAsAdmin: status.HasRunAsAdminFlag);
            }

            if (status.IsDisabledInTaskManager)
            {
                return ApplyStartupConfiguration(enableStartup: true, runAsAdmin: false);
            }

            if (status.HasRunAsAdminFlag && (status.HasHkcuRunEntry || status.HasHklmRunEntry) && !status.HasScheduledTask)
            {
                // Clear conflicting RUNASADMIN flag so standard Task-Manager-visible startup works reliably
                return ApplyStartupConfiguration(enableStartup: true, runAsAdmin: false);
            }

            bool shouldEnable = status.IsEnabled || status.HasScheduledTask || status.HasHkcuRunEntry || status.HasHklmRunEntry;
            bool useAdmin = status.Mode == StartupMode.TaskSchedulerElevated || status.HasRunAsAdminFlag;
            return ApplyStartupConfiguration(enableStartup: shouldEnable, runAsAdmin: useAdmin);
        }

        private static string GetCurrentWindowsUserName()
        {
            try
            {
                return WindowsIdentity.GetCurrent().Name;
            }
            catch
            {
                return $"{Environment.UserDomainName}\\{Environment.UserName}";
            }
        }

        private static bool IsCurrentProcessAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static List<string> GetCandidateExecutablePaths(string exePath)
        {
            var paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                paths.Add(exePath);
                string? dir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string siblingApp = Path.Combine(dir, "FileOrgy.App.exe");
                    string siblingMain = Path.Combine(dir, "FileOrgy.exe");
                    if (!paths.Contains(siblingApp, StringComparer.OrdinalIgnoreCase)) paths.Add(siblingApp);
                    if (!paths.Contains(siblingMain, StringComparer.OrdinalIgnoreCase)) paths.Add(siblingMain);
                }
            }
            return paths;
        }

        private static bool HasRunAsAdminInHive(RegistryKey hive, IReadOnlyList<string> candidatePaths)
        {
            try
            {
                using var layers = hive.OpenSubKey(LayersKeyPath, writable: false);
                if (layers == null) return false;

                foreach (string path in candidatePaths)
                {
                    string? val = layers.GetValue(path)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val) &&
                        val.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void SetRunAsAdminInHkcu(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return;

            using var layers = Registry.CurrentUser.OpenSubKey(LayersKeyPath, writable: true) ??
                               Registry.CurrentUser.CreateSubKey(LayersKeyPath, writable: true);
            if (layers == null) return;

            string? existing = layers.GetValue(exePath)?.ToString();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                if (existing.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                layers.SetValue(exePath, $"{existing.Trim()} RUNASADMIN", RegistryValueKind.String);
            }
            else
            {
                layers.SetValue(exePath, "~ RUNASADMIN", RegistryValueKind.String);
            }
        }

        private static void RemoveRunAsAdminFromHkcu(IReadOnlyList<string> candidatePaths)
        {
            try
            {
                using var layers = Registry.CurrentUser.OpenSubKey(LayersKeyPath, writable: true);
                if (layers == null) return;

                foreach (string path in candidatePaths)
                {
                    string? val = layers.GetValue(path)?.ToString();
                    if (string.IsNullOrWhiteSpace(val) ||
                        !val.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var remainingTokens = val
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => !string.Equals(t, "RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (remainingTokens.Count == 0 || (remainingTokens.Count == 1 && remainingTokens[0] == "~"))
                    {
                        layers.DeleteValue(path, throwOnMissingValue: false);
                    }
                    else
                    {
                        layers.SetValue(path, string.Join(" ", remainingTokens), RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }

        private static void ClearStartupApprovedDisableByte()
        {
            try
            {
                using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKeyPath, writable: true);
                approvedKey?.DeleteValue(AppName, throwOnMissingValue: false);
            }
            catch { }
        }

        private static string? ReadRegistryString(RegistryKey hive, string subKeyPath, string valueName)
        {
            try
            {
                using var key = hive.OpenSubKey(subKeyPath, writable: false);
                return key?.GetValue(valueName)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ReadRegistryBytes(RegistryKey hive, string subKeyPath, string valueName)
        {
            try
            {
                using var key = hive.OpenSubKey(subKeyPath, writable: false);
                return key?.GetValue(valueName) as byte[];
            }
            catch
            {
                return null;
            }
        }

        private sealed class TaskQueryResult
        {
            public bool Exists { get; init; }
            public bool IsForeignOrInaccessible { get; init; }
            public string? Principal { get; init; }
            public string? TaskToRun { get; init; }
            public string? Status { get; init; }
        }

        private static TaskQueryResult QueryElevatedScheduledTask()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/query /tn \"{ElevatedTaskName}\" /v /fo LIST",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return new TaskQueryResult();

                var stderrTask = proc.StandardError.ReadToEndAsync();
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = stderrTask.GetAwaiter().GetResult();
                proc.WaitForExit(2500);

                string combined = $"{stdout}\n{stderr}";

                if (proc.ExitCode == 0)
                {
                    string? principal = StartupDiagnosticsParser.ParseSchtasksPrincipal(stdout);
                    string? taskToRun = StartupDiagnosticsParser.ParseSchtasksTaskToRun(stdout);
                    string? status = StartupDiagnosticsParser.ParseSchtasksStatus(stdout);
                    string currentUser = GetCurrentWindowsUserName();

                    bool foreign = !string.IsNullOrWhiteSpace(principal) &&
                                   !StartupDiagnosticsParser.IsSameWindowsPrincipal(principal, currentUser);

                    return new TaskQueryResult
                    {
                        Exists = true,
                        IsForeignOrInaccessible = foreign,
                        Principal = principal,
                        TaskToRun = taskToRun,
                        Status = status
                    };
                }

                if (StartupDiagnosticsParser.IsAccessDeniedMessage(combined))
                {
                    return new TaskQueryResult
                    {
                        Exists = true,
                        IsForeignOrInaccessible = true
                    };
                }
            }
            catch { }

            return new TaskQueryResult();
        }

        private static bool DeleteElevatedTaskDirect()
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
                if (proc == null) return false;
                proc.WaitForExit(3000);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CreateElevatedTaskDirect(string taskRunCommand, string targetUser, out string error)
        {
            error = string.Empty;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{ElevatedTaskName}\" /tr \"{taskRunCommand}\" /sc onlogon /ru \"{targetUser}\" /rl HIGHEST /it /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    error = "Failed to start schtasks.exe.";
                    return false;
                }

                var stderrTask = proc.StandardError.ReadToEndAsync();
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = stderrTask.GetAwaiter().GetResult();
                proc.WaitForExit(4000);

                if (proc.ExitCode == 0) return true;

                error = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void DeleteHklmRunDirect()
        {
            try
            {
                using var lmRun = Registry.LocalMachine.OpenSubKey(RunKeyPath, writable: true);
                lmRun?.DeleteValue(AppName, throwOnMissingValue: false);
            }
            catch { }
        }

        private static (bool Success, bool CancelledByUser, string Message) RunElevatedCleanup(
            IReadOnlyList<string> candidatePaths,
            bool alreadyElevated,
            bool removeRunAsAdminFromHklm = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            sb.AppendLine($"Remove-ItemProperty -Path 'HKLM:\\{RunKeyPath}' -Name '{AppName}' -Force");

            if (removeRunAsAdminFromHklm)
            {
                foreach (string path in candidatePaths)
                {
                    string escaped = EscapePsSingleQuoted(path);
                    sb.AppendLine($"$v = (Get-ItemProperty -Path 'HKLM:\\{LayersKeyPath}' -Name '{escaped}' -ErrorAction SilentlyContinue).'{escaped}'");
                    sb.AppendLine("if ($v -and $v -match 'RUNASADMIN') {");
                    sb.AppendLine("  $rem = @($v -split ' ' | Where-Object { $_ -and $_ -ne 'RUNASADMIN' })");
                    sb.AppendLine($"  if ($rem.Count -eq 0 -or ($rem.Count -eq 1 -and $rem[0] -eq '~')) {{ Remove-ItemProperty -Path 'HKLM:\\{LayersKeyPath}' -Name '{escaped}' -Force }}");
                    sb.AppendLine($"  else {{ Set-ItemProperty -Path 'HKLM:\\{LayersKeyPath}' -Name '{escaped}' -Value ($rem -join ' ') -Force }}");
                    sb.AppendLine("}");
                }
            }

            sb.AppendLine("$psi = New-Object System.Diagnostics.ProcessStartInfo");
            sb.AppendLine("$psi.FileName = 'schtasks.exe'");
            sb.AppendLine($"$psi.Arguments = '/delete /tn \"{ElevatedTaskName}\" /f'");
            sb.AppendLine("$psi.UseShellExecute = $false");
            sb.AppendLine("$psi.CreateNoWindow = $true");
            sb.AppendLine("$p = [System.Diagnostics.Process]::Start($psi)");
            sb.AppendLine("if ($p) { $p.WaitForExit() }");
            sb.AppendLine("exit 0");

            return ExecutePowerShellScript(sb.ToString(), elevate: !alreadyElevated);
        }

        private static (bool Success, bool CancelledByUser, string Message) RunElevatedTaskCreate(
            string taskRunCommand,
            string targetUser)
        {
            string schtasksArgs = $"/create /tn \"{ElevatedTaskName}\" /tr \"{taskRunCommand}\" /sc onlogon /ru \"{targetUser}\" /rl HIGHEST /it /f";
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            sb.AppendLine($"Remove-ItemProperty -Path 'HKLM:\\{RunKeyPath}' -Name '{AppName}' -Force");
            sb.AppendLine("$psi = New-Object System.Diagnostics.ProcessStartInfo");
            sb.AppendLine("$psi.FileName = 'schtasks.exe'");
            sb.AppendLine($"$psi.Arguments = '{EscapePsSingleQuoted(schtasksArgs)}'");
            sb.AppendLine("$psi.UseShellExecute = $false");
            sb.AppendLine("$psi.CreateNoWindow = $true");
            sb.AppendLine("$p = [System.Diagnostics.Process]::Start($psi)");
            sb.AppendLine("if (-not $p) { exit 1 }");
            sb.AppendLine("$p.WaitForExit()");
            sb.AppendLine("exit $p.ExitCode");

            return ExecutePowerShellScript(sb.ToString(), elevate: true);
        }

        private static (bool Success, bool CancelledByUser, string Message) ExecutePowerShellScript(string script, bool elevate)
        {
            try
            {
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}",
                    UseShellExecute = elevate,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (elevate)
                {
                    psi.Verb = "runas";
                }

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    return (false, false, "Failed to launch PowerShell helper.");
                }

                proc.WaitForExit(15000);
                return proc.ExitCode == 0
                    ? (true, false, "Completed.")
                    : (false, false, $"Elevated startup helper exited with code {proc.ExitCode}.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return (false, true, "Administrator permission prompt was cancelled by user.");
            }
            catch (Exception ex)
            {
                return (false, false, $"Failed to run elevated startup helper: {ex.Message}");
            }
        }

        private static string EscapePsSingleQuoted(string value) =>
            value.Replace("'", "''");

        private static string? GetBestExecutablePath()
        {
            string? currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                if (!currentExe.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase) &&
                    !currentExe.Contains(@"\tests\", StringComparison.OrdinalIgnoreCase))
                {
                    return currentExe;
                }
            }

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
