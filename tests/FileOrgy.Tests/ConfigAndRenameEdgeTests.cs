using System;
using System.Collections.Generic;
using System.IO;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using FileOrgy.Core.Utils;
using Xunit;

namespace FileOrgy.Tests
{
    public class ConfigAndRenameEdgeTests : IDisposable
    {
        private readonly string _tempDir;

        public ConfigAndRenameEdgeTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FileOrgy_CfgTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ConfigService_RoundTrips_HasCompletedOnboarding_And_RuleExtendedFields()
        {
            string cfgFile = Path.Combine(_tempDir, "config.json");
            var svc = new ConfigService(cfgFile);

            var cfg = svc.CurrentConfig;
            cfg.Settings.HasCompletedOnboarding = true;
            cfg.Settings.DarkMode = false;
            cfg.Rules.Clear();
            cfg.Rules.Add(new Rule
            {
                Id = "r-scoped",
                Name = "Scoped Snake Rule",
                ScopedFolderIds = new List<string> { "folder-a", "folder-b" },
                Conditions = new List<RuleCondition>
                {
                    new()
                    {
                        Target = RuleTarget.CustomVariable,
                        TargetVariable = "ClientCode",
                        Operator = ConditionOperator.Equals,
                        Value = "ACME"
                    }
                },
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        StepType = StepType.SmartRename,
                        RenamePattern = "{basename}{dotext}",
                        CaseTransform = CaseTransform.SnakeCase,
                        ContinueOnError = true
                    }
                }
            });

            svc.SaveConfig(cfg);

            var reloaded = new ConfigService(cfgFile).CurrentConfig;
            Assert.True(reloaded.Settings.HasCompletedOnboarding);
            Assert.False(reloaded.Settings.DarkMode);
            var rule = Assert.Single(reloaded.Rules);
            Assert.Equal(new[] { "folder-a", "folder-b" }, rule.ScopedFolderIds);
            Assert.Equal("ClientCode", rule.Conditions[0].TargetVariable);
            Assert.Equal(CaseTransform.SnakeCase, rule.Steps[0].CaseTransform);
            Assert.True(rule.Steps[0].ContinueOnError);
        }

        [Fact]
        public void SmartRenamer_AppliesSnakeCase_And_KebabCase_Cleanly()
        {
            Assert.Equal("monthly_invoice_2026", SmartRenamer.ApplyCaseTransform("Monthly Invoice 2026", CaseTransform.SnakeCase));
            Assert.Equal("monthly-invoice-2026", SmartRenamer.ApplyCaseTransform("Monthly Invoice 2026", CaseTransform.KebabCase));
        }

        [Fact]
        public void PipelineSimulator_RespectsScopedFolderIds_AcrossRules()
        {
            string folderA = Path.Combine(_tempDir, "Downloads");
            string folderB = Path.Combine(_tempDir, "Desktop");
            Directory.CreateDirectory(folderA);
            Directory.CreateDirectory(folderB);

            string fileInB = Path.Combine(folderB, "report.pdf");
            File.WriteAllText(fileInB, "sample");

            var cfg = new AppConfiguration
            {
                WatchFolders = new List<WatchFolderConfig>
                {
                    new() { Id = "wf-downloads", DisplayName = "Downloads", FolderPath = folderA, Enabled = true },
                    new() { Id = "wf-desktop", DisplayName = "Desktop", FolderPath = folderB, Enabled = true }
                },
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "r-dl-only",
                        Name = "Downloads PDF Only",
                        Enabled = true,
                        Priority = 1,
                        ScopedFolderIds = new List<string> { "wf-downloads" },
                        Conditions = new List<RuleCondition>
                        {
                            new() { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "pdf" }
                        },
                        Steps = new List<WorkflowStep>
                        {
                            new() { StepType = StepType.SmartRename, RenamePattern = "dl_{basename}{dotext}" }
                        }
                    }
                }
            };

            var report = PipelineSimulator.SimulateFile(fileInB, cfg);
            Assert.False(report.AnyRuleMatched);
        }
    }
}
