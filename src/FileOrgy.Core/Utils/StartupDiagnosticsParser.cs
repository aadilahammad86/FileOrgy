using System;
using System.Linq;

namespace FileOrgy.Core.Utils
{
    /// <summary>
    /// Pure diagnostic parsing helpers for Windows startup registration (schtasks, Registry Run, StartupApproved).
    /// </summary>
    public static class StartupDiagnosticsParser
    {
        /// <summary>
        /// Parses the "Run As User:" principal from `schtasks.exe /query /v /fo LIST` output.
        /// </summary>
        public static string? ParseSchtasksPrincipal(string? output)
        {
            return ParseSchtasksListField(output, "Run As User");
        }

        /// <summary>
        /// Parses the "Task To Run:" command line from `schtasks.exe /query /v /fo LIST` output.
        /// </summary>
        public static string? ParseSchtasksTaskToRun(string? output)
        {
            return ParseSchtasksListField(output, "Task To Run");
        }

        /// <summary>
        /// Parses the "Status:" field from `schtasks.exe /query /v /fo LIST` output.
        /// </summary>
        public static string? ParseSchtasksStatus(string? output)
        {
            return ParseSchtasksListField(output, "Status");
        }

        /// <summary>
        /// Returns true if schtasks output indicates an access-denied error (typically meaning a protected or foreign admin task exists).
        /// </summary>
        public static bool IsAccessDeniedMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            return message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("0x80070005", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if schtasks output indicates the scheduled task does not exist.
        /// </summary>
        public static bool IsTaskNotFoundMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            return message.Contains("cannot find the file specified", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("cannot find the path specified", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("system cannot find", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Inspects Windows Explorer StartupApproved\Run binary data to determine whether Task Manager disabled the entry.
        /// Byte 0 == 0x02 indicates enabled; odd first bytes (0x03, 0x01, etc.) indicate disabled.
        /// </summary>
        public static bool IsStartupApprovedDisabled(byte[]? data)
        {
            if (data == null || data.Length == 0) return false;
            return data[0] == 0x03 || (data[0] & 0x01) == 1;
        }

        /// <summary>
        /// Extracts the executable path from a Run registry value or Task Scheduler command string.
        /// </summary>
        public static string? ExtractExePathFromCommand(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;

            string trimmed = command.Trim();

            if (trimmed.StartsWith('\"'))
            {
                int closingQuote = trimmed.IndexOf('\"', 1);
                if (closingQuote > 1)
                {
                    string quoted = trimmed[1..closingQuote].Trim();
                    return string.IsNullOrWhiteSpace(quoted) ? null : quoted;
                }
            }

            int exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                string upToExe = trimmed[..(exeIndex + 4)].Trim('\"', ' ');
                return string.IsNullOrWhiteSpace(upToExe) ? null : upToExe;
            }

            int firstSpace = trimmed.IndexOf(' ');
            string token = (firstSpace > 0 ? trimmed[..firstSpace] : trimmed).Trim('\"');
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        /// <summary>
        /// Compares a scheduled task principal against the current Windows identity name.
        /// </summary>
        public static bool IsSameWindowsPrincipal(string? taskPrincipal, string? currentWindowsUser)
        {
            if (string.IsNullOrWhiteSpace(taskPrincipal) || string.IsNullOrWhiteSpace(currentWindowsUser))
            {
                return false;
            }

            string normalizedTask = taskPrincipal.Trim();
            string normalizedCurrent = currentWindowsUser.Trim();

            if (string.Equals(normalizedTask, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Handle unqualified local username returned by some schtasks configurations
            if (!normalizedTask.Contains('\\') && normalizedCurrent.Contains('\\'))
            {
                string shortCurrent = normalizedCurrent.Split('\\').Last();
                return string.Equals(normalizedTask, shortCurrent, StringComparison.OrdinalIgnoreCase);
            }

            if (normalizedTask.Contains('\\') && !normalizedCurrent.Contains('\\'))
            {
                string shortTask = normalizedTask.Split('\\').Last();
                return string.Equals(shortTask, normalizedCurrent, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static string? ParseSchtasksListField(string? output, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!line.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string remainder = line[fieldName.Length..].TrimStart();
                if (!remainder.StartsWith(':'))
                {
                    continue;
                }

                string value = remainder[1..].Trim();
                if (string.IsNullOrWhiteSpace(value) ||
                    string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return value;
            }

            return null;
        }
    }
}
