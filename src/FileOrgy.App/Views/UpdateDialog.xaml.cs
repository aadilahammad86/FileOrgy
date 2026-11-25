using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using FileOrgy.App.Services;

namespace FileOrgy.App.Views
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;
        private CancellationTokenSource? _cts;
        private string? _downloadedInstallerPath;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateService = updateService;
            SourceInitialized += (_, _) => ThemeService.ApplyWindowDarkMode(this, ThemeService.IsDark);

            PopulateInfo();
        }

        private void PopulateInfo()
        {
            TxtTitle.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseTitle)
                ? $"FileOrgy {_updateInfo.LatestVersion} Available"
                : _updateInfo.ReleaseTitle;

            TxtCurrentVersion.Text = $"Current: {_updateInfo.CurrentVersion}";
            TxtLatestVersion.Text = $"Latest: {_updateInfo.LatestVersion}";
            TxtFileSize.Text = _updateInfo.FormattedSize;

            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes)
                ? "No release notes provided for this version."
                : _updateInfo.ReleaseNotes;

            if (string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
            {
                BtnDownloadAndInstall.IsEnabled = false;
                BtnDownloadAndInstall.Content = "No Direct Installer Available";
            }
        }

        private async void OnDownloadAndInstallClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_downloadedInstallerPath))
            {
                try
                {
                    _updateService.LaunchInstallerAndExit(_downloadedInstallerPath);
                    Close();
                }
                catch (Exception ex)
                {
                    ShowError($"Could not launch installer: {ex.Message}");
                }
                return;
            }

            InlineErrorBanner.Visibility = Visibility.Collapsed;
            InstallerReadyBanner.Visibility = Visibility.Collapsed;
            BtnDownloadAndInstall.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            ProgressSection.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();
            var progress = new Progress<UpdateDownloadProgress>(p =>
            {
                PbDownload.Value = p.ProgressPercentage;
                TxtProgressPercent.Text = $"{p.ProgressPercentage}%";
                TxtProgressStatus.Text = p.StatusText;
            });

            try
            {
                string installerPath = await _updateService.DownloadUpdateAsync(_updateInfo, progress, _cts.Token);
                _downloadedInstallerPath = installerPath;

                PbDownload.Value = 100;
                TxtProgressPercent.Text = "100%";
                TxtProgressStatus.Text = "Download complete.";

                TxtInstallerReady.Text = $"Update {_updateInfo.LatestVersion} downloaded and ready. Click 'Install & Restart FileOrgy' to close FileOrgy and run setup.";
                InstallerReadyBanner.Visibility = Visibility.Visible;

                BtnDownloadAndInstall.Content = "Install & Restart FileOrgy";
                BtnDownloadAndInstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                ProgressSection.Visibility = Visibility.Collapsed;
                BtnDownloadAndInstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ProgressSection.Visibility = Visibility.Collapsed;
                ShowError($"Failed to download update: {ex.Message} You can manually download the installer from GitHub.");
                BtnDownloadAndInstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        }

        private void OnViewOnGitHubClick(object sender, RoutedEventArgs e)
        {
            string url = string.IsNullOrWhiteSpace(_updateInfo.ReleaseUrl)
                ? UpdateService.ReleasesWebUrl
                : _updateInfo.ReleaseUrl;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError($"Could not open browser: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            TxtErrorBanner.Text = message;
            InlineErrorBanner.Visibility = Visibility.Visible;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }
    }
}
