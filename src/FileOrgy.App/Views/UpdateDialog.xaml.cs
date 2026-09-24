using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using FileOrgy.App.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace FileOrgy.App.Views
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;
        private CancellationTokenSource? _cts;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateService = updateService;

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
                TxtProgressStatus.Text = "Download complete! Launching installer...";

                var result = WpfMessageBox.Show(
                    $"Update {_updateInfo.LatestVersion} has been downloaded successfully.\n\nFileOrgy will now close to run the setup installer. Click OK to continue.",
                    "Ready to Install Update",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    _updateService.LaunchInstallerAndExit(installerPath);
                }
                else
                {
                    BtnDownloadAndInstall.IsEnabled = true;
                    BtnCancel.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    $"Failed to download update:\n{ex.Message}\n\nYou can manually download the installer from GitHub.",
                    "Update Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ProgressSection.Visibility = Visibility.Collapsed;
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
                WpfMessageBox.Show($"Could not open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }
    }
}
