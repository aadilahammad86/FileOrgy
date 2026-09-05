using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FileOrgy.Core.Models;
using FileOrgy.Core.Utils;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FileOrgy.Core.Services
{
    public class ArchiveExtractionResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public List<string> ExtractedFiles { get; } = new();
    }

    public static class ArchiveExtractor
    {
        private static readonly HashSet<string> SingleArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz", ".7z", ".rar"
        };

        // Matches multipart secondary files (e.g. file.part2.rar, file.z02, file.zip.002, file.7z.002)
        // These should NOT trigger a new extraction because they are extracted when processing part 1.
        private static readonly Regex MultipartSecondaryRegex = new(
            @"(?:\.part0*[2-9]\d*\.rar|\.r\d{2}|\.z0*[2-9]\d*|\.7z\.0*[2-9]\d*|\.zip\.0*[2-9]\d*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Matches first volume of multipart archives:
        // .part1.rar, .part01.rar, .zip.001, .7z.001
        private static readonly Regex MultipartPrimaryRegex = new(
            @"(?:\.part0*1\.rar|\.7z\.0*1|\.zip\.0*1)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Determines whether a file path represents a supported archive format.
        /// </summary>
        public static bool IsSupportedArchive(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string fileName = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath);

            if (SingleArchiveExtensions.Contains(ext))
            {
                // Make sure it's not a secondary multipart file like .part02.rar
                return !MultipartSecondaryRegex.IsMatch(fileName);
            }

            if (MultipartPrimaryRegex.IsMatch(fileName))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a file is a secondary volume of a multipart archive (e.g., .part2.rar).
        /// </summary>
        public static bool IsSecondaryMultipartVolume(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            return MultipartSecondaryRegex.IsMatch(Path.GetFileName(filePath));
        }

        /// <summary>
        /// Extracts archive contents safely to the designated destination directory.
        /// </summary>
        public static ArchiveExtractionResult Extract(string archivePath, string destinationDirectory)
        {
            var result = new ArchiveExtractionResult();

            if (!File.Exists(archivePath))
            {
                result.ErrorMessage = "Archive file not found";
                return result;
            }

            try
            {
                Directory.CreateDirectory(destinationDirectory);
                result.OutputDirectory = destinationDirectory;

                var readerOptions = new ReaderOptions
                {
                    ArchiveEncoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 }
                };

                using var archive = ArchiveFactory.OpenArchive(archivePath, readerOptions);

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) continue;

                    string entryKey = entry.Key ?? Path.GetFileName(archivePath);
                    if (!PathHelper.IsSafeExtractionPath(destinationDirectory, entryKey))
                    {
                        // Zip slip attempt detected, skip this dangerous entry
                        continue;
                    }

                    string targetFilePath = Path.Combine(destinationDirectory, entryKey);
                    string? targetDir = Path.GetDirectoryName(targetFilePath);
                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    entry.WriteToFile(targetFilePath, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });

                    result.ExtractedFiles.Add(targetFilePath);
                }

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Finds all companion files for multipart archives (e.g. file.part1.rar, file.part2.rar, etc.)
        /// </summary>
        public static List<string> FindMultipartVolumes(string firstVolumePath)
        {
            var volumes = new List<string>();

            try
            {
                var parts = ArchiveFactory.GetFileParts(firstVolumePath);
                foreach (var p in parts)
                {
                    if (File.Exists(p)) volumes.Add(p);
                }
            }
            catch { }

            if (volumes.Count == 0 && File.Exists(firstVolumePath))
            {
                volumes.Add(firstVolumePath);
            }

            return volumes;
        }
    }
}
