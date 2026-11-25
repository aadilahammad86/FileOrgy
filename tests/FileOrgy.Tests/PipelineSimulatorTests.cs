using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using Xunit;

namespace FileOrgy.Tests
{
    public class PipelineSimulatorTests : IDisposable
    {
        private readonly string _tempDir;

        public PipelineSimulatorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FileOrgy_SimTests_" + Guid.NewGuid().ToString("N"));
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
        public void SimulateFile_IsNonDestructive_AcrossDestructiveAndSideEffectSteps()
        {
            string sourceDir = Path.Combine(_tempDir, "Watch");
            Directory.CreateDirectory(sourceDir);

            string sampleFile = Path.Combine(sourceDir, "archive_bundle.zip");
            using (var fs = new FileStream(sampleFile, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("doc.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.WriteLine("hello");
            }

            string neverCreatedMoveDir = Path.Combine(_tempDir, "ShouldNotExist_Move");
            string neverCreatedExtractDir = Path.Combine(_tempDir, "ShouldNotExist_Extract");
            string neverCreatedMarker = Path.Combine(_tempDir, "cmd_ran.txt");

            var config = new AppConfiguration
            {
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "r1",
                        Name = "Full Pipeline Dry Run",
                        Enabled = true,
                        Priority = 1,
                        StopOnFirstMatch = false,
                        Conditions = new List<RuleCondition>
                        {
                            new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "zip" }
                        },
                        Steps = new List<WorkflowStep>
                        {
                            new WorkflowStep
                            {
                                StepType = StepType.ExtractArchive,
                                ExtractDestination = ArchiveExtractDestination.CustomFolder,
                                CustomExtractPath = neverCreatedExtractDir,
                                ArchivePostAction = ArchivePostAction.DeleteArchivePermanently
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.RunCommand,
                                CommandExecutable = "cmd.exe",
                                CommandArguments = $"/c echo ran > \"{neverCreatedMarker}\""
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.SmartRename,
                                RenamePattern = "renamed_{basename}{dotext}"
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.MoveFile,
                                DestinationTemplate = neverCreatedMoveDir
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.RecycleFile
                            }
                        }
                    }
                }
            };

            var report = PipelineSimulator.SimulateFile(sampleFile, config);

            Assert.True(report.AnyRuleMatched);
            Assert.True(report.WouldDeleteOrRecycle);
            Assert.Equal(5, report.ExecutedSteps.Count);
            Assert.All(report.ExecutedSteps, s => Assert.True(s.WouldSucceed));

            // Verify zero disk mutations occurred
            Assert.True(File.Exists(sampleFile));
            Assert.False(Directory.Exists(neverCreatedMoveDir));
            Assert.False(Directory.Exists(neverCreatedExtractDir));
            Assert.False(File.Exists(neverCreatedMarker));
        }

