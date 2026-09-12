using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FileOrgy.Core.Models;

namespace FileOrgy.Core.Services
{
    public class FileOrgyOrchestrator : IDisposable
    {
        private readonly ConfigService _configService;
        private readonly LogManager _logManager;
        private readonly FileSystemMonitorService _monitorService;

        private readonly Channel<string> _fileProcessingChannel;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<Task> _workerTasks = new();

        private int _totalFilesProcessed;
        private long _totalBytesSaved;
        private bool _disposed;

        public ConfigService ConfigService => _configService;
        public LogManager LogManager => _logManager;
        public FileSystemMonitorService MonitorService => _monitorService;

        public bool IsMonitoringPaused
        {
            get => _monitorService.IsPaused;
            set => _monitorService.IsPaused = value;
        }

        public int TotalFilesProcessed => _totalFilesProcessed;
        public long TotalBytesSaved => Interlocked.Read(ref _totalBytesSaved);

        public event Action? StatsUpdated;

        public FileOrgyOrchestrator(ConfigService? configService = null, LogManager? logManager = null)
        {
            _configService = configService ?? new ConfigService();
            _logManager = logManager ?? LogManager.Instance;
            _monitorService = new FileSystemMonitorService();

            var channelOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _fileProcessingChannel = Channel.CreateBounded<string>(channelOptions);

            _monitorService.FileDetected += OnFileDetected;
            _monitorService.ErrorOccurred += OnMonitorError;

            // Start background worker loop(s)
            int concurrency = Math.Max(1, Math.Min(4, _configService.CurrentConfig.Settings.MaxConcurrentOperations));
            for (int i = 0; i < concurrency; i++)
            {
                _workerTasks.Add(Task.Run(() => ProcessingWorkerLoopAsync(_cts.Token)));
            }

            // Sync watch folders from configuration
            _monitorService.SynchronizeWatchFolders(_configService.CurrentConfig.WatchFolders);
        }

        public void ReloadConfiguration()
        {
            _monitorService.SynchronizeWatchFolders(_configService.CurrentConfig.WatchFolders);
        }

        public void EnqueueFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                _fileProcessingChannel.Writer.TryWrite(filePath);
            }
        }

        public async Task ScanFolderNowAsync(WatchFolderConfig folderConfig, CancellationToken ct = default)
        {
            _logManager.AddLog(new LogEntry
            {
                Level = LogLevel.Info,
                EventType = LogEventType.SystemInfo,
                SourcePath = folderConfig.FolderPath,
                Details = $"Manual scan triggered on '{folderConfig.FolderPath}'"
            });

            await _monitorService.ScanFolderAsync(folderConfig, ct);
        }

        private void OnFileDetected(object? sender, FileDetectedEventArgs e)
        {
            _logManager.AddLog(new LogEntry
            {
                Level = LogLevel.Info,
                EventType = LogEventType.FileDetected,
                SourcePath = e.FilePath,
                Details = $"Detected file: {Path.GetFileName(e.FilePath)}"
            });

            EnqueueFile(e.FilePath);
        }

        private void OnMonitorError(object? sender, string errorMessage)
        {
            _logManager.AddLog(new LogEntry
            {
                Level = LogLevel.Error,
                EventType = LogEventType.ErrorOccurred,
                Details = errorMessage
            });
        }

        private async Task ProcessingWorkerLoopAsync(CancellationToken ct)
        {
            var reader = _fileProcessingChannel.Reader;

            while (!ct.IsCancellationRequested && await reader.WaitToReadAsync(ct))
            {
                while (reader.TryRead(out var filePath))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        await ProcessFileAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logManager.AddLog(new LogEntry
                        {
                            Level = LogLevel.Error,
                            EventType = LogEventType.ErrorOccurred,
                            SourcePath = filePath,
                            Details = $"Unhandled error processing file: {ex.Message}",
                            IsSuccess = false
                        });
                    }
                }
            }
        }

        public async Task<bool> ProcessFileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            // Multipart secondary check (e.g. file.part2.rar) - don't process secondary volumes independently
            if (ArchiveExtractor.IsSecondaryMultipartVolume(filePath))
            {
                return false;
            }

            var config = _configService.CurrentConfig;
            var context = new WorkflowContext(filePath, config.CustomVariables);
            long initialSize = context.FileSizeBytes;

            // Get rules sorted by priority (lowest number = highest priority)
            var activeRules = config.Rules
                .Where(r => r.Enabled)
                .OrderBy(r => r.Priority)
                .ToList();

            bool anyRuleMatched = false;

            foreach (var rule in activeRules)
            {
                if (context.IsAborted) break;

                // Check folder scoping if configured
                if (rule.ScopedFolderIds != null && rule.ScopedFolderIds.Count > 0)
                {
                    string parentDir = context.DirectoryPath;
                    bool inScope = config.WatchFolders
                        .Where(wf => rule.ScopedFolderIds.Contains(wf.Id))
                        .Any(wf => parentDir.StartsWith(wf.FolderPath, StringComparison.OrdinalIgnoreCase));

                    if (!inScope) continue;
                }

                bool isMatch = ConditionEvaluator.EvaluateRule(rule, context, config.KeywordLists, config.CustomVariables);
                if (isMatch)
                {
                    anyRuleMatched = true;

                    var executionResult = await WorkflowEngine.ExecuteWorkflowAsync(rule, context, log =>
                    {
                        _logManager.AddLog(log);
                    });

                    Interlocked.Increment(ref _totalFilesProcessed);

                    // Track saved bytes if file was recycled/deleted
                    if (!File.Exists(context.CurrentFilePath))
                    {
                        Interlocked.Add(ref _totalBytesSaved, initialSize);
                    }

                    StatsUpdated?.Invoke();

                    // If archive step extracted files and wants them organized, queue them up!
                    if (executionResult.ExtractedFilesToEnqueue.Count > 0)
                    {
                        foreach (var extractedFile in executionResult.ExtractedFilesToEnqueue)
                        {
                            EnqueueFile(extractedFile);
                        }
                    }

                    if (rule.StopOnFirstMatch)
                    {
                        break;
                    }
                }
            }

            if (!anyRuleMatched)
            {
                _logManager.AddLog(new LogEntry
                {
                    Level = LogLevel.Info,
                    EventType = LogEventType.SystemInfo,
                    SourcePath = filePath,
                    Details = $"No matching rule for '{Path.GetFileName(filePath)}'. Left untouched in folder."
                });
            }

            return anyRuleMatched;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _monitorService.Dispose();
            _cts.Dispose();
        }
    }
}
