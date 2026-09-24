using System;
using System.Collections.Generic;
using FileOrgy.Core.Services;
using Xunit;

namespace FileOrgy.Tests
{
    public class UpdateHelperTests
    {
        [Theory]
        [InlineData("v1.0.6", 1, 0, 6)]
        [InlineData("1.0.6", 1, 0, 6)]
        [InlineData("V2.1.0", 2, 1, 0)]
        [InlineData("v1.0.6+commit123", 1, 0, 6)]
        [InlineData("v1.0.6-beta1", 1, 0, 6)]
        [InlineData("1.2", 1, 2, 0)]
        public void TryParseVersion_ValidTags_ParsesCorrectly(string tag, int major, int minor, int build)
        {
            bool success = UpdateHelper.TryParseVersion(tag, out var version);

            Assert.True(success);
            Assert.Equal(major, version.Major);
            Assert.Equal(minor, version.Minor);
            Assert.Equal(build, version.Build);
        }

        [Theory]
        [InlineData("v1.0.5", "v1.0.6", true)]
        [InlineData("1.0.5", "1.0.6", true)]
        [InlineData("1.0.6", "1.0.6", false)]
        [InlineData("v1.0.7", "v1.0.6", false)]
        [InlineData("1.1.0", "1.0.9", false)]
        [InlineData("1.0.9", "1.1.0", true)]
        public void IsUpdateNewer_ComparesCorrectly(string current, string remote, bool expectedNewer)
        {
            bool isNewer = UpdateHelper.IsUpdateNewer(current, remote);
            Assert.Equal(expectedNewer, isNewer);
        }

        [Fact]
        public void FindBestInstallerAssetName_PrefersVersionedSetupOverGeneric()
        {
            var assets = new List<string>
            {
                "FileOrgy-v1.0.6-portable.zip",
                "FileOrgy-Setup.exe",
                "FileOrgy-Setup-v1.0.6.exe",
                "SomeOtherTool.exe"
            };

            string? best = UpdateHelper.FindBestInstallerAssetName(assets);

            Assert.Equal("FileOrgy-Setup-v1.0.6.exe", best);
        }

        [Fact]
        public void FindBestInstallerAssetName_FallsBackToGenericSetup()
        {
            var assets = new List<string>
            {
                "FileOrgy-v1.0.6-portable.zip",
                "FileOrgy-Setup.exe"
            };

            string? best = UpdateHelper.FindBestInstallerAssetName(assets);

            Assert.Equal("FileOrgy-Setup.exe", best);
        }

        [Fact]
        public void FindBestInstallerAssetName_ReturnsNullIfNoExe()
        {
            var assets = new List<string>
            {
                "FileOrgy-v1.0.6-portable.zip",
                "checksums.txt"
            };

            string? best = UpdateHelper.FindBestInstallerAssetName(assets);

            Assert.Null(best);
        }
    }
}
