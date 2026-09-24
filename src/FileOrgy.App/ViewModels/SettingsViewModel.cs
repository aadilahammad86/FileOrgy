using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace FileOrgy.App.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly AppSettings _settings;
        private readonly UpdateService _updateService;

        private string _updateStatusText = "Up to date";
        private bool _isCheckingForUpdates;
        private bool _isUpdateAvailable;
        private UpdateInfo? _availableUpdate;

        public bool StartWithWindows
        {
            get => _settings.StartWithWindows;
            set
            {
                if (_settings.StartWithWindows != value)
                {
                    _settings.StartWithWindows = value;
                    StartupHelper.SetStartup(value);
                    OnPropertyChanged();
                    SaveSettings();
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
                if (_settings.GlobalDebounceDelayMs != value)
                {
                    _settings.GlobalDebounceDelayMs = value;
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
                if (_settings.MaxConcurrentOperations != value)
                {
                    _settings.MaxConcurrentOperations = value;
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
                if (_settings.LogRetentionDays != value)
                {
                    _settings.LogRetentionDays = value;
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

        public ICommand CheckForUpdatesCommand { get; }
        public ICommand ShowUpdateDialogCommand { get; }
        public ICommand OpenReleasesUrlCommand { get; }

        public event Action<UpdateInfo, UpdateService>? RequestShowUpdateDialog;

        public SettingsViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
            _settings = orchestrator.ConfigService.CurrentConfig.Settings;
            _updateService = new UpdateService();

            // Sync startup registry state
            _settings.StartWithWindows = StartupHelper.IsStartupEnabled();

            UpdateStatusText = $"FileOrgy {CurrentVersionText}";

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
                        WpfMessageBox.Show(
                            $"FileOrgy {info.CurrentVersion} is currently the latest version available.\nNo updates are needed at this time.",
                            "Up to Date",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Error: {ex.Message}";
                IsUpdateAvailable = false;
                AvailableUpdate = null;
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
