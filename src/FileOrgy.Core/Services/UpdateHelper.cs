using System;
using System.Collections.Generic;

namespace FileOrgy.Core.Services
{
    public static class UpdateHelper
    {
        public static Version NormalizeVersion(Version v)
        {
            int major = Math.Max(0, v.Major);
            int minor = Math.Max(0, v.Minor);
            int build = Math.Max(0, v.Build);
            return new Version(major, minor, build);
        }

        public static bool TryParseVersion(string? rawTag, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return false;
            }

            string clean = rawTag.Trim().TrimStart('v', 'V').Split('+')[0].Split('-')[0];
            if (Version.TryParse(clean, out var parsed))
            {
                version = NormalizeVersion(parsed);
                return true;
            }

            // Fallback for simple "1.0"
            if (Version.TryParse(clean + ".0", out var fallback))
            {
                version = NormalizeVersion(fallback);
                return true;
            }

            return false;
        }

        public static bool IsUpdateNewer(string currentVersionTag, string remoteVersionTag)
        {
            if (TryParseVersion(currentVersionTag, out var current) &&
                TryParseVersion(remoteVersionTag, out var remote))
            {
                return remote > current;
            }

            return false;
        }

        public static string? FindBestInstallerAssetName(IEnumerable<string> assetNames)
        {
            string? bestName = null;
            int bestScore = -1;

            foreach (var name in assetNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                int score = -1;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (name.StartsWith("FileOrgy-Setup-", StringComparison.OrdinalIgnoreCase))
                    {
                        score = 30;
                    }
                    else if (string.Equals(name, "FileOrgy-Setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        score = 20;
                    }
                    else
                    {
                        score = 10;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = name;
                }
            }

            return bestName;
        }
    }
}
