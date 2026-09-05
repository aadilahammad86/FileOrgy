using System;
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

        public SettingsViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
            _settings = orchestrator.ConfigService.CurrentConfig.Settings;

            // Sync startup registry state
            _settings.StartWithWindows = StartupHelper.IsStartupEnabled();
        }

        private void SaveSettings()
        {
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
        }
    }
}
