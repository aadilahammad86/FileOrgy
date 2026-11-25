using System.Text.Json;
using FileOrgy.Core.Models;
using FileOrgy.Core.Utils;
using Xunit;

namespace FileOrgy.Tests
{
    public class StartupDiagnosticsParserTests
    {
        [Fact]
        public void ParseSchtasks_ExtractsPrincipalTaskToRunAndStatus()
        {
            const string sampleOutput = """
                Folder: \
                HostName:                             WORKSTATION01
                TaskName:                             \FileOrgy_ElevatedAutoStart
                Next Run Time:                        N/A
                Status:                               Ready
                Logon Mode:                           Interactive only
                Last Run Time:                        11/25/2026 8:00:00 AM
                Last Result:                          0
                Author:                               WORKSTATION01\Admin
                Task To Run:                          "C:\Program Files\FileOrgy\FileOrgy.exe" --tray
                Start In:                             N/A
                Run As User:                          WORKSTATION01\swalih
                """;

            Assert.Equal(@"WORKSTATION01\swalih", StartupDiagnosticsParser.ParseSchtasksPrincipal(sampleOutput));
            Assert.Equal("\"C:\\Program Files\\FileOrgy\\FileOrgy.exe\" --tray", StartupDiagnosticsParser.ParseSchtasksTaskToRun(sampleOutput));
            Assert.Equal("Ready", StartupDiagnosticsParser.ParseSchtasksStatus(sampleOutput));
        }

        [Fact]
        public void ParseSchtasks_HandlesMissingAndNAValues()
        {
            const string sampleOutput = """
                TaskName:                             \FileOrgy_ElevatedAutoStart
                Run As User:                          N/A
                """;

            Assert.Null(StartupDiagnosticsParser.ParseSchtasksPrincipal(sampleOutput));
            Assert.Null(StartupDiagnosticsParser.ParseSchtasksTaskToRun(sampleOutput));
            Assert.Null(StartupDiagnosticsParser.ParseSchtasksPrincipal(null));
        }

        [Fact]
        public void IsAccessDeniedAndNotFound_DetectExpectedSchtasksErrors()
        {
            Assert.True(StartupDiagnosticsParser.IsAccessDeniedMessage("ERROR: Access is denied."));
            Assert.True(StartupDiagnosticsParser.IsAccessDeniedMessage("Failed with HRESULT 0x80070005"));
            Assert.False(StartupDiagnosticsParser.IsAccessDeniedMessage("ERROR: The system cannot find the file specified."));
            Assert.False(StartupDiagnosticsParser.IsAccessDeniedMessage(null));

            Assert.True(StartupDiagnosticsParser.IsTaskNotFoundMessage("ERROR: The system cannot find the file specified."));
            Assert.False(StartupDiagnosticsParser.IsTaskNotFoundMessage("ERROR: Access is denied."));
        }

        [Fact]
        public void IsStartupApprovedDisabled_DetectsTaskManagerStateBytes()
        {
            byte[] enabledBytes = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] disabledBytesWin11 = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
            byte[] disabledOddByte = new byte[] { 0x01, 0x00, 0x00, 0x00 };

            Assert.False(StartupDiagnosticsParser.IsStartupApprovedDisabled(enabledBytes));
            Assert.True(StartupDiagnosticsParser.IsStartupApprovedDisabled(disabledBytesWin11));
            Assert.True(StartupDiagnosticsParser.IsStartupApprovedDisabled(disabledOddByte));
            Assert.False(StartupDiagnosticsParser.IsStartupApprovedDisabled(null));
            Assert.False(StartupDiagnosticsParser.IsStartupApprovedDisabled(System.Array.Empty<byte>()));
        }

        [Theory]
        [InlineData("\"C:\\Program Files\\FileOrgy\\FileOrgy.exe\" --tray", "C:\\Program Files\\FileOrgy\\FileOrgy.exe")]
        [InlineData("C:\\Program Files\\FileOrgy\\FileOrgy.exe --tray", "C:\\Program Files\\FileOrgy\\FileOrgy.exe")]
        [InlineData("\"C:\\Tools\\FileOrgy.exe\"", "C:\\Tools\\FileOrgy.exe")]
        [InlineData("C:\\Tools\\FileOrgy.exe", "C:\\Tools\\FileOrgy.exe")]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void ExtractExePathFromCommand_ParsesQuotedAndUnquotedCommands(string? input, string? expected)
        {
            Assert.Equal(expected, StartupDiagnosticsParser.ExtractExePathFromCommand(input));
        }

        [Fact]
        public void IsSameWindowsPrincipal_ComparesDomainAndShortUsersCorrectly()
        {
            Assert.True(StartupDiagnosticsParser.IsSameWindowsPrincipal(@"PC\sysadmin", @"pc\SYSADMIN"));
            Assert.True(StartupDiagnosticsParser.IsSameWindowsPrincipal(@"sysadmin", @"PC\sysadmin"));
            Assert.False(StartupDiagnosticsParser.IsSameWindowsPrincipal(@"PC\OtherAdmin", @"PC\sysadmin"));
            Assert.False(StartupDiagnosticsParser.IsSameWindowsPrincipal(null, @"PC\sysadmin"));
        }

        [Fact]
        public void AppSettings_BackwardCompatibleDeserialization_DefaultsHasCompletedOnboardingToFalse()
        {
            const string legacyConfigJson = """
                {
                  "Settings": {
                    "StartWithWindows": true,
                    "MinimizeToTrayOnClose": true,
                    "DarkMode": true,
                    "GlobalDebounceDelayMs": 1500
                  },
                  "WatchFolders": [],
                  "Rules": []
                }
                """;

            var config = JsonSerializer.Deserialize<AppConfiguration>(legacyConfigJson);

            Assert.NotNull(config);
            Assert.True(config!.Settings.StartWithWindows);
            Assert.True(config.Settings.DarkMode);
            Assert.Equal(1500, config.Settings.GlobalDebounceDelayMs);
            Assert.False(config.Settings.HasCompletedOnboarding);
        }
    }
}
