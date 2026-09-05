using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FileOrgy.Core.Utils
{
    public static class PathHelper
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Cleans a filename by replacing invalid characters with a fallback character.
        /// </summary>
        public static string SanitizeFileName(string fileName, char replacement = '_')
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "unnamed";

            var clean = fileName;
            foreach (var c in InvalidFileNameChars)
            {
                clean = clean.Replace(c, replacement);
            }

            clean = clean.Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(clean) ? "unnamed" : clean;
        }

        /// <summary>
        /// Resolves conflict by appending (1), (2), etc. if target already exists.
        /// </summary>
        public static string GetUniqueFilePath(string destinationPath)
        {
            if (!File.Exists(destinationPath)) return destinationPath;

            string dir = Path.GetDirectoryName(destinationPath) ?? string.Empty;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
            string ext = Path.GetExtension(destinationPath);

            int counter = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{fileNameWithoutExt} ({counter}){ext}");
                counter++;
            } while (File.Exists(candidate));

            return candidate;
        }

        /// <summary>
        /// Human-readable file size format.
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Prevents zip-slip directory traversal attacks during archive extraction.
        /// </summary>
        public static bool IsSafeExtractionPath(string destinationDirectory, string entryPath)
        {
            string fullDestDir = Path.GetFullPath(destinationDirectory);
            if (!fullDestDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullDestDir += Path.DirectorySeparatorChar;
            }

            string fullTarget = Path.GetFullPath(Path.Combine(destinationDirectory, entryPath));
            return fullTarget.StartsWith(fullDestDir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a path is dangerous to delete/clean (e.g. Windows, System32, Root drives).
        /// </summary>
        public static bool IsSystemOrProtectedDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;

            try
            {
                string full = Path.GetFullPath(path).TrimEnd('\\', '/');
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
                string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd('\\', '/');
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');

                if (full.Equals(winDir, StringComparison.OrdinalIgnoreCase) || full.StartsWith(winDir + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (full.Equals(sysDir, StringComparison.OrdinalIgnoreCase) || full.StartsWith(sysDir + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (full.Equals(progFiles, StringComparison.OrdinalIgnoreCase) || full.Equals(progFilesX86, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Protect root drives like C:\, D:\
                var root = Path.GetPathRoot(full)?.TrimEnd('\\', '/');
                if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
