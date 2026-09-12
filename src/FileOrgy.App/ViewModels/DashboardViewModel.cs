using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using FileOrgy.Core.Utils;

namespace FileOrgy.App.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private int _totalFilesProcessed;
        private int _operationsToday;
        private int _activeFoldersCount;
        private int _activeRulesCount;
        private string _storageSavedText = "0 B";
        private bool _isMonitoring = true;
        private string _statusText = "Active - Monitoring Folders";

        public int TotalFilesProcessed
        {
            get => _totalFilesProcessed;
            set => SetProperty(ref _totalFilesProcessed, value);
        }

        public int OperationsToday
        {
            get => _operationsToday;
            set => SetProperty(ref _operationsToday, value);
        }

        public int ActiveFoldersCount
        {
            get => _activeFoldersCount;
            set => SetProperty(ref _activeFoldersCount, value);
        }

        public int ActiveRulesCount
        {
            get => _activeRulesCount;
            set => SetProperty(ref _activeRulesCount, value);
        }

        public string StorageSavedText
        {
            get => _storageSavedText;
            set => SetProperty(ref _storageSavedText, value);
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                if (SetProperty(ref _isMonitoring, value))
                {
                    _orchestrator.IsMonitoringPaused = !value;
                    StatusText = value ? "Active - Monitoring Folders" : "Paused";
                    OnPropertyChanged(nameof(ToggleMonitoringButtonText));
                }
            }
        }

        public string ToggleMonitoringButtonText => IsMonitoring ? "⏸ Pause Monitoring" : "▶ Resume Monitoring";

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ObservableCollection<LogEntry> RecentActivity { get; } = new();

        public ICommand ToggleMonitoringCommand { get; }
        public ICommand ScanFolderNowCommand { get; }
        public ICommand ClearActivityCommand { get; }

        public event Action? RequestScanFolder;

        public DashboardViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;

            ToggleMonitoringCommand = new RelayCommand(() => IsMonitoring = !IsMonitoring);
            ScanFolderNowCommand = new RelayCommand(() => RequestScanFolder?.Invoke());
            ClearActivityCommand = new RelayCommand(() =>
            {
                RecentActivity.Clear();
                _orchestrator.LogManager.ClearInMemoryLogs();
            });

            _orchestrator.StatsUpdated += RefreshStats;
            _orchestrator.LogManager.LogAdded += OnLogAdded;

            RefreshStats();
            LoadInitialLogs();
        }

        public void RefreshStats()
        {
            var config = _orchestrator.ConfigService.CurrentConfig;
            var stats = _orchestrator.LogManager.GetStatistics();

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                TotalFilesProcessed = _orchestrator.TotalFilesProcessed;
                OperationsToday = stats.OperationsToday;
                ActiveFoldersCount = config.WatchFolders.Count(w => w.Enabled);
                ActiveRulesCount = config.Rules.Count(r => r.Enabled);
                StorageSavedText = PathHelper.FormatFileSize(_orchestrator.TotalBytesSaved);
            });
        }

        private void LoadInitialLogs()
        {
            RecentActivity.Clear();
            var logs = _orchestrator.LogManager.GetRecentLogs(50);
            foreach (var log in logs)
            {
                RecentActivity.Add(log);
            }
        }

        private void OnLogAdded(LogEntry entry)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                RecentActivity.Insert(0, entry);
                if (RecentActivity.Count > 100)
                {
                    RecentActivity.RemoveAt(RecentActivity.Count - 1);
                }
                RefreshStats();
            });
        }
    }
}