        [Fact]
        public void SimulateFile_ResolvesDocumentDateAndRegexCapturesInMemory()
        {
            string sourceDir = Path.Combine(_tempDir, "Docs");
            Directory.CreateDirectory(sourceDir);

            string invoiceFile = Path.Combine(sourceDir, "scan001.txt");
            File.WriteAllText(invoiceFile, "ACME Corp\nInvoice Date: 2026-08-20\nInvoice Number: INV-7721\nTotal: $99.00");

            string destRoot = Path.Combine(_tempDir, "SortedInvoices");

            var config = new AppConfiguration
            {
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "r-invoice",
                        Name = "Invoice Pipeline",
                        Enabled = true,
                        Priority = 1,
                        Conditions = new List<RuleCondition>
                        {
                            new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "txt" }
                        },
                        Steps = new List<WorkflowStep>
                        {
                            new WorkflowStep { StepType = StepType.InspectDocument, Name = "Inspect" },
                            new WorkflowStep
                            {
                                StepType = StepType.RegexCapture,
                                Name = "Capture Invoice ID",
                                RegexSource = "Content",
                                RegexPattern = @"Invoice Number:\s*(?<inv>[A-Z0-9-]+)"
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.SmartRename,
                                Name = "Rename",
                                RenamePattern = "{doc_date:yyyy-MM-dd}_{regex:inv}_{basename}{dotext}"
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.MoveFile,
                                Name = "Move",
                                DestinationTemplate = Path.Combine(destRoot, "{doc_date:yyyy}")
                            }
                        }
                    }
                }
            };

            var report = PipelineSimulator.SimulateFile(invoiceFile, config);

            Assert.True(report.AnyRuleMatched);
            Assert.Equal(new DateTime(2026, 8, 20), report.ExtractedDate);
            Assert.Equal("INV-7721", report.ResolvedVariables["regex_inv"]);
            Assert.Equal("2026-08-20_INV-7721_scan001.txt", report.FinalFileName);
            Assert.Equal(Path.Combine(destRoot, "2026", "2026-08-20_INV-7721_scan001.txt"), report.FinalFilePath);
            Assert.True(File.Exists(invoiceFile));
            Assert.False(Directory.Exists(destRoot));
        }

        [Fact]
        public void SimulateRule_ProvidesConditionBreakdownAndRespectsFolderScopeWithSeparatorNormalization()
        {
            string downloads = Path.Combine(_tempDir, "Downloads");
            string downloadsSibling = Path.Combine(_tempDir, "Downloads2");
            Directory.CreateDirectory(downloads);
            Directory.CreateDirectory(downloadsSibling);

            var config = new AppConfiguration
            {
                WatchFolders = new List<WatchFolderConfig>
                {
                    new WatchFolderConfig { Id = "wf-dl", FolderPath = downloads }
                },
                KeywordLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DocExts"] = new List<string> { "pdf", "docx" }
                }
            };

            var rule = new Rule
            {
                Id = "r-scoped",
                Name = "Scoped Rule",
                Enabled = true,
                ScopedFolderIds = new List<string> { "wf-dl" },
                MatchLogic = ConditionMatchLogic.All,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.InKeywordList, KeywordListName = "DocExts" },
                    new RuleCondition { Target = RuleTarget.FileName, Operator = ConditionOperator.Contains, Value = "invoice" }
                },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { StepType = StepType.MoveFile, DestinationTemplate = Path.Combine(downloads, "Invoices") }
                }
            };

            // 1. Inside scoped Downloads folder, 1 condition passes, 1 fails
            var partialFailEval = PipelineSimulator.SimulateRule(rule, Path.Combine(downloads, "notes.pdf"), config);
            Assert.True(partialFailEval.IsInFolderScope);
            Assert.False(partialFailEval.ConditionsMatched);
            Assert.False(partialFailEval.IsMatch);
            Assert.Equal(2, partialFailEval.ConditionResults.Count);
            Assert.True(partialFailEval.ConditionResults[0].Matched);
            Assert.False(partialFailEval.ConditionResults[1].Matched);

            // 2. Sibling folder "Downloads2" must NOT match "Downloads" scope
            var siblingEval = PipelineSimulator.SimulateRule(rule, Path.Combine(downloadsSibling, "invoice.pdf"), config);
            Assert.False(siblingEval.IsInFolderScope);
            Assert.False(siblingEval.IsMatch);

            // 3. Subdirectory inside Downloads DOES match scope and all conditions
            var matchEval = PipelineSimulator.SimulateRule(rule, Path.Combine(downloads, "Sub", "invoice.pdf"), config);
            Assert.True(matchEval.IsInFolderScope);
            Assert.True(matchEval.ConditionsMatched);
            Assert.True(matchEval.IsMatch);
            Assert.Equal(Path.Combine(downloads, "Invoices", "invoice.pdf"), matchEval.PredictedOutputPath);
        }

        [Fact]
        public void SimulateFile_HandlesMissingSamplePathAndInvalidRegexesGracefully()
        {
            string missingPath = Path.Combine(_tempDir, "NonExistent", "invoice_[draft].pdf");

            var config = new AppConfiguration
            {
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "r-bad-regex-cond",
                        Name = "Bad Regex Condition",
                        Enabled = true,
                        Priority = 1,
                        Conditions = new List<RuleCondition>
                        {
                            new RuleCondition { Target = RuleTarget.FileName, Operator = ConditionOperator.MatchesRegex, Value = "[unclosed(" }
                        }
                    },
                    new Rule
                    {
                        Id = "r-bad-regex-step",
                        Name = "Bad Regex Step",
                        Enabled = true,
                        Priority = 2,
                        Conditions = new List<RuleCondition>
                        {
                            new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "pdf" }
                        },
                        Steps = new List<WorkflowStep>
                        {
                            new WorkflowStep
                            {
                                StepType = StepType.RegexCapture,
                                RegexSource = "FileName",
                                RegexPattern = "(?<broken",
                                ContinueOnError = true
                            },
                            new WorkflowStep
                            {
                                StepType = StepType.SmartRename,
                                RenamePattern = "clean_{basename}{dotext}"
                            }
                        }
                    }
                }
            };

            var report = PipelineSimulator.SimulateFile(missingPath, config);

            Assert.False(report.FileExistsOnDisk);
            Assert.True(report.AnyRuleMatched);
            Assert.Equal("r-bad-regex-step", report.PrimaryMatchedRule?.RuleId);
            Assert.False(report.RuleEvaluations[0].IsMatch);
            Assert.Contains("invalid regex", report.RuleEvaluations[0].ConditionResults[0].Summary, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(2, report.ExecutedSteps.Count);
            Assert.False(report.ExecutedSteps[0].WouldSucceed);
            Assert.Contains("Invalid regex", report.ExecutedSteps[0].Details, StringComparison.OrdinalIgnoreCase);
            Assert.True(report.ExecutedSteps[1].WouldSucceed);
            Assert.Equal("clean_invoice_[draft].pdf", report.FinalFileName);
        }

        [Fact]
        public void SimulateFile_RespectsStopOnFirstMatchAndSkipsSecondaryMultipartVolumes()
        {
            var config = new AppConfiguration
            {
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "r1",
                        Name = "First Rule",
                        Priority = 1,
                        StopOnFirstMatch = true,
                        Conditions = new List<RuleCondition>
                        {
                            new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "pdf" }
                        },
                        Steps = new List<WorkflowStep>
                        {
                            new WorkflowStep { StepType = StepType.SmartRename, RenamePattern = "first_{filename}" }
                        }
                    },
                    new Rule
                    {
                        Id = "r2",
                        Name = "Second Rule",
                        Priority = 2,
                        Conditions = new List<RuleCondition>
                        {
                            new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "pdf" }
                        },
                        Steps = new List<WorkflowStep>
                        {
                            new WorkflowStep { StepType = StepType.SmartRename, RenamePattern = "second_{filename}" }
                        }
                    }
                }
            };

            var report = PipelineSimulator.SimulateFile(Path.Combine(_tempDir, "doc.pdf"), config);
            Assert.True(report.AnyRuleMatched);
            Assert.Equal(2, report.RuleEvaluations.Count);
            Assert.True(report.RuleEvaluations[0].IsMatch);
            Assert.False(report.RuleEvaluations[1].IsMatch);
            Assert.True(report.RuleEvaluations[1].WasSkippedDueToPriorMatch);
            Assert.Equal("first_doc.pdf", report.FinalFileName);

            var multipartReport = PipelineSimulator.SimulateFile(Path.Combine(_tempDir, "backup.part2.rar"), config);
            Assert.True(multipartReport.IsSecondaryMultipartVolume);
            Assert.False(multipartReport.AnyRuleMatched);
        }
    }
}
