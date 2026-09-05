using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App.ViewModels
{
    public class WatchFolderItemViewModel : ViewModelBase
    {
        private readonly WatchFolderConfig _config;
        private readonly Action _onChanged;

        public string Id => _config.Id;

        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(_config.DisplayName) ? Path.GetFileName(_config.FolderPath) : _config.DisplayName;
            set
            {
                if (_config.DisplayName != value)
                {
                    _config.DisplayName = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public string FolderPath
        {
            get => _config.FolderPath;
            set
            {
                if (_config.FolderPath != value)
                {
                    _config.FolderPath = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public bool Enabled
        {
            get => _config.Enabled;
            set
            {
                if (_config.Enabled != value)
                {
                    _config.Enabled = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public bool IncludeSubdirectories
        {
            get => _config.IncludeSubdirectories;
            set
            {
                if (_config.IncludeSubdirectories != value)
                {
                    _config.IncludeSubdirectories = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public int DebounceDelayMs
        {
            get => _config.DebounceDelayMs;
            set
            {
                if (_config.DebounceDelayMs != value)
                {
                    _config.DebounceDelayMs = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public string IgnorePatternsText
        {
            get => string.Join(", ", _config.IgnorePatterns ?? new());
            set
            {
                _config.IgnorePatterns = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                OnPropertyChanged();
                _onChanged();
            }
        }

        public WatchFolderConfig Config => _config;

        public WatchFolderItemViewModel(WatchFolderConfig config, Action onChanged)
        {
            _config = config;
            _onChanged = onChanged;
        }
    }

    public class WatchFoldersViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private WatchFolderItemViewModel? _selectedFolder;

        public ObservableCollection<WatchFolderItemViewModel> Folders { get; } = new();

        public WatchFolderItemViewModel? SelectedFolder
        {
            get => _selectedFolder;
            set => SetProperty(ref _selectedFolder, value);
        }

        public ICommand AddFolderCommand { get; }
        public ICommand RemoveFolderCommand { get; }
        public ICommand ScanFolderCommand { get; }

        public WatchFoldersViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;

            AddFolderCommand = new RelayCommand(AddFolder);
            RemoveFolderCommand = new RelayCommand(RemoveSelectedFolder, () => SelectedFolder != null);
            ScanFolderCommand = new RelayCommand(ScanSelectedFolder, () => SelectedFolder != null);

            LoadFolders();
        }

        public void LoadFolders()
        {
            Folders.Clear();
            foreach (var folder in _orchestrator.ConfigService.CurrentConfig.WatchFolders)
            {
                Folders.Add(new WatchFolderItemViewModel(folder, SaveAndApply));
            }
            SelectedFolder = Folders.FirstOrDefault();
        }

        private void AddFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Folder to Monitor",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                string path = dialog.SelectedPath;

                // Check duplicate
                if (Folders.Any(f => string.Equals(f.FolderPath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    System.Windows.MessageBox.Show("This folder is already in the watch list.", "Duplicate Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var newConfig = new WatchFolderConfig
                {
                    FolderPath = path,
                    DisplayName = Path.GetFileName(path),
                    Enabled = true,
                    IncludeSubdirectories = false,
                    DebounceDelayMs = 1500
                };

                _orchestrator.ConfigService.CurrentConfig.WatchFolders.Add(newConfig);
                var vm = new WatchFolderItemViewModel(newConfig, SaveAndApply);
                Folders.Add(vm);
                SelectedFolder = vm;
                SaveAndApply();
            }
        }

        private void RemoveSelectedFolder()
        {
            if (SelectedFolder == null) return;

            var confirm = System.Windows.MessageBox.Show(
                $"Remove monitored folder '{SelectedFolder.DisplayName}'?",
                "Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirm == System.Windows.MessageBoxResult.Yes)
            {
                _orchestrator.ConfigService.CurrentConfig.WatchFolders.Remove(SelectedFolder.Config);
                Folders.Remove(SelectedFolder);
                SelectedFolder = Folders.FirstOrDefault();
                SaveAndApply();
            }
        }

        private async void ScanSelectedFolder()
        {
            if (SelectedFolder == null) return;
            await _orchestrator.ScanFolderNowAsync(SelectedFolder.Config);
        }

        private void SaveAndApply()
        {
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
            _orchestrator.ReloadConfiguration();
        }
    }
}
