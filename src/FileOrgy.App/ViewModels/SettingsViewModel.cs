using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly AppSettings _settings;
        private readonly UpdateService _updateService;
        private readonly NotificationService? _notifications;

        private StartupStatusInfo _startupStatus;
        private string _updateStatusText = "Up to date";
        private bool _isCheckingForUpdates;
        private bool _isUpdateAvailable;
        private UpdateInfo? _availableUpdate;

        public StartupStatusInfo StartupStatus
        {
            get => _startupStatus;
            private set => SetProperty(ref _startupStatus, value);
        }

        public bool StartWithWindows
        {
            get => _settings.StartWithWindows;
            set
            {
                if (_settings.StartWithWindows != value)
                {
                    var result = StartupHelper.ApplyStartupConfiguration(value, RunAsAdmin, startMinimized: true);
                    RefreshStartupStatus(notifyEvent: true);

                    if (result.Success)
                    {
                        _notifications?.ShowToast(
                            value ? "Startup Enabled" : "Startup Disabled",
                            result.Message,
                            ToastSeverity.Success);
                    }
                    else if (result.CancelledByUser)
                    {
                        _notifications?.ShowToast(
                            "Elevation Cancelled",
                            "Administrator approval is required to modify elevated startup settings.",
                            ToastSeverity.Warning);
                    }
                    else
                    {
                        _notifications?.ShowToast(
                            "Startup Update Failed",
                            result.Message,
                            ToastSeverity.Error);
                    }

                    SaveSettings();
                }
            }
        }

        public bool RunAsAdmin
        {
            get => StartupStatus.Mode == StartupMode.TaskSchedulerElevated || StartupStatus.HasRunAsAdminFlag;
            set
            {
                if (RunAsAdmin != value)
                {
                    var result = StartupHelper.ApplyStartupConfiguration(
                        enableStartup: StartWithWindows,
                        runAsAdmin: value,
                        startMinimized: true);
                    RefreshStartupStatus(notifyEvent: true);

                    if (result.Success)
                    {
                        _notifications?.ShowToast(
                            value ? "Elevated Mode Configured" : "Standard User Mode Configured",
                            result.Message,
                            ToastSeverity.Success);
                    }
                    else if (result.CancelledByUser)
                    {
                        _notifications?.ShowToast(
                            "UAC Cancelled",
                            "Run-as-administrator startup mode was not changed because elevation was cancelled.",
                            ToastSeverity.Warning);
                    }
                    else
                    {
                        _notifications?.ShowToast(
                            "Startup Mode Change Failed",
                            result.Message,
                            ToastSeverity.Error);
                    }

                    SaveSettings();
                }
            }
        }

        public bool DarkMode
        {
            get => _settings.DarkMode;
            set
            {
                if (_settings.DarkMode != value)
                {
                    _settings.DarkMode = value;
                    ThemeService.ApplyTheme(value);
                    OnPropertyChanged();
                    SaveSettings();
                    ThemeChanged?.Invoke(value);
                }
            }
        }

        public bool MinimizeToTrayOnClose
        {
            get => _settings.MinimizeToTrayOnClose;
            set
            {
                if (_settings.MinimizeToTrayOnClose != value)
                {
                    _settings.MinimizeToTrayOnClose = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool ShowDesktopNotifications
        {
            get => _settings.ShowDesktopNotifications;
            set
            {
                if (_settings.ShowDesktopNotifications != value)
                {
                    _settings.ShowDesktopNotifications = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool ScanOnStartup
        {
            get => _settings.ScanOnStartup;
            set
            {
                if (_settings.ScanOnStartup != value)
                {
                    _settings.ScanOnStartup = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool AutoCheckForUpdates
        {
            get => _settings.AutoCheckForUpdates;
            set
            {
                if (_settings.AutoCheckForUpdates != value)
                {
                    _settings.AutoCheckForUpdates = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public int GlobalDebounceDelayMs
        {
            get => _settings.GlobalDebounceDelayMs;
            set
            {
                int clamped = Math.Clamp(value, 100, 60000);
                if (_settings.GlobalDebounceDelayMs != clamped)
                {
                    _settings.GlobalDebounceDelayMs = clamped;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public int MaxConcurrentOperations
        {
            get => _settings.MaxConcurrentOperations;
            set
            {
                int clamped = Math.Clamp(value, 1, 8);
                if (_settings.MaxConcurrentOperations != clamped)
                {
                    _settings.MaxConcurrentOperations = clamped;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public int LogRetentionDays
        {
            get => _settings.LogRetentionDays;
            set
            {
                int clamped = Math.Clamp(value, 1, 3650);
                if (_settings.LogRetentionDays != clamped)
                {
                    _settings.LogRetentionDays = clamped;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string CurrentVersionText => UpdateService.GetCurrentVersionString();

        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value);
        }

        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            set => SetProperty(ref _isCheckingForUpdates, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetProperty(ref _isUpdateAvailable, value);
        }

        public UpdateInfo? AvailableUpdate
        {
            get => _availableUpdate;
            set => SetProperty(ref _availableUpdate, value);
        }

        public ICommand RepairStartupCommand { get; }
        public ICommand RefreshStartupStatusCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand ShowUpdateDialogCommand { get; }
        public ICommand OpenReleasesUrlCommand { get; }

        public event Action<UpdateInfo, UpdateService>? RequestShowUpdateDialog;
        public event Action<StartupStatusInfo>? StartupStatusChanged;
        public event Action<bool>? ThemeChanged;

        public SettingsViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _settings = orchestrator.ConfigService.CurrentConfig.Settings;
            _notifications = notifications;
            _updateService = new UpdateService();

            _startupStatus = StartupHelper.GetStartupStatus();
            _settings.StartWithWindows = _startupStatus.IsEnabled;

            UpdateStatusText = $"FileOrgy {CurrentVersionText}";

            RepairStartupCommand = new RelayCommand(RepairStartup);
            RefreshStartupStatusCommand = new RelayCommand(() => RefreshStartupStatus(notifyEvent: true));
            CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync(true));
            ShowUpdateDialogCommand = new RelayCommand(_ =>
            {
                if (AvailableUpdate != null)
                {
                    RequestShowUpdateDialog?.Invoke(AvailableUpdate, _updateService);
                }
            });
            OpenReleasesUrlCommand = new RelayCommand(_ =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = UpdateService.ReleasesWebUrl,
                        UseShellExecute = true
                    });
                }
                catch { }
            });
        }

        public void RefreshStartupStatus(bool notifyEvent = false)
        {
            StartupStatus = StartupHelper.GetStartupStatus();
            _settings.StartWithWindows = StartupStatus.IsEnabled;
            OnPropertyChanged(nameof(StartWithWindows));
            OnPropertyChanged(nameof(RunAsAdmin));
            if (notifyEvent)
            {
                StartupStatusChanged?.Invoke(StartupStatus);
            }
        }

        public void RepairStartup()
        {
            var result = StartupHelper.RepairStartup();
            RefreshStartupStatus(notifyEvent: true);
            SaveSettings();

            if (result.Success)
            {
                _notifications?.ShowToast(
                    "Startup Repaired",
                    result.Message,
                    ToastSeverity.Success);
            }
            else if (result.CancelledByUser)
            {
                _notifications?.ShowToast(
                    "Repair Cancelled",
                    "Administrator approval was cancelled.",
                    ToastSeverity.Warning);
            }
            else
            {
                _notifications?.ShowToast(
                    "Startup Repair Failed",
                    result.Message,
                    ToastSeverity.Error);
            }
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync(bool isManual = false)
        {
            if (IsCheckingForUpdates) return null;

            IsCheckingForUpdates = true;
            UpdateStatusText = "Checking repository for latest release...";

            try
            {
                var info = await _updateService.CheckForUpdatesAsync();
                if (!string.IsNullOrEmpty(info.ErrorMessage))
                {
                    UpdateStatusText = $"Check failed: {info.ErrorMessage}";
                    IsUpdateAvailable = false;
                    AvailableUpdate = null;
                    if (isManual)
                    {
                        _notifications?.ShowToast("Update Check Failed", info.ErrorMessage, ToastSeverity.Warning);
                    }
                    return info;
                }

                if (info.IsUpdateAvailable)
                {
                    UpdateStatusText = $"Update available: {info.LatestVersion} ({info.FormattedSize})";
                    IsUpdateAvailable = true;
                    AvailableUpdate = info;

                    if (isManual)
                    {
                        RequestShowUpdateDialog?.Invoke(info, _updateService);
                    }
                }
                else
                {
                    UpdateStatusText = $"FileOrgy is up to date ({info.CurrentVersion})";
                    IsUpdateAvailable = false;
                    AvailableUpdate = null;

                    if (isManual)
                    {
                        _notifications?.ShowToast(
                            "Up to Date",
                            $"FileOrgy {info.CurrentVersion} is the latest release available.",
                            ToastSeverity.Success);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Error: {ex.Message}";
                IsUpdateAvailable = false;
                AvailableUpdate = null;
                if (isManual)
                {
                    _notifications?.ShowToast("Update Check Error", ex.Message, ToastSeverity.Error);
                }
                return null;
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private void SaveSettings()
        {
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
        }
    }
}
