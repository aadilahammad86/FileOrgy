using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileOrgy.Core.Utils
{
    public static class FileLockHelper
    {
        /// <summary>
        /// Checks if a file is locked by another process (e.g., active download or copy operation).
        /// </summary>
        public static bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // File might be read-only or in use with read lock
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Waits until the file is completely written and unlocked, using non-blocking exponential backoff.
        /// </summary>
        public static async Task<bool> WaitForFileUnlockAsync(string filePath, int timeoutMs = 15000, CancellationToken ct = default)
        {
            if (!File.Exists(filePath)) return false;

            var startTime = DateTime.UtcNow;
            int delay = 200;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (ct.IsCancellationRequested) return false;

                if (!IsFileLocked(filePath))
                {
                    // Check if file size is stable (e.g. large browser download)
                    long size1 = GetFileSizeSafe(filePath);
                    await Task.Delay(250, ct);
                    long size2 = GetFileSizeSafe(filePath);

                    if (size1 == size2 && !IsFileLocked(filePath))
                    {
                        return true;
                    }
                }

                await Task.Delay(delay, ct);
                delay = Math.Min(delay * 2, 2000);
            }

            return !IsFileLocked(filePath);
        }

        private static long GetFileSizeSafe(string filePath)
        {
            try
            {
                return new FileInfo(filePath).Length;
            }
            catch
            {
                return -1;
            }
        }
    }
}
