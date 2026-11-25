using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using FileOrgy.Core.Utils;
using WpfApp = System.Windows.Application;

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
        private readonly UpdateService _updateService = new();
        private readonly DispatcherTimer? _telemetryTimer;
        private NavigationTab _currentTab = NavigationTab.Dashboard;

        private bool _hasPendingUpdate;
        private string _pendingUpdateVersion = string.Empty;
        private UpdateInfo? _pendingUpdateInfo;
        private string _memoryUsageText = "0 MB RAM";
        private string _activeWatchSummaryText = "0 folders  •  0 rules";
        private StartupStatusInfo _startupStatus;
        private bool _isScanning;
        private bool _showQuickStartOnboarding;

        public NotificationService Notifications { get; }
        public PipelinePreviewViewModel PipelinePreviewVM { get; }
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
                    OnPropertyChanged(nameof(ActivePageTitle));
                    OnPropertyChanged(nameof(ActivePageSubtitle));
                    OnPropertyChanged(nameof(ShowGlobalStartupWarningBanner));
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

        public string ActivePageTitle => CurrentTab switch
        {
            NavigationTab.Dashboard => "Overview",
            NavigationTab.WatchFolders => "Watch Folders",
            NavigationTab.Rules => "Rules & Workflows",
            NavigationTab.Variables => "Variables & Keyword Lists",
            NavigationTab.Logs => "Activity History",
            NavigationTab.Settings => "Settings",
            _ => "Overview"
        };

        public string ActivePageSubtitle => CurrentTab switch
        {
            NavigationTab.Dashboard => "Real-time directory monitoring, pipeline simulator, and live telemetry",
            NavigationTab.WatchFolders => "Configure monitored directories, ignore patterns, and scan intervals",
            NavigationTab.Rules => "Build conditional rules and sequential multi-step automation pipelines",
            NavigationTab.Variables => "Manage custom template variables and reusable keyword match lists",
            NavigationTab.Logs => "Inspect automated file operations, errors, and export audit logs",
            NavigationTab.Settings => "Windows startup diagnostics, appearance theme, concurrency, and updates",
            _ => string.Empty
        };

        public string MemoryUsageText
        {
            get => _memoryUsageText;
            private set => SetProperty(ref _memoryUsageText, value);
        }

        public string ActiveWatchSummaryText
        {
            get => _activeWatchSummaryText;
            private set => SetProperty(ref _activeWatchSummaryText, value);
        }

        public StartupStatusInfo StartupStatus
        {
            get => _startupStatus;
            private set
            {
                if (SetProperty(ref _startupStatus, value))
                {
                    OnPropertyChanged(nameof(ShowStartupWarningBanner));
                    OnPropertyChanged(nameof(ShowGlobalStartupWarningBanner));
                }
            }
        }

        public bool ShowStartupWarningBanner => StartupStatus.HasDiagnosticWarning;
        public bool ShowGlobalStartupWarningBanner => StartupStatus.HasDiagnosticWarning && CurrentTab != NavigationTab.Settings;

        public bool IsDarkMode
        {
            get => SettingsVM.DarkMode;
            set
            {
                if (SettingsVM.DarkMode != value)
                {
                    SettingsVM.DarkMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool ShowQuickStartOnboarding
        {
            get => _showQuickStartOnboarding;
            private set => SetProperty(ref _showQuickStartOnboarding, value);
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
        public ICommand RepairStartupFromBannerCommand { get; }
        public ICommand OpenStartupSettingsCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand ScanMonitoredFoldersAsyncCommand { get; }
        public ICommand DismissOnboardingCommand { get; }
        public ICommand QuickSetupDefaultsCommand { get; }

        public event Action<Rule>? RequestEditRuleModal;
        public event Action? RequestScanAnyFolder;
        public event Action? RequestScanMonitoredFolders;
        public event Action<UpdateInfo, UpdateService>? RequestShowUpdateModal;

        public MainViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            Notifications = notifications ?? new NotificationService();

            PipelinePreviewVM = new PipelinePreviewViewModel(orchestrator, Notifications);
            DashboardVM = new DashboardViewModel(orchestrator, Notifications);
            WatchFoldersVM = new WatchFoldersViewModel(orchestrator, Notifications);
            RulesVM = new RulesViewModel(orchestrator, Notifications);
            VariablesVM = new VariablesViewModel(orchestrator, Notifications);
            LogsVM = new LogsViewModel(orchestrator, Notifications);
            SettingsVM = new SettingsViewModel(orchestrator, Notifications);

            _startupStatus = SettingsVM.StartupStatus;
            var initialConfig = orchestrator.ConfigService.CurrentConfig;
            _showQuickStartOnboarding = !initialConfig.Settings.HasCompletedOnboarding
                && initialConfig.WatchFolders.Count == 0
                && initialConfig.Rules.Count == 0;

            DashboardVM.SyncQuickRules(RulesVM.Rules);

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
            DashboardVM.RequestNavigateTab += tab => CurrentTab = tab;
            DashboardVM.QuickRulesChanged += () =>
            {
                RulesVM.NotifyRulesChanged();
                UpdateActiveWatchSummary();
            };

            RulesVM.RequestEditRule += rule => RequestEditRuleModal?.Invoke(rule);
            RulesVM.RulesChanged += () =>
            {
                DashboardVM.RefreshStats();
                DashboardVM.SyncQuickRules(RulesVM.Rules);
                UpdateActiveWatchSummary();
            };

            WatchFoldersVM.FoldersChanged += () =>
            {
                DashboardVM.RefreshStats();
                UpdateActiveWatchSummary();
            };

            SettingsVM.RequestShowUpdateDialog += (info, svc) => RequestShowUpdateModal?.Invoke(info, svc);
            SettingsVM.StartupStatusChanged += status => StartupStatus = status;
            SettingsVM.ThemeChanged += _ => OnPropertyChanged(nameof(IsDarkMode));

            _orchestrator.StatsUpdated += UpdateActiveWatchSummary;

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

            RepairStartupFromBannerCommand = new RelayCommand(() => SettingsVM.RepairStartup());
            OpenStartupSettingsCommand = new RelayCommand(() => CurrentTab = NavigationTab.Settings);
            ToggleThemeCommand = new RelayCommand(() => IsDarkMode = !IsDarkMode);
            ScanMonitoredFoldersAsyncCommand = new RelayCommand(
                async () => await ScanMonitoredFoldersAsync(),
                () => !IsScanning);
            DismissOnboardingCommand = new RelayCommand(DismissOnboarding);
            QuickSetupDefaultsCommand = new RelayCommand(QuickSetupDefaults);

            UpdateTelemetry();
            UpdateActiveWatchSummary();

            if (WpfApp.Current != null)
            {
                _telemetryTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                _telemetryTimer.Tick += (_, _) => UpdateTelemetry();
                _telemetryTimer.Start();
            }
        }

        private async Task ScanMonitoredFoldersAsync()
        {
            if (IsScanning) return;

            var activeFolders = _orchestrator.ConfigService.CurrentConfig.WatchFolders
                .Where(w => w.Enabled)
                .ToList();

            if (activeFolders.Count == 0)
            {
                Notifications.ShowToast(
                    "No Active Watch Folders",
                    "Enable or add at least one folder in Watch Folders to run a scan.",
                    ToastSeverity.Warning);
                CurrentTab = NavigationTab.WatchFolders;
                return;
            }

            IsScanning = true;
            try
            {
                Notifications.ShowToast(
                    "Scan Started",
                    $"Scanning {activeFolders.Count} monitored folder(s)...",
                    ToastSeverity.Info);

                await _orchestrator.ScanAllWatchFoldersAsync();

                Notifications.ShowToast(
                    "Scan Complete",
                    $"Finished scanning {activeFolders.Count} monitored folder(s).",
                    ToastSeverity.Success);
            }
            catch (Exception ex)
            {
                Notifications.ShowToast("Scan Failed", ex.Message, ToastSeverity.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void DismissOnboarding()
        {
            _orchestrator.ConfigService.CurrentConfig.Settings.HasCompletedOnboarding = true;
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
            ShowQuickStartOnboarding = false;
        }

        private void QuickSetupDefaults()
        {
            var config = _orchestrator.ConfigService.CurrentConfig;
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            if (Directory.Exists(downloads) &&
                !config.WatchFolders.Any(w => string.Equals(w.FolderPath, downloads, StringComparison.OrdinalIgnoreCase)))
            {
                config.WatchFolders.Add(new WatchFolderConfig
                {
                    Id = "downloads-folder",
                    DisplayName = "Downloads Folder",
                    FolderPath = downloads,
                    Enabled = true,
                    IncludeSubdirectories = false,
                    DebounceDelayMs = 1500
                });
            }

            _orchestrator.ConfigService.LoadInPlaceOrganizationPresets(overwriteExisting: false);
            config.Settings.HasCompletedOnboarding = true;
            _orchestrator.ConfigService.SaveConfig(config);
            _orchestrator.ReloadConfiguration();

            WatchFoldersVM.LoadFolders();
            RulesVM.LoadRules();
            DashboardVM.RefreshStats();
            DashboardVM.SyncQuickRules(RulesVM.Rules);
            UpdateActiveWatchSummary();
            ShowQuickStartOnboarding = false;

            Notifications.ShowToast(
                "Quick Setup Complete",
                "Downloads watch folder and 7 In-Place Organization presets are active.",
                ToastSeverity.Success);
        }

        private void UpdateTelemetry()
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                MemoryUsageText = $"{PathHelper.FormatFileSize(proc.WorkingSet64)} RAM";
            }
            catch
            {
                MemoryUsageText = "RAM unavailable";
            }
        }

        private void UpdateActiveWatchSummary()
        {
            var config = _orchestrator.ConfigService.CurrentConfig;
            int activeFolders = config.WatchFolders.Count(w => w.Enabled);
            int activeRules = config.Rules.Count(r => r.Enabled);
            string summary = $"{activeFolders} folder{(activeFolders == 1 ? "" : "s")}  •  {activeRules} rule{(activeRules == 1 ? "" : "s")}";

            var dispatcher = WpfApp.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                ActiveWatchSummaryText = summary;
            }
            else
            {
                dispatcher.Invoke(() => ActiveWatchSummaryText = summary);
            }
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
