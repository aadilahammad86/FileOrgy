using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using FileOrgy.Core.Utils;
using WpfApp = System.Windows.Application;

namespace FileOrgy.App.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly NotificationService? _notifications;
        private int _totalFilesProcessed;
        private int _operationsToday;
        private int _errorsTodayCount;
        private int _activeFoldersCount;
        private int _watchedFoldersTotalCount;
        private int _activeRulesCount;
        private int _totalRulesCount;
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

        public int ErrorsTodayCount
        {
            get => _errorsTodayCount;
            set => SetProperty(ref _errorsTodayCount, value);
        }

        public int ActiveFoldersCount
        {
            get => _activeFoldersCount;
            set => SetProperty(ref _activeFoldersCount, value);
        }

        public int WatchedFoldersTotalCount
        {
            get => _watchedFoldersTotalCount;
            set => SetProperty(ref _watchedFoldersTotalCount, value);
        }

        public int ActiveRulesCount
        {
            get => _activeRulesCount;
            set => SetProperty(ref _activeRulesCount, value);
        }

        public int TotalRulesCount
        {
            get => _totalRulesCount;
            set => SetProperty(ref _totalRulesCount, value);
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

        public string ToggleMonitoringButtonText => IsMonitoring ? "Pause Monitoring" : "Resume Monitoring";

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ObservableCollection<LogEntry> RecentActivity { get; } = new();
        public ObservableCollection<RuleItemViewModel> QuickRules { get; } = new();

        public ICommand ToggleMonitoringCommand { get; }
        public ICommand ScanFolderNowCommand { get; }
        public ICommand ScanMonitoredFoldersCommand { get; }
        public ICommand ScanCustomFolderCommand { get; }
        public ICommand ClearActivityCommand { get; }
        public ICommand NavigateToTabCommand { get; }

        public event Action? RequestScanFolder;
        public event Action? RequestScanMonitoredFolders;
        public event Action<NavigationTab>? RequestNavigateTab;
        public event Action? QuickRulesChanged;

        public DashboardViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _notifications = notifications;

            ToggleMonitoringCommand = new RelayCommand(() =>
            {
                IsMonitoring = !IsMonitoring;
                _notifications?.ShowToast(
                    IsMonitoring ? "Monitoring Resumed" : "Monitoring Paused",
                    IsMonitoring
                        ? "FileOrgy is actively watching your configured directories."
                        : "Real-time monitoring is paused.",
                    ToastSeverity.Info);
            });
            ScanMonitoredFoldersCommand = new RelayCommand(() => RequestScanMonitoredFolders?.Invoke());
            ScanCustomFolderCommand = new RelayCommand(() => RequestScanFolder?.Invoke());
            ScanFolderNowCommand = new RelayCommand(() => RequestScanMonitoredFolders?.Invoke());
            ClearActivityCommand = new RelayCommand(() =>
            {
                RecentActivity.Clear();
                _orchestrator.LogManager.ClearInMemoryLogs();
                RefreshStats();
                _notifications?.ShowToast("Activity Cleared", "In-memory operation feed cleared.", ToastSeverity.Info);
            });
            NavigateToTabCommand = new RelayCommand(param =>
            {
                if (param is NavigationTab tab)
                {
                    RequestNavigateTab?.Invoke(tab);
                }
                else if (param is string str && Enum.TryParse<NavigationTab>(str, true, out var parsed))
                {
                    RequestNavigateTab?.Invoke(parsed);
                }
            });

            _orchestrator.StatsUpdated += RefreshStats;
            _orchestrator.LogManager.LogAdded += OnLogAdded;

            RefreshStats();
            SyncQuickRules();
            LoadInitialLogs();
        }

        public void RefreshStats()
        {
            var config = _orchestrator.ConfigService.CurrentConfig;
            var stats = _orchestrator.LogManager.GetStatistics();
            int errorsToday = _orchestrator.LogManager.FilterLogs(null, LogLevel.Error, DateTime.Today, null).Count;

            void Apply()
            {
                TotalFilesProcessed = _orchestrator.TotalFilesProcessed;
                OperationsToday = stats.OperationsToday;
                ErrorsTodayCount = errorsToday;
                WatchedFoldersTotalCount = config.WatchFolders.Count;
                ActiveFoldersCount = config.WatchFolders.Count(w => w.Enabled);
                TotalRulesCount = config.Rules.Count;
                ActiveRulesCount = config.Rules.Count(r => r.Enabled);
                StorageSavedText = PathHelper.FormatFileSize(_orchestrator.TotalBytesSaved);
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

        public void SyncQuickRules(ObservableCollection<RuleItemViewModel>? sourceRules = null)
        {
            void Apply()
            {
                QuickRules.Clear();
                if (sourceRules != null)
                {
                    foreach (var r in sourceRules.Take(8))
                    {
                        QuickRules.Add(r);
                    }
                    return;
                }

                var sorted = _orchestrator.ConfigService.CurrentConfig.Rules
                    .OrderBy(r => r.Priority)
                    .Take(8);

                foreach (var rule in sorted)
                {
                    QuickRules.Add(new RuleItemViewModel(rule, () =>
                    {
                        _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
                        RefreshStats();
                        QuickRulesChanged?.Invoke();
                    }));
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
            void Apply()
            {
                RecentActivity.Insert(0, entry);
                if (RecentActivity.Count > 100)
                {
                    RecentActivity.RemoveAt(RecentActivity.Count - 1);
                }
                RefreshStats();
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
    }
}
