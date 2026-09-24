using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App.ViewModels
{
    public enum NavigationTab
    {
        Dashboard,
        WatchFolders,
        Rules,
        Variables,
        Logs,
        Settings
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private NavigationTab _currentTab = NavigationTab.Dashboard;

        private bool _hasPendingUpdate;
        private string _pendingUpdateVersion = string.Empty;
        private UpdateInfo? _pendingUpdateInfo;
        private readonly UpdateService _updateService = new();

        public DashboardViewModel DashboardVM { get; }
        public WatchFoldersViewModel WatchFoldersVM { get; }
        public RulesViewModel RulesVM { get; }
        public VariablesViewModel VariablesVM { get; }
        public LogsViewModel LogsVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public NavigationTab CurrentTab
        {
            get => _currentTab;
            set
            {
                if (SetProperty(ref _currentTab, value))
                {
                    OnPropertyChanged(nameof(IsDashboardTab));
                    OnPropertyChanged(nameof(IsWatchFoldersTab));
                    OnPropertyChanged(nameof(IsRulesTab));
                    OnPropertyChanged(nameof(IsVariablesTab));
                    OnPropertyChanged(nameof(IsLogsTab));
                    OnPropertyChanged(nameof(IsSettingsTab));
                }
            }
        }

        public bool IsDashboardTab
        {
            get => CurrentTab == NavigationTab.Dashboard;
            set { if (value) CurrentTab = NavigationTab.Dashboard; }
        }

        public bool IsWatchFoldersTab
        {
            get => CurrentTab == NavigationTab.WatchFolders;
            set { if (value) CurrentTab = NavigationTab.WatchFolders; }
        }

        public bool IsRulesTab
        {
            get => CurrentTab == NavigationTab.Rules;
            set { if (value) CurrentTab = NavigationTab.Rules; }
        }

        public bool IsVariablesTab
        {
            get => CurrentTab == NavigationTab.Variables;
            set { if (value) CurrentTab = NavigationTab.Variables; }
        }

        public bool IsLogsTab
        {
            get => CurrentTab == NavigationTab.Logs;
            set { if (value) CurrentTab = NavigationTab.Logs; }
        }

        public bool IsSettingsTab
        {
            get => CurrentTab == NavigationTab.Settings;
            set { if (value) CurrentTab = NavigationTab.Settings; }
        }

        public bool HasPendingUpdate
        {
            get => _hasPendingUpdate;
            set => SetProperty(ref _hasPendingUpdate, value);
        }

        public string PendingUpdateVersion
        {
            get => _pendingUpdateVersion;
            set => SetProperty(ref _pendingUpdateVersion, value);
        }

        public UpdateInfo? PendingUpdateInfo
        {
            get => _pendingUpdateInfo;
            set => SetProperty(ref _pendingUpdateInfo, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand OpenPendingUpdateDialogCommand { get; }
        public ICommand DismissPendingUpdateNotificationCommand { get; }

        public event Action<Rule>? RequestEditRuleModal;
        public event Action? RequestScanAnyFolder;
        public event Action? RequestScanMonitoredFolders;
        public event Action<UpdateInfo, UpdateService>? RequestShowUpdateModal;

        public MainViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;

            DashboardVM = new DashboardViewModel(orchestrator);
            WatchFoldersVM = new WatchFoldersViewModel(orchestrator);
            RulesVM = new RulesViewModel(orchestrator);
            VariablesVM = new VariablesViewModel(orchestrator);
            LogsVM = new LogsViewModel(orchestrator);
            SettingsVM = new SettingsViewModel(orchestrator);

            NavigateCommand = new RelayCommand(param =>
            {
                if (param is NavigationTab tab)
                {
                    CurrentTab = tab;
                }
                else if (param is string str && Enum.TryParse<NavigationTab>(str, true, out var parsed))
                {
                    CurrentTab = parsed;
                }
            });

            DashboardVM.RequestScanFolder += () => RequestScanAnyFolder?.Invoke();
            DashboardVM.RequestScanMonitoredFolders += () => RequestScanMonitoredFolders?.Invoke();
            RulesVM.RequestEditRule += rule => RequestEditRuleModal?.Invoke(rule);

            SettingsVM.RequestShowUpdateDialog += (info, svc) => RequestShowUpdateModal?.Invoke(info, svc);

            OpenPendingUpdateDialogCommand = new RelayCommand(_ =>
            {
                if (PendingUpdateInfo != null)
                {
                    RequestShowUpdateModal?.Invoke(PendingUpdateInfo, _updateService);
                }
                else if (SettingsVM.AvailableUpdate != null)
                {
                    RequestShowUpdateModal?.Invoke(SettingsVM.AvailableUpdate, _updateService);
                }
            });

            DismissPendingUpdateNotificationCommand = new RelayCommand(_ =>
            {
                HasPendingUpdate = false;
            });
        }

        public async Task CheckForUpdatesInBackgroundAsync()
        {
            if (!_orchestrator.ConfigService.CurrentConfig.Settings.AutoCheckForUpdates)
            {
                return;
            }

            try
            {
                var update = await SettingsVM.CheckForUpdatesAsync(isManual: false);
                if (update != null && update.IsUpdateAvailable)
                {
                    HasPendingUpdate = true;
                    PendingUpdateVersion = update.LatestVersion;
                    PendingUpdateInfo = update;
                }
            }
            catch
            {
                // Background update check should never bubble exceptions
            }
        }
    }
}
