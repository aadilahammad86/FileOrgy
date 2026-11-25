using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FileOrgy.App.Services;
using FileOrgy.App.ViewModels;
using FileOrgy.App.Views;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace FileOrgy.App
{
    public partial class MainWindow : Window
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

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
            _trayService.ScanMonitoredRequested += ScanMonitoredFolders;
            _trayService.ExitRequested += ExitApplication;

            _viewModel.RequestEditRuleModal += OpenRuleEditor;
            _viewModel.RequestScanAnyFolder += PromptScanFolder;
            _viewModel.RequestScanMonitoredFolders += ScanMonitoredFolders;
            _viewModel.RequestShowUpdateModal += ShowUpdateDialog;

            SourceInitialized += OnWindowSourceInitialized;
            PreviewKeyDown += OnWindowPreviewKeyDown;

            Loaded += (_, _) =>
            {
                _ = _viewModel.CheckForUpdatesInBackgroundAsync();
                UpdateMaximizeChromeState();
            };
        }

        private void OnWindowSourceInitialized(object? sender, EventArgs e)
        {
            ThemeService.ApplyWindowDarkMode(this, ThemeService.IsDark);

            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT workArea = monitorInfo.rcWork;
                    RECT monitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.x = Math.Abs(workArea.Left - monitorArea.Left);
                    mmi.ptMaxPosition.y = Math.Abs(workArea.Top - monitorArea.Top);
                    mmi.ptMaxSize.x = Math.Abs(workArea.Right - workArea.Left);
                    mmi.ptMaxSize.y = Math.Abs(workArea.Bottom - workArea.Top);
                }
            }

            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice;
            double dpiX = transform?.M11 ?? 1.0;
            double dpiY = transform?.M22 ?? 1.0;

            mmi.ptMinTrackSize.x = (int)Math.Ceiling(MinWidth * dpiX);
            mmi.ptMinTrackSize.y = (int)Math.Ceiling(MinHeight * dpiY);

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _viewModel.Notifications.HasActiveConfirmation)
            {
                _viewModel.Notifications.ResolveConfirmation(false);
                e.Handled = true;
                return;
            }

            if (_viewModel.Notifications.HasActiveConfirmation)
            {
                return;
            }

            if (e.Key == Key.F5)
            {
                if (_viewModel.ScanMonitoredFoldersAsyncCommand.CanExecute(null))
                {
                    _viewModel.ScanMonitoredFoldersAsyncCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.D1:
                    case Key.NumPad1:
                        _viewModel.CurrentTab = NavigationTab.Dashboard;
                        e.Handled = true;
                        break;
                    case Key.D2:
                    case Key.NumPad2:
                        _viewModel.CurrentTab = NavigationTab.WatchFolders;
                        e.Handled = true;
                        break;
                    case Key.D3:
                    case Key.NumPad3:
                        _viewModel.CurrentTab = NavigationTab.Rules;
                        e.Handled = true;
                        break;
                    case Key.D4:
                    case Key.NumPad4:
                        _viewModel.CurrentTab = NavigationTab.Variables;
                        e.Handled = true;
                        break;
                    case Key.D5:
                    case Key.NumPad5:
                        _viewModel.CurrentTab = NavigationTab.Logs;
                        e.Handled = true;
                        break;
                    case Key.D6:
                    case Key.NumPad6:
                        _viewModel.CurrentTab = NavigationTab.Settings;
                        e.Handled = true;
                        break;
                    case Key.N:
                        _viewModel.CurrentTab = NavigationTab.Rules;
                        if (_viewModel.RulesVM.AddRuleCommand.CanExecute(null))
                        {
                            _viewModel.RulesVM.AddRuleCommand.Execute(null);
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnDismissToastClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.Tag is Guid id)
                {
                    _viewModel.Notifications.DismissToast(id);
                }
                else if (element.DataContext is ToastMessage toast)
                {
                    _viewModel.Notifications.DismissToast(toast.Id);
                }
            }
        }

        private void OnConfirmConfirmationClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Notifications.ResolveConfirmation(true);
        }

        private void OnCancelConfirmationClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Notifications.ResolveConfirmation(false);
        }

        private void UpdateMaximizeChromeState()
        {
            bool isMaximized = WindowState == WindowState.Maximized;
            ShellBorder.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(1);

            string iconKey = isMaximized ? "Icon.Restore" : "Icon.Maximize";
            if (TryFindResource(iconKey) is Geometry iconGeometry)
            {
                MaximizeRestoreIcon.Data = iconGeometry;
            }

            string label = isMaximized ? "Restore Window" : "Maximize Window";
            MaximizeRestoreButton.ToolTip = isMaximized ? "Restore" : "Maximize";
            AutomationProperties.SetName(MaximizeRestoreButton, label);
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
                _orchestrator.ReloadConfiguration();
                _viewModel.RulesVM.SaveAndApply();
                _viewModel.RulesVM.LoadRules();
                _viewModel.Notifications.ShowToast("Rule Saved", $"Updated '{rule.Name}'.", ToastSeverity.Success);
            }
        }

        private void ShowUpdateDialog(UpdateInfo info, UpdateService updateService)
        {
            var updateDialog = new UpdateDialog(info, updateService)
            {
                Owner = this,
                Icon = Icon
            };
            updateDialog.ShowDialog();
        }

        private async void PromptScanFolder()
        {
            using var fbd = new System.Windows.Forms.FolderBrowserDialog
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
                    DisplayName = existingWf?.DisplayName ?? Path.GetFileName(fbd.SelectedPath),
                    IncludeSubdirectories = false, // Always root-only for manual scans
                    DebounceDelayMs = existingWf?.DebounceDelayMs ?? 100,
                    Enabled = true
                };

                await _orchestrator.ScanFolderNowAsync(scanConfig);
                _viewModel.Notifications.ShowToast(
                    "Folder Scan Initiated",
                    $"Scanning '{scanConfig.DisplayName}' root items for automation rules.",
                    ToastSeverity.Info);
                _trayService.ShowNotification(
                    "Folder Scan Initiated",
                    $"Scanning '{scanConfig.DisplayName}' root items for automation rules.");
            }
        }

        private void ScanMonitoredFolders()
        {
            if (Dispatcher.CheckAccess())
            {
                if (_viewModel.ScanMonitoredFoldersAsyncCommand.CanExecute(null))
                {
                    _viewModel.ScanMonitoredFoldersAsyncCommand.Execute(null);
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    if (_viewModel.ScanMonitoredFoldersAsyncCommand.CanExecute(null))
                    {
                        _viewModel.ScanMonitoredFoldersAsyncCommand.Execute(null);
                    }
                });
            }
        }

        public async Task ScanDirectoryAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

            RestoreWindow();

            var existingWf = _orchestrator.ConfigService.CurrentConfig.WatchFolders
                .FirstOrDefault(w => string.Equals(w.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));

            var scanConfig = new WatchFolderConfig
            {
                Id = existingWf?.Id ?? Guid.NewGuid().ToString("N"),
                FolderPath = folderPath,
                DisplayName = existingWf?.DisplayName ?? Path.GetFileName(folderPath),
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
            UpdateMaximizeChromeState();

            if (WindowState == WindowState.Minimized && _orchestrator.ConfigService.CurrentConfig.Settings.MinimizeToTrayOnClose)
            {
                Hide();
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
    }
}
