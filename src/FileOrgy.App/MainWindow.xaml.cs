using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using FileOrgy.App.Services;
using FileOrgy.App.ViewModels;
using FileOrgy.App.Views;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App
{
    public partial class MainWindow : Window
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly MainViewModel _viewModel;
        private readonly TrayService _trayService;
        private bool _isExplicitExit;

        public MainWindow()
        {
            InitializeComponent();
            InitializeIcon();

            _orchestrator = new FileOrgyOrchestrator();
            _viewModel = new MainViewModel(_orchestrator);
            DataContext = _viewModel;

            _trayService = new TrayService(this, _orchestrator);
            _trayService.OpenRequested += RestoreWindow;
            _trayService.ScanRequested += PromptScanFolder;
            _trayService.ExitRequested += ExitApplication;

            _viewModel.RequestEditRuleModal += OpenRuleEditor;
            _viewModel.RequestScanAnyFolder += PromptScanFolder;
        }

        private void InitializeIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/FileOrgy.App;component/assets/app.ico", UriKind.RelativeOrAbsolute);
                Icon = BitmapFrame.Create(iconUri);
                return;
            }
            catch
            {
                // Pack URI lookup fallback
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidatePaths =
                {
                    Path.Combine(baseDir, "assets", "app.ico"),
                    Path.Combine(baseDir, "app.ico")
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        Icon = BitmapFrame.Create(new Uri(path, UriKind.Absolute));
                        return;
                    }
                }
            }
            catch
            {
                // Silently fallback without crashing - ensures rock-solid startup
            }
        }

        private void OpenRuleEditor(Rule rule)
        {
            var editVm = new RuleEditViewModel(rule, _orchestrator.ConfigService);
            var dialog = new RuleEditDialog(editVm)
            {
                Owner = this,
                Icon = Icon
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.RulesVM.SaveAndApply();
                _viewModel.RulesVM.LoadRules();
            }
        }

        private async void PromptScanFolder()
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select Folder to Scan and Organize Immediately",
                UseDescriptionForTitle = true
            };

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                RestoreWindow();

                var existingWf = _orchestrator.ConfigService.CurrentConfig.WatchFolders
                    .FirstOrDefault(w => string.Equals(w.FolderPath, fbd.SelectedPath, StringComparison.OrdinalIgnoreCase));

                var scanConfig = new WatchFolderConfig
                {
                    Id = existingWf?.Id ?? Guid.NewGuid().ToString("N"),
                    FolderPath = fbd.SelectedPath,
                    DisplayName = existingWf?.DisplayName ?? System.IO.Path.GetFileName(fbd.SelectedPath),
                    IncludeSubdirectories = false, // Always root-only for manual scans
                    DebounceDelayMs = existingWf?.DebounceDelayMs ?? 100,
                    Enabled = true
                };

                await _orchestrator.ScanFolderNowAsync(scanConfig);
                _trayService.ShowNotification("Folder Scan Initiated", $"Scanning '{scanConfig.DisplayName}' root items for automation rules.");
            }
        }

        public async Task ScanDirectoryAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !System.IO.Directory.Exists(folderPath)) return;

            RestoreWindow();

            var existingWf = _orchestrator.ConfigService.CurrentConfig.WatchFolders
                .FirstOrDefault(w => string.Equals(w.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));

            var scanConfig = new WatchFolderConfig
            {
                Id = existingWf?.Id ?? Guid.NewGuid().ToString("N"),
                FolderPath = folderPath,
                DisplayName = existingWf?.DisplayName ?? System.IO.Path.GetFileName(folderPath),
                IncludeSubdirectories = false,
                DebounceDelayMs = existingWf?.DebounceDelayMs ?? 100,
                Enabled = true
            };

            await _orchestrator.ScanFolderNowAsync(scanConfig);
            _trayService.ShowNotification("Folder Scan Initiated", $"Scanning '{scanConfig.DisplayName}' root items for automation rules.");
        }

        public void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }

        public void ExitApplication()
        {
            _isExplicitExit = true;
            _trayService.Dispose();
            _orchestrator.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit && _orchestrator.ConfigService.CurrentConfig.Settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
                _trayService.ShowNotification("FileOrgy Running in Background", "FileOrgy continues monitoring folders silently in your system tray.");
            }
            else
            {
                base.OnClosing(e);
                _trayService.Dispose();
                _orchestrator.Dispose();
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && _orchestrator.ConfigService.CurrentConfig.Settings.MinimizeToTrayOnClose)
            {
                Hide();
            }
        }
    }
}