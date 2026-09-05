using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileOrgy.Core.Models;

namespace FileOrgy.Core.Services
{
    public class LogStatistics
    {
        public int TotalOperations { get; set; }
        public int OperationsToday { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public long BytesProcessed { get; set; }
    }

    public class LogManager
    {
        private static readonly Lazy<LogManager> _instance = new(() => new LogManager());
        public static LogManager Instance => _instance.Value;

        private readonly ConcurrentQueue<LogEntry> _writeQueue = new();
        private readonly List<LogEntry> _inMemoryLogs = new();
        private readonly object _lock = new();
        private readonly string _logFilePath;
        private readonly Timer _flushTimer;
        private int _operationsToday;
        private DateTime _currentDay = DateTime.Today;

        public event Action<LogEntry>? LogAdded;

        public LogManager(string? customLogPath = null)
        {
            if (string.IsNullOrWhiteSpace(customLogPath))
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(appData, "FileOrgy", "logs");
                Directory.CreateDirectory(folder);
                _logFilePath = Path.Combine(folder, "activity.jsonl");
            }
            else
            {
                _logFilePath = customLogPath;
                string? dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }

            LoadRecentLogs();

            // Periodic background flush every 2 seconds to keep disk I/O near zero
            _flushTimer = new Timer(_ => FlushQueue(), null, 2000, 2000);
        }

        public void AddLog(LogEntry entry)
        {
            lock (_lock)
            {
                if (DateTime.Today != _currentDay)
                {
                    _currentDay = DateTime.Today;
                    _operationsToday = 0;
                }

                _operationsToday++;
                _inMemoryLogs.Insert(0, entry);

                // Keep memory usage lean: max 1000 items in memory
                if (_inMemoryLogs.Count > 1000)
                {
                    _inMemoryLogs.RemoveAt(_inMemoryLogs.Count - 1);
                }
            }

            _writeQueue.Enqueue(entry);
            LogAdded?.Invoke(entry);
        }

        public IReadOnlyList<LogEntry> GetRecentLogs(int count = 200)
        {
            lock (_lock)
            {
                return _inMemoryLogs.Take(count).ToList();
            }
        }

        public List<LogEntry> FilterLogs(string? query, LogLevel? level, DateTime? fromDate, DateTime? toDate)
        {
            lock (_lock)
            {
                var queryable = _inMemoryLogs.AsEnumerable();

                if (level.HasValue)
                {
                    queryable = queryable.Where(l => l.Level == level.Value);
                }

                if (fromDate.HasValue)
                {
                    queryable = queryable.Where(l => l.Timestamp >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    queryable = queryable.Where(l => l.Timestamp <= toDate.Value);
                }

                if (!string.IsNullOrWhiteSpace(query))
                {
                    queryable = queryable.Where(l =>
                        l.Details.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        l.SourcePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        l.DestinationPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        l.RuleName.Contains(query, StringComparison.OrdinalIgnoreCase));
                }

                return queryable.ToList();
            }
        }

        public LogStatistics GetStatistics()
        {
            lock (_lock)
            {
                int total = _inMemoryLogs.Count;
                int success = _inMemoryLogs.Count(l => l.IsSuccess);
                int errors = _inMemoryLogs.Count(l => !l.IsSuccess);

                return new LogStatistics
                {
                    TotalOperations = total,
                    OperationsToday = _operationsToday,
                    SuccessCount = success,
                    ErrorCount = errors
                };
            }
        }

        public void ClearInMemoryLogs()
        {
            lock (_lock)
            {
                _inMemoryLogs.Clear();
            }
        }

        public void ExportToCsv(string targetPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Level,EventType,RuleName,StepName,IsSuccess,DurationMs,SourcePath,DestinationPath,Details");

            List<LogEntry> logs;
            lock (_lock)
            {
                logs = _inMemoryLogs.ToList();
            }

            foreach (var log in logs)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\"{0:yyyy-MM-dd HH:mm:ss}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\"",
                    log.Timestamp,
                    log.Level,
                    log.EventType,
                    EscapeCsv(log.RuleName),
                    EscapeCsv(log.StepName),
                    log.IsSuccess,
                    log.DurationMs,
                    EscapeCsv(log.SourcePath),
                    EscapeCsv(log.DestinationPath),
                    EscapeCsv(log.Details)));
            }

            File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportToJson(string targetPath)
        {
            List<LogEntry> logs;
            lock (_lock)
            {
                logs = _inMemoryLogs.ToList();
            }

            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(targetPath, json, Encoding.UTF8);
        }

        private void FlushQueue()
        {
            if (_writeQueue.IsEmpty) return;

            var entries = new List<LogEntry>();
            while (_writeQueue.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            if (entries.Count == 0) return;

            try
            {
                var lines = entries.Select(e => JsonSerializer.Serialize(e));
                File.AppendAllLines(_logFilePath, lines, Encoding.UTF8);
            }
            catch
            {
                // Silently ignore disk write issues in background
            }
        }

        private void LoadRecentLogs()
        {
            if (!File.Exists(_logFilePath)) return;

            try
            {
                var lines = File.ReadLines(_logFilePath, Encoding.UTF8).TakeLast(500);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<LogEntry>(line);
                        if (entry != null)
                        {
                            _inMemoryLogs.Add(entry);
                            if (entry.Timestamp.Date == DateTime.Today)
                            {
                                _operationsToday++;
                            }
                        }
                    }
                    catch { }
                }

                _inMemoryLogs.Reverse(); // Newest first
            }
            catch { }
        }

        private static string EscapeCsv(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("\"", "\"\"");
        }
    }
}
