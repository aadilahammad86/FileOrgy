using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FileOrgy.Core.Services;

namespace FileOrgy.App.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseTitle { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public DateTime? PublishedAt { get; set; }
        public string? DownloadUrl { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? ErrorMessage { get; set; }

        public string FormattedSize => FileSize > 0
            ? $"{FileSize / (1024.0 * 1024.0):F1} MB"
            : "Unknown size";
    }

    public class UpdateDownloadProgress
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public int ProgressPercentage { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        public const string GitHubRepoOwner = "aadilahammad86";
        public const string GitHubRepoName = "FileOrgy";
        public const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";
        public const string ReleasesWebUrl = $"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static UpdateService()
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "FileOrgy-Updater/1.0");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            }
        }

        public static Version GetCurrentVersion()
        {
            var assembly = typeof(App).Assembly;

            // 1. Try InformationalVersion
            var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoAttr != null && !string.IsNullOrWhiteSpace(infoAttr.InformationalVersion))
            {
                if (UpdateHelper.TryParseVersion(infoAttr.InformationalVersion, out var v))
                {
                    return v;
                }
            }

            // 2. Try AssemblyVersion
            var asmVersion = assembly.GetName().Version;
            if (asmVersion != null)
            {
                return UpdateHelper.NormalizeVersion(asmVersion);
            }

            return new Version(1, 0, 0);
        }

        public static string GetCurrentVersionString()
        {
            var v = GetCurrentVersion();
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            var currentVersion = GetCurrentVersion();
            var updateInfo = new UpdateInfo
            {
                CurrentVersion = $"v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}"
            };

            try
            {
                using var response = await _httpClient.GetAsync(ReleasesApiUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    updateInfo.ErrorMessage = $"GitHub Releases API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                    return updateInfo;
                }

                var jsonStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: ct).ConfigureAwait(false);
                var root = doc.RootElement;

                string rawTag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
                string title = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                string notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : ReleasesWebUrl;

                DateTime? publishedAt = null;
                if (root.TryGetProperty("published_at", out var pubEl) && pubEl.TryGetDateTime(out var dt))
                {
                    publishedAt = dt;
                }

                if (!UpdateHelper.TryParseVersion(rawTag, out var latestVer))
                {
                    updateInfo.ErrorMessage = $"Could not parse remote release version: '{rawTag}'";
                    return updateInfo;
                }

                updateInfo.LatestVersion = $"v{latestVer.Major}.{latestVer.Minor}.{latestVer.Build}";
                updateInfo.ReleaseTitle = string.IsNullOrWhiteSpace(title) ? $"FileOrgy {updateInfo.LatestVersion}" : title;
                updateInfo.ReleaseNotes = notes;
                updateInfo.ReleaseUrl = htmlUrl;
                updateInfo.PublishedAt = publishedAt;

                // Compare versions
                updateInfo.IsUpdateAvailable = latestVer > currentVersion;

                // Find download asset
                if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
                {
                    var assetList = assetsEl.EnumerateArray().ToList();
                    var assetNames = assetList
                        .Select(a => a.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .Where(n => n != null)
                        .Cast<string>()
                        .ToList();

                    string? bestAssetName = UpdateHelper.FindBestInstallerAssetName(assetNames);
                    if (bestAssetName != null)
                    {
                        var matchingAsset = assetList.FirstOrDefault(a =>
                            a.TryGetProperty("name", out var n) && string.Equals(n.GetString(), bestAssetName, StringComparison.OrdinalIgnoreCase));

                        updateInfo.FileName = bestAssetName;
                        updateInfo.DownloadUrl = matchingAsset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() : null;
                        updateInfo.FileSize = matchingAsset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                    }
                }

                return updateInfo;
            }
            catch (Exception ex)
            {
                updateInfo.ErrorMessage = $"Update check failed: {ex.Message}";
                return updateInfo;
            }
        }

        public async Task<string> DownloadUpdateAsync(
            UpdateInfo updateInfo,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                throw new InvalidOperationException("No download URL available for this update.");
            }

            string fileName = updateInfo.FileName ?? $"FileOrgy-Setup-{updateInfo.LatestVersion}.exe";
            string tempDir = Path.Combine(Path.GetTempPath(), "FileOrgyUpdates");
            Directory.CreateDirectory(tempDir);
            string destinationPath = Path.Combine(tempDir, fileName);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;

            using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            var sw = Stopwatch.StartNew();
            long lastReportedTime = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                totalRead += bytesRead;

                if (sw.ElapsedMilliseconds - lastReportedTime > 100 || totalRead == totalBytes)
                {
                    lastReportedTime = sw.ElapsedMilliseconds;
                    int percent = totalBytes > 0 ? (int)((totalRead * 100) / totalBytes) : 0;

                    progress?.Report(new UpdateDownloadProgress
                    {
                        BytesReceived = totalRead,
                        TotalBytes = totalBytes,
                        ProgressPercentage = percent,
                        StatusText = totalBytes > 0
                            ? $"{totalRead / (1024.0 * 1024.0):F1} MB / {totalBytes / (1024.0 * 1024.0):F1} MB ({percent}%)"
                            : $"{totalRead / (1024.0 * 1024.0):F1} MB downloaded"
                    });
                }
            }

            return destinationPath;
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("Installer executable not found at specified path.", installerPath);
            }

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            Process.Start(psi);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                {
                    mw.ExitApplication();
                }
                else
                {
                    System.Windows.Application.Current.Shutdown();
                }
            });
        }
    }
}
