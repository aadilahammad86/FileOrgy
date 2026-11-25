using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using Forms = System.Windows.Forms;

namespace FileOrgy.App.ViewModels
{
    public class WatchFolderItemViewModel : ViewModelBase
    {
        private readonly WatchFolderConfig _config;
        private readonly Action _onChanged;
        private string _newIgnorePattern = string.Empty;

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
                    OnPropertyChanged(nameof(FolderExistsOnDisk));
                    _onChanged();
                }
            }
        }

        public bool FolderExistsOnDisk => !string.IsNullOrWhiteSpace(_config.FolderPath) && Directory.Exists(_config.FolderPath);

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
                int clamped = Math.Clamp(value, 100, 60000);
                if (_config.DebounceDelayMs != clamped)
                {
                    _config.DebounceDelayMs = clamped;
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
                _config.IgnorePatterns = (value ?? string.Empty)
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                SyncIgnorePatternsCollection();
                OnPropertyChanged();
                _onChanged();
            }
        }

        public string NewIgnorePattern
        {
            get => _newIgnorePattern;
            set => SetProperty(ref _newIgnorePattern, value ?? string.Empty);
        }

        public ObservableCollection<string> IgnorePatternsCollection { get; } = new();

        public WatchFolderConfig Config => _config;

        public ICommand AddIgnorePatternCommand { get; }
        public ICommand RemoveIgnorePatternCommand { get; }
        public ICommand OpenInExplorerCommand { get; }
        public ICommand BrowseFolderPathCommand { get; }

        public WatchFolderItemViewModel(WatchFolderConfig config, Action onChanged)
        {
            _config = config;
            _onChanged = onChanged;
            _config.IgnorePatterns ??= new();

            SyncIgnorePatternsCollection();

            AddIgnorePatternCommand = new RelayCommand(AddIgnorePattern);
            RemoveIgnorePatternCommand = new RelayCommand(param =>
            {
                if (param is string pattern)
                {
                    RemoveIgnorePattern(pattern);
                }
            });
            OpenInExplorerCommand = new RelayCommand(OpenInExplorer);
            BrowseFolderPathCommand = new RelayCommand(BrowseFolderPath);
        }

        private void SyncIgnorePatternsCollection()
        {
            IgnorePatternsCollection.Clear();
            foreach (var p in _config.IgnorePatterns)
            {
                IgnorePatternsCollection.Add(p);
            }
        }

        private void AddIgnorePattern()
        {
            string pattern = NewIgnorePattern.Trim();
            if (string.IsNullOrEmpty(pattern)) return;

            if (!_config.IgnorePatterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
            {
                _config.IgnorePatterns.Add(pattern);
                IgnorePatternsCollection.Add(pattern);
                OnPropertyChanged(nameof(IgnorePatternsText));
                _onChanged();
            }

            NewIgnorePattern = string.Empty;
        }

        private void RemoveIgnorePattern(string pattern)
        {
            var match = _config.IgnorePatterns.FirstOrDefault(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _config.IgnorePatterns.Remove(match);
                IgnorePatternsCollection.Remove(match);
                OnPropertyChanged(nameof(IgnorePatternsText));
                _onChanged();
            }
        }

        private void OpenInExplorer()
        {
            try
            {
                if (Directory.Exists(FolderPath))
                {
                    Process.Start("explorer.exe", $"\"{FolderPath}\"");
                }
            }
            catch { }
        }

        private void BrowseFolderPath()
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select Monitored Folder Path",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(FolderPath) ? FolderPath : string.Empty
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                FolderPath = dialog.SelectedPath;
                if (string.IsNullOrWhiteSpace(_config.DisplayName))
                {
                    DisplayName = Path.GetFileName(dialog.SelectedPath);
                }
            }
        }
    }

    public class WatchFoldersViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly NotificationService? _notifications;
        private WatchFolderItemViewModel? _selectedFolder;

        public ObservableCollection<WatchFolderItemViewModel> Folders { get; } = new();

        public WatchFolderItemViewModel? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand AddFolderCommand { get; }
        public ICommand QuickAddKnownFolderCommand { get; }
        public ICommand RemoveFolderCommand { get; }
        public ICommand ScanFolderCommand { get; }

        public event Action? FoldersChanged;

        public WatchFoldersViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _notifications = notifications;

            AddFolderCommand = new RelayCommand(AddFolder);
            QuickAddKnownFolderCommand = new RelayCommand(param =>
            {
                if (param is string knownName)
                {
                    QuickAddKnownFolder(knownName);
                }
            });
            RemoveFolderCommand = new RelayCommand(
                async param => await RemoveFolderAsync(param as WatchFolderItemViewModel),
                param => (param as WatchFolderItemViewModel ?? SelectedFolder) != null);
            ScanFolderCommand = new RelayCommand(
                async param => await ScanFolderAsync(param as WatchFolderItemViewModel),
                param => (param as WatchFolderItemViewModel ?? SelectedFolder) != null);

            LoadFolders();
        }

        public void LoadFolders()
        {
            string? previousId = SelectedFolder?.Id;
            Folders.Clear();
            foreach (var folder in _orchestrator.ConfigService.CurrentConfig.WatchFolders)
            {
                Folders.Add(new WatchFolderItemViewModel(folder, SaveAndApply));
            }
            SelectedFolder = (!string.IsNullOrEmpty(previousId)
                ? Folders.FirstOrDefault(f => f.Id == previousId)
                : null) ?? Folders.FirstOrDefault();
            FoldersChanged?.Invoke();
        }

        private void AddFolder()
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select Folder to Monitor",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                AddFolderByPath(dialog.SelectedPath, Path.GetFileName(dialog.SelectedPath));
            }
        }

        private void QuickAddKnownFolder(string knownName)
        {
            string? resolvedPath = knownName.Trim().ToLowerInvariant() switch
            {
                "downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(resolvedPath) || !Directory.Exists(resolvedPath))
            {
                _notifications?.ShowToast(
                    "Folder Unavailable",
                    $"Could not locate the '{knownName}' folder on this user profile.",
                    ToastSeverity.Warning);
                return;
            }

            AddFolderByPath(resolvedPath, knownName);
        }

        private void AddFolderByPath(string path, string? displayName)
        {
            if (Folders.Any(f => string.Equals(f.FolderPath, path, StringComparison.OrdinalIgnoreCase)))
            {
                _notifications?.ShowToast(
                    "Already Monitored",
                    $"'{displayName ?? path}' is already in your monitored folders list.",
                    ToastSeverity.Info);
                return;
            }

            var newConfig = new WatchFolderConfig
            {
                FolderPath = path,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(path) : displayName,
                Enabled = true,
                IncludeSubdirectories = false,
                DebounceDelayMs = 1500
            };

            _orchestrator.ConfigService.CurrentConfig.WatchFolders.Add(newConfig);
            var vm = new WatchFolderItemViewModel(newConfig, SaveAndApply);
            Folders.Add(vm);
            SelectedFolder = vm;
            SaveAndApply();

            _notifications?.ShowToast(
                "Folder Added",
                $"Now monitoring '{vm.DisplayName}'.",
                ToastSeverity.Success);
        }

        private async Task RemoveFolderAsync(WatchFolderItemViewModel? target = null)
        {
            var folder = target ?? SelectedFolder;
            if (folder == null) return;

            if (_notifications != null)
            {
                bool confirmed = await _notifications.ConfirmAsync(
                    "Remove Watch Folder",
                    $"Stop monitoring '{folder.DisplayName}'? Files on disk will not be touched.",
                    confirmLabel: "Remove Folder",
                    cancelLabel: "Cancel",
                    isDestructive: true);

                if (!confirmed) return;
            }

            _orchestrator.ConfigService.CurrentConfig.WatchFolders.Remove(folder.Config);
            Folders.Remove(folder);
            SelectedFolder = Folders.FirstOrDefault();
            SaveAndApply();

            _notifications?.ShowToast(
                "Folder Removed",
                $"Stopped monitoring '{folder.DisplayName}'.",
                ToastSeverity.Info);
        }

        private async Task ScanFolderAsync(WatchFolderItemViewModel? target = null)
        {
            var folder = target ?? SelectedFolder;
            if (folder == null) return;

            if (!Directory.Exists(folder.FolderPath))
            {
                _notifications?.ShowToast(
                    "Folder Missing",
                    $"Directory '{folder.FolderPath}' does not exist on disk.",
                    ToastSeverity.Warning);
                return;
            }

            _notifications?.ShowToast(
                "Scanning Folder",
                $"Scanning '{folder.DisplayName}' against active rules...",
                ToastSeverity.Info);

            await _orchestrator.ScanFolderNowAsync(folder.Config);

            _notifications?.ShowToast(
                "Scan Complete",
                $"Finished scanning '{folder.DisplayName}'.",
                ToastSeverity.Success);
        }

        private void SaveAndApply()
        {
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
            _orchestrator.ReloadConfiguration();
            FoldersChanged?.Invoke();
        }
    }
}
