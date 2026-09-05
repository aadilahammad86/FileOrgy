using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileOrgy.Core.Models;
using FileOrgy.Core.Utils;

namespace FileOrgy.Core.Services
{
    public class FileDetectedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public WatchFolderConfig WatchFolder { get; set; } = null!;
        public WatcherChangeTypes ChangeType { get; set; }
    }

    public class FileSystemMonitorService : IDisposable
    {
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private bool _isPaused;
        private bool _disposed;

        public event EventHandler<FileDetectedEventArgs>? FileDetected;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
                UpdateWatchersEnableState();
            }
        }

        public void SynchronizeWatchFolders(IEnumerable<WatchFolderConfig> configs)
        {
            lock (_lock)
            {
                var activeConfigIds = new HashSet<string>();

                foreach (var config in configs)
                {
                    activeConfigIds.Add(config.Id);

                    if (_watchers.TryGetValue(config.Id, out var existingWatcher))
                    {
                        // Update existing watcher properties
                        if (!string.Equals(existingWatcher.Path, config.FolderPath, StringComparison.OrdinalIgnoreCase) ||
                            existingWatcher.IncludeSubdirectories != config.IncludeSubdirectories)
                        {
                            StopWatcher(config.Id);
                            StartWatcher(config);
                        }
                        else
                        {
                            existingWatcher.EnableRaisingEvents = config.Enabled && !_isPaused;
                        }
                    }
                    else if (config.Enabled)
                    {
                        StartWatcher(config);
                    }
                }

                // Remove watchers for configs that no longer exist
                var toRemove = _watchers.Keys.Where(id => !activeConfigIds.Contains(id)).ToList();
                foreach (var id in toRemove)
                {
                    StopWatcher(id);
                }
            }
        }

        public void StartWatcher(WatchFolderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.FolderPath) || !Directory.Exists(config.FolderPath))
            {
                return;
            }

            try
            {
                var watcher = new FileSystemWatcher(config.FolderPath)
                {
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size |
                                   NotifyFilters.CreationTime,
                    IncludeSubdirectories = config.IncludeSubdirectories,
                    InternalBufferSize = 65536 // 64KB kernel buffer to prevent buffer overflow drops
                };

                watcher.Created += (s, e) => OnFileSystemEvent(e.FullPath, config, WatcherChangeTypes.Created);
                watcher.Changed += (s, e) => OnFileSystemEvent(e.FullPath, config, WatcherChangeTypes.Changed);
                watcher.Renamed += (s, e) => OnFileSystemEvent(e.FullPath, config, WatcherChangeTypes.Renamed);
                watcher.Error += (s, e) => OnWatcherError(e.GetException(), config);

                watcher.EnableRaisingEvents = config.Enabled && !_isPaused;
                _watchers[config.Id] = watcher;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start watcher for '{config.FolderPath}': {ex.Message}");
            }
        }

        public void StopWatcher(string configId)
        {
            if (_watchers.TryGetValue(configId, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _watchers.Remove(configId);
            }
        }

        public void StopAll()
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();
            }
        }

        public async Task ScanFolderAsync(WatchFolderConfig config, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(config.FolderPath) || !Directory.Exists(config.FolderPath))
            {
                return;
            }

            var searchOption = config.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            await Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(config.FolderPath, "*", searchOption);
                    foreach (var file in files)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (ShouldIgnoreFile(file, config.IgnorePatterns)) continue;

                        FileDetected?.Invoke(this, new FileDetectedEventArgs
                        {
                            FilePath = file,
                            WatchFolder = config,
                            ChangeType = WatcherChangeTypes.Created
                        });
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"Error scanning folder '{config.FolderPath}': {ex.Message}");
                }
            }, ct);
        }

        private void OnFileSystemEvent(string fullPath, WatchFolderConfig config, WatcherChangeTypes changeType)
        {
            if (_isPaused || !config.Enabled) return;

            // Ignore directories
            if (Directory.Exists(fullPath)) return;

            // Check ignore patterns
            if (ShouldIgnoreFile(fullPath, config.IgnorePatterns)) return;

            // Debounce: Cancel pending notification for this file and schedule new one
            int delayMs = config.DebounceDelayMs > 0 ? config.DebounceDelayMs : 1200;

            var cts = new CancellationTokenSource();
            _debounceTokens.AddOrUpdate(
                fullPath,
                cts,
                (_, oldCts) =>
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                    return cts;
                });

            Task.Delay(delayMs, cts.Token).ContinueWith(async task =>
            {
                if (task.IsCanceled) return;

                _debounceTokens.TryRemove(fullPath, out _);

                if (!File.Exists(fullPath)) return;

                // Wait until file is unlocked and not actively being written
                bool isUnlocked = await FileLockHelper.WaitForFileUnlockAsync(fullPath, 15000, cts.Token);
                if (!isUnlocked || !File.Exists(fullPath)) return;

                FileDetected?.Invoke(this, new FileDetectedEventArgs
                {
                    FilePath = fullPath,
                    WatchFolder = config,
                    ChangeType = changeType
                });
            }, cts.Token);
        }

        private static bool ShouldIgnoreFile(string filePath, List<string>? ignorePatterns)
        {
            if (string.IsNullOrEmpty(filePath)) return true;

            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName)) return true;

            // Default ignores
            if (fileName.StartsWith("~$") || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ignorePatterns == null || ignorePatterns.Count == 0) return false;

            foreach (var pattern in ignorePatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                string regexPattern = "^" + Regex.Escape(pattern.Trim())
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";

                if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnWatcherError(Exception? ex, WatchFolderConfig config)
        {
            ErrorOccurred?.Invoke(this, $"Watcher error on '{config.FolderPath}': {ex?.Message}");
        }

        private void UpdateWatchersEnableState()
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = !_isPaused;
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var cts in _debounceTokens.Values)
            {
                try { cts.Cancel(); cts.Dispose(); } catch { }
            }
            _debounceTokens.Clear();

            StopAll();
        }
    }
}
