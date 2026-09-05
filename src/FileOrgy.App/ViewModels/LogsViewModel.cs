using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App.ViewModels
{
    public class LogsViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private string _searchText = string.Empty;
        private string _selectedLevelFilter = "All";
        private LogEntry? _selectedLog;

        public ObservableCollection<LogEntry> Logs { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
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
                if (SetProperty(ref _selectedLevelFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public LogEntry? SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
        }

        public ICommand ClearLogsCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand OpenSourceInExplorerCommand { get; }
        public ICommand OpenDestInExplorerCommand { get; }
        public ICommand CopyPathCommand { get; }

        public LogsViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;

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

            var filtered = _orchestrator.LogManager.FilterLogs(SearchText, level, null, null);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                Logs.Clear();
                foreach (var log in filtered.Take(300))
                {
                    Logs.Add(log);
                }
            });
        }

        private void OnLogAdded(LogEntry entry)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                // Prepend if matches current filter
                if (MatchesCurrentFilter(entry))
                {
                    Logs.Insert(0, entry);
                    if (Logs.Count > 300)
                    {
                        Logs.RemoveAt(Logs.Count - 1);
                    }
                }
            });
        }

        private bool MatchesCurrentFilter(LogEntry entry)
        {
            if (SelectedLevelFilter != "All" && entry.Level.ToString() != SelectedLevelFilter)
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

        private void ClearLogs()
        {
            Logs.Clear();
            _orchestrator.LogManager.ClearInMemoryLogs();
        }

        private void ExportCsv()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"FileOrgy_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                _orchestrator.LogManager.ExportToCsv(sfd.FileName);
                System.Windows.MessageBox.Show("Logs exported successfully to CSV.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportJson()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"FileOrgy_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                _orchestrator.LogManager.ExportToJson(sfd.FileName);
                System.Windows.MessageBox.Show("Logs exported successfully to JSON.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
                System.Windows.Clipboard.SetText(path);
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
