using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;
using WpfClipboard = System.Windows.Clipboard;

namespace FileOrgy.App.ViewModels
{
    public class LogsViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly NotificationService? _notifications;
        private string _searchText = string.Empty;
        private string _selectedLevelFilter = "All";
        private string _selectedEventFilter = "All";
        private int _totalMatchingCount;
        private LogEntry? _selectedLog;

        public ObservableCollection<LogEntry> Logs { get; } = new();

        public IReadOnlyList<string> LevelFilterOptions { get; } = new[]
        {
            "All", "Success", "Error", "Warning", "Info"
        };

        public IReadOnlyList<string> EventFilterOptions { get; } = new[]
        {
            "All", "Moved", "Renamed", "Extracted", "Cleanup / Recycle", "Errors"
        };

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedLevelFilter
        {
            get => _selectedLevelFilter;
            set
            {
                if (SetProperty(ref _selectedLevelFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedEventFilter
        {
            get => _selectedEventFilter;
            set
            {
                if (SetProperty(ref _selectedEventFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
                {
                    ApplyFilter();
                }
            }
        }

        public int TotalMatchingCount
        {
            get => _totalMatchingCount;
            private set => SetProperty(ref _totalMatchingCount, value);
        }

        public LogEntry? SelectedLog
        {
            get => _selectedLog;
            set
            {
                if (SetProperty(ref _selectedLog, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand ClearLogsCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand OpenSourceInExplorerCommand { get; }
        public ICommand OpenDestInExplorerCommand { get; }
        public ICommand CopyPathCommand { get; }

        public LogsViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _notifications = notifications;

            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportCsvCommand = new RelayCommand(ExportCsv);
            ExportJsonCommand = new RelayCommand(ExportJson);
            OpenSourceInExplorerCommand = new RelayCommand(OpenSourceInExplorer, () => SelectedLog != null && !string.IsNullOrEmpty(SelectedLog.SourcePath));
            OpenDestInExplorerCommand = new RelayCommand(OpenDestInExplorer, () => SelectedLog != null && !string.IsNullOrEmpty(SelectedLog.DestinationPath));
            CopyPathCommand = new RelayCommand(CopyPath, () => SelectedLog != null);

            _orchestrator.LogManager.LogAdded += OnLogAdded;
            ApplyFilter();
        }

        public void ApplyFilter()
        {
            LogLevel? level = SelectedLevelFilter switch
            {
                "Success" => LogLevel.Success,
                "Error" => LogLevel.Error,
                "Warning" => LogLevel.Warning,
                "Info" => LogLevel.Info,
                _ => null
            };

            var filtered = _orchestrator.LogManager
                .FilterLogs(SearchText, level, null, null)
                .Where(MatchesEventFilter)
                .ToList();

            void Apply()
            {
                TotalMatchingCount = filtered.Count;
                Logs.Clear();
                foreach (var log in filtered.Take(300))
                {
                    Logs.Add(log);
                }
            }

            var dispatcher = WpfApp.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                dispatcher.Invoke(Apply);
            }
        }

        private void OnLogAdded(LogEntry entry)
        {
            void Apply()
            {
                if (MatchesCurrentFilter(entry))
                {
                    TotalMatchingCount++;
                    Logs.Insert(0, entry);
                    if (Logs.Count > 300)
                    {
                        Logs.RemoveAt(Logs.Count - 1);
                    }
                }
            }

            var dispatcher = WpfApp.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                dispatcher.Invoke(Apply);
            }
        }

        private bool MatchesCurrentFilter(LogEntry entry)
        {
            if (SelectedLevelFilter != "All" && entry.Level.ToString() != SelectedLevelFilter)
            {
                return false;
            }

            if (!MatchesEventFilter(entry))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                return entry.Details.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       entry.SourcePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       entry.DestinationPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       entry.RuleName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private bool MatchesEventFilter(LogEntry entry)
        {
            return SelectedEventFilter switch
            {
                "Moved" => entry.EventType is LogEventType.FileMoved or LogEventType.FileCopied,
                "Renamed" => entry.EventType == LogEventType.FileRenamed,
                "Extracted" => entry.EventType == LogEventType.ArchiveExtracted,
                "Cleanup / Recycle" => entry.EventType is LogEventType.CleanupExecuted or LogEventType.FileRecycled or LogEventType.FileDeleted,
                "Errors" => entry.EventType == LogEventType.ErrorOccurred || entry.Level == LogLevel.Error,
                _ => true
            };
        }

        private void ClearLogs()
        {
            Logs.Clear();
            TotalMatchingCount = 0;
            _orchestrator.LogManager.ClearInMemoryLogs();
            _notifications?.ShowToast("Logs Cleared", "In-memory activity history cleared.", ToastSeverity.Info);
        }

        private void ExportCsv()
        {
            using var sfd = new Forms.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"FileOrgy_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == Forms.DialogResult.OK)
            {
                try
                {
                    _orchestrator.LogManager.ExportToCsv(sfd.FileName);
                    _notifications?.ShowToast(
                        "Export Complete",
                        $"Exported logs to {Path.GetFileName(sfd.FileName)}.",
                        ToastSeverity.Success);
                }
                catch (Exception ex)
                {
                    _notifications?.ShowToast("Export Failed", ex.Message, ToastSeverity.Error);
                }
            }
        }

        private void ExportJson()
        {
            using var sfd = new Forms.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"FileOrgy_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (sfd.ShowDialog() == Forms.DialogResult.OK)
            {
                try
                {
                    _orchestrator.LogManager.ExportToJson(sfd.FileName);
                    _notifications?.ShowToast(
                        "Export Complete",
                        $"Exported logs to {Path.GetFileName(sfd.FileName)}.",
                        ToastSeverity.Success);
                }
                catch (Exception ex)
                {
                    _notifications?.ShowToast("Export Failed", ex.Message, ToastSeverity.Error);
                }
            }
        }

        private void OpenSourceInExplorer()
        {
            if (SelectedLog == null || string.IsNullOrEmpty(SelectedLog.SourcePath)) return;
            OpenInExplorer(SelectedLog.SourcePath);
        }

        private void OpenDestInExplorer()
        {
            if (SelectedLog == null || string.IsNullOrEmpty(SelectedLog.DestinationPath)) return;
            OpenInExplorer(SelectedLog.DestinationPath);
        }

        private void CopyPath()
        {
            if (SelectedLog == null) return;
            string path = !string.IsNullOrEmpty(SelectedLog.DestinationPath) ? SelectedLog.DestinationPath : SelectedLog.SourcePath;
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    WpfClipboard.SetText(path);
                    _notifications?.ShowToast("Path Copied", path, ToastSeverity.Success);
                }
                catch { }
            }
        }

        private static void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"\"{path}\"");
                }
                else
                {
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
            }
            catch { }
        }
    }
}
