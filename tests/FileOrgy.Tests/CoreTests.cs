using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using FileOrgy.Core.Utils;
using Xunit;

namespace FileOrgy.Tests
{
    public class CoreTests : IDisposable
    {
        private readonly string _testTempDir;

        public CoreTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "FileOrgy_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void SmartRenamer_ReplacesStandardTokens()
        {
            string testFile = Path.Combine(_testTempDir, "SampleReport.txt");
            File.WriteAllText(testFile, "Test report body content");

            var context = new WorkflowContext(testFile);
            context.SetVariable("Client", "AcmeCorp");

            string template = "{var:Client}_{basename}_{date:yyyyMMdd}{dotext}";
            string renamed = SmartRenamer.EvaluateTemplate(template, context);

            string expectedDate = DateTime.Now.ToString("yyyyMMdd");
            Assert.Equal($"AcmeCorp_SampleReport_{expectedDate}.txt", renamed);
        }

        [Fact]
        public void SmartRenamer_AppliesCaseTransformation()
        {
            string input = "My Test Document Name.PDF";

            Assert.Equal("my test document name.pdf", SmartRenamer.ApplyCaseTransform(input, CaseTransform.LowerCase));
            Assert.Equal("MY TEST DOCUMENT NAME.PDF", SmartRenamer.ApplyCaseTransform(input, CaseTransform.UpperCase));
            Assert.Equal("my_test_document_name_pdf", SmartRenamer.ApplyCaseTransform(input, CaseTransform.SnakeCase));
            Assert.Equal("my-test-document-name-pdf", SmartRenamer.ApplyCaseTransform(input, CaseTransform.KebabCase));
        }

        [Fact]
        public void DateParserHelper_ExtractsIsoDate()
        {
            string text = "Order confirmation for purchase completed on 2026-09-05. Thank you.";
            var date = DateParserHelper.ExtractDateFromText(text);

            Assert.NotNull(date);
            Assert.Equal(2026, date.Value.Year);
            Assert.Equal(9, date.Value.Month);
            Assert.Equal(5, date.Value.Day);
        }

        [Fact]
        public void DateParserHelper_ExtractsPrefixedInvoiceDate()
        {
            string text = @"INVOICE #48921
Company: Tech Solutions Inc.
Invoice Date: October 15, 2025
Amount: $1,250.00";

            var date = DateParserHelper.ExtractDateFromText(text);

            Assert.NotNull(date);
            Assert.Equal(2025, date.Value.Year);
            Assert.Equal(10, date.Value.Month);
            Assert.Equal(15, date.Value.Day);
        }

        [Fact]
        public void ConditionEvaluator_MatchesKeywordList()
        {
            string filePath = Path.Combine(_testTempDir, "invoice_9981.pdf");
            File.WriteAllText(filePath, "Dummy content");

            var context = new WorkflowContext(filePath);
            var keywordLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["DocTypes"] = new List<string> { "invoice", "receipt", "statement" }
            };
            var globalVars = new Dictionary<string, string>();

            var condition = new RuleCondition
            {
                Target = RuleTarget.FileName,
                Operator = ConditionOperator.InKeywordList,
                KeywordListName = "DocTypes"
            };

            bool matches = ConditionEvaluator.EvaluateCondition(condition, context, keywordLists, globalVars);
            Assert.True(matches);
        }

        [Fact]
        public void ConditionEvaluator_EvaluatesRuleWithAllLogic()
        {
            string filePath = Path.Combine(_testTempDir, "quarterly_statement.pdf");
            File.WriteAllText(filePath, "Dummy content");

            var context = new WorkflowContext(filePath);
            var keywordLists = new Dictionary<string, List<string>>();
            var globalVars = new Dictionary<string, string>();

            var rule = new Rule
            {
                MatchLogic = ConditionMatchLogic.All,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "pdf" },
                    new RuleCondition { Target = RuleTarget.FileName, Operator = ConditionOperator.Contains, Value = "statement" }
                }
            };

            bool matches = ConditionEvaluator.EvaluateRule(rule, context, keywordLists, globalVars);
            Assert.True(matches);
        }

        [Fact]
        public async Task MultiStepWorkflow_ChainsDocumentInspectionRenameAndMove()
        {
            string sourceFolder = Path.Combine(_testTempDir, "Incoming");
            string destinationFolder = Path.Combine(_testTempDir, "Sorted");
            Directory.CreateDirectory(sourceFolder);
            Directory.CreateDirectory(destinationFolder);

            string fileToProcess = Path.Combine(sourceFolder, "raw_invoice.txt");
            string documentText = "ACME Corp\nInvoice Date: 2026-08-20\nInvoice Number: INV-9944\nTotal: $500.00";
            File.WriteAllText(fileToProcess, documentText);

            var context = new WorkflowContext(fileToProcess);

            var rule = new Rule
            {
                Name = "Invoice Multi-Step Pipeline",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepType = StepType.InspectDocument,
                        Name = "Inspect Document"
                    },
                    new WorkflowStep
                    {
                        StepType = StepType.RegexCapture,
                        Name = "Extract Invoice Number",
                        RegexSource = "Content",
                        RegexPattern = @"Invoice Number:\s*([A-Z0-9-]+)"
                    },
                    new WorkflowStep
                    {
                        StepType = StepType.SmartRename,
                        Name = "Rename Invoice",
                        RenamePattern = "{doc_date:yyyy-MM-dd}_{match:1}_{basename}{dotext}"
                    },
                    new WorkflowStep
                    {
                        StepType = StepType.MoveFile,
                        Name = "Route to Destination",
                        DestinationTemplate = Path.Combine(destinationFolder, "{doc_date:yyyy}")
                    }
                }
            };

            var result = await WorkflowEngine.ExecuteWorkflowAsync(rule, context);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(fileToProcess));

            string expectedTargetPath = Path.Combine(destinationFolder, "2026", "2026-08-20_INV-9944_raw_invoice.txt");
            Assert.True(File.Exists(expectedTargetPath), $"Expected file at {expectedTargetPath} but it was at {context.CurrentFilePath}");
        }

        [Fact]
        public void ArchiveExtractor_ExtractsZipArchive()
        {
            string zipPath = Path.Combine(_testTempDir, "archive_sample.zip");
            string extractDest = Path.Combine(_testTempDir, "Extracted");

            // Create a small zip archive
            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("nested/hello.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.WriteLine("Extracted successfully!");
            }

            Assert.True(ArchiveExtractor.IsSupportedArchive(zipPath));

            var result = ArchiveExtractor.Extract(zipPath, extractDest);

            Assert.True(result.IsSuccess);
            Assert.Single(result.ExtractedFiles);
            Assert.True(File.Exists(Path.Combine(extractDest, "nested", "hello.txt")));
        }

        [Fact]
        public void LogManager_LogsAndExports()
        {
            string customLog = Path.Combine(_testTempDir, "test_log.jsonl");
            var logManager = new LogManager(customLog);

            logManager.AddLog(new LogEntry
            {
                Level = LogLevel.Success,
                EventType = LogEventType.FileMoved,
                RuleName = "Test Rule",
                SourcePath = @"C:\test\source.txt",
                DestinationPath = @"C:\test\dest.txt",
                Details = "Move completed"
            });

            var stats = logManager.GetStatistics();
            Assert.True(stats.TotalOperations >= 1);

            string csvPath = Path.Combine(_testTempDir, "export.csv");
            logManager.ExportToCsv(csvPath);
            Assert.True(File.Exists(csvPath));
            string csvContent = File.ReadAllText(csvPath);
            Assert.Contains("Test Rule", csvContent);
        }

        [Fact]
        public void PathHelper_GeneratesUniqueConflictPath()
        {
            string testFile = Path.Combine(_testTempDir, "file.txt");
            File.WriteAllText(testFile, "first");

            string unique = PathHelper.GetUniqueFilePath(testFile);
            Assert.Equal(Path.Combine(_testTempDir, "file (1).txt"), unique);

            File.WriteAllText(unique, "second");
            string unique2 = PathHelper.GetUniqueFilePath(testFile);
            Assert.Equal(Path.Combine(_testTempDir, "file (2).txt"), unique2);
        }

        [Fact]
        public void PathHelper_DetectsProtectedSystemDirectories()
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

            Assert.True(PathHelper.IsSystemOrProtectedDirectory(winDir));
            Assert.True(PathHelper.IsSystemOrProtectedDirectory(sysDir));
            Assert.False(PathHelper.IsSystemOrProtectedDirectory(_testTempDir));
        }

        [Fact]
        public void WindowsRecycleBin_SendsFileToRecycleBinSuccessfully()
        {
            string testFile = Path.Combine(_testTempDir, "recycle_me.txt");
            File.WriteAllText(testFile, "temporary recycle test content");
            Assert.True(File.Exists(testFile));

            bool success = WindowsRecycleBin.SendToRecycleBin(testFile);
            Assert.True(success);
            Assert.False(File.Exists(testFile));
        }

        [Fact]
        public void SmartRenamer_ReplacesDirectoryTokens()
        {
            string subDir = Path.Combine(_testTempDir, "WatchedFolder");
            Directory.CreateDirectory(subDir);
            string testFile = Path.Combine(subDir, "report.pdf");
            File.WriteAllText(testFile, "pdf content");

            var context = new WorkflowContext(testFile);

            string dirResult = SmartRenamer.EvaluateTemplate(@"{directory}\Sorted\{filename}", context);
            string shortDirResult = SmartRenamer.EvaluateTemplate(@"{dir}\Sorted\{filename}", context);
            string folderResult = SmartRenamer.EvaluateTemplate(@"{folder}\Sorted\{filename}", context);

            string expected = Path.Combine(subDir, "Sorted", "report.pdf");
            Assert.Equal(expected, dirResult);
            Assert.Equal(expected, shortDirResult);
            Assert.Equal(expected, folderResult);
        }

        [Fact]
        public void ConfigService_InPlacePresets_LoadsExpectedRulesAndKeywords()
        {
            var config = new AppConfiguration();
            ConfigService.EnsureDefaultListsAndVariables(config);
            var presets = ConfigService.GetInPlaceOrganizationPresets();

            Assert.True(config.KeywordLists.ContainsKey("ProgramExts"));
            Assert.True(config.KeywordLists.ContainsKey("DocumentExts"));
            Assert.True(config.KeywordLists.ContainsKey("ArchiveExts"));
            Assert.True(config.KeywordLists.ContainsKey("PictureExts"));
            Assert.True(config.KeywordLists.ContainsKey("VideoExts"));
            Assert.True(config.KeywordLists.ContainsKey("AudioExts"));
            Assert.True(config.KeywordLists.ContainsKey("AllSortedExts"));

            Assert.Equal(7, presets.Count);
            Assert.All(presets, rule =>
            {
                Assert.Single(rule.Steps);
                Assert.Equal(StepType.MoveFile, rule.Steps[0].StepType);
                Assert.Contains("{directory}", rule.Steps[0].DestinationTemplate);
            });
        }

        [Fact]
        public async Task WorkflowEngine_ExecutesInPlaceDirectoryMove()
        {
            string watchFolder = Path.Combine(_testTempDir, "InPlaceSource");
            Directory.CreateDirectory(watchFolder);

            string invoiceFile = Path.Combine(watchFolder, "test_invoice.pdf");
            File.WriteAllText(invoiceFile, "invoice content");

            var context = new WorkflowContext(invoiceFile);

            var presets = ConfigService.GetInPlaceOrganizationPresets();
            var docRule = presets.First(r => r.Id == "rule-inplace-documents");

            var result = await WorkflowEngine.ExecuteWorkflowAsync(docRule, context);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(invoiceFile));

            string expectedPath = Path.Combine(watchFolder, "Documents", "test_invoice.pdf");
            Assert.True(File.Exists(expectedPath));
        }

        [Fact]
        public void ConditionEvaluator_MatchesExtensionWithAndWithoutLeadingDot()
        {
            string pdfPath = Path.Combine(_testTempDir, "document.pdf");
            File.WriteAllText(pdfPath, "PDF dummy");
            var context = new WorkflowContext(pdfPath);

            var condWithDot = new RuleCondition
            {
                Target = RuleTarget.Extension,
                Operator = ConditionOperator.Equals,
                Value = ".pdf"
            };

            var condWithoutDot = new RuleCondition
            {
                Target = RuleTarget.Extension,
                Operator = ConditionOperator.Equals,
                Value = "pdf"
            };

            var emptyKeywords = new Dictionary<string, List<string>>();
            var emptyVars = new Dictionary<string, string>();

            Assert.True(ConditionEvaluator.EvaluateCondition(condWithDot, context, emptyKeywords, emptyVars));
            Assert.True(ConditionEvaluator.EvaluateCondition(condWithoutDot, context, emptyKeywords, emptyVars));
        }

        [Fact]
        public void ConditionEvaluator_MatchesKeywordListFromValueProperty()
        {
            string exePath = Path.Combine(_testTempDir, "setup.exe");
            File.WriteAllText(exePath, "dummy");
            var context = new WorkflowContext(exePath);

            var keywords = new Dictionary<string, List<string>>
            {
                ["Installers"] = new List<string> { "exe", "msi" }
            };

            // Notice KeywordListName is not set, but Value is set to the list name
            var condition = new RuleCondition
            {
                Target = RuleTarget.Extension,
                Operator = ConditionOperator.InKeywordList,
                Value = "Installers"
            };

            Assert.True(ConditionEvaluator.EvaluateCondition(condition, context, keywords, new Dictionary<string, string>()));
        }

        [Fact]
        public void ConfigService_LoadInPlacePresets_SupportsOverwriteAndReindexing()
        {
            string configPath = Path.Combine(_testTempDir, "custom_config.json");
            var configService = new ConfigService(configPath);

            configService.CurrentConfig.Rules.Clear();

            configService.LoadInPlaceOrganizationPresets(overwriteExisting: false);
            Assert.Equal(7, configService.CurrentConfig.Rules.Count);

            // Modify a preset rule
            configService.CurrentConfig.Rules[0].Name = "Modified In-Place Rule";
            configService.SaveConfig(configService.CurrentConfig);

            // Load again with overwrite = false -> should retain modification
            configService.LoadInPlaceOrganizationPresets(overwriteExisting: false);
            Assert.Equal("Modified In-Place Rule", configService.CurrentConfig.Rules[0].Name);

            // Load with overwrite = true -> should reset back to standard
            configService.LoadInPlaceOrganizationPresets(overwriteExisting: true);
            Assert.Equal(7, configService.CurrentConfig.Rules.Count);
            Assert.NotEqual("Modified In-Place Rule", configService.CurrentConfig.Rules[0].Name);

            // Verify priorities are monotonically increasing from 1 to 7
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(i + 1, configService.CurrentConfig.Rules[i].Priority);
            }
        }

        [Fact]
        public async Task FileOrgyOrchestrator_ScanAllWatchFoldersAsync_ProcessesAllActiveFolders()
        {
            string configPath = Path.Combine(_testTempDir, "scan_all_config.json");
            var configService = new ConfigService(configPath);
            configService.CurrentConfig.Settings.ScanOnStartup = false; // Disable auto scan for test

            string watchDir1 = Path.Combine(_testTempDir, "watch1");
            string watchDir2 = Path.Combine(_testTempDir, "watch2");
            Directory.CreateDirectory(watchDir1);
            Directory.CreateDirectory(watchDir2);

            configService.CurrentConfig.WatchFolders.Clear();
            configService.CurrentConfig.WatchFolders.Add(new WatchFolderConfig
            {
                Id = "wf1",
                FolderPath = watchDir1,
                Enabled = true
            });
            configService.CurrentConfig.WatchFolders.Add(new WatchFolderConfig
            {
                Id = "wf2",
                FolderPath = watchDir2,
                Enabled = true
            });

            using var orchestrator = new FileOrgyOrchestrator(configService);

            // Create test files
            File.WriteAllText(Path.Combine(watchDir1, "test1.txt"), "content 1");
            File.WriteAllText(Path.Combine(watchDir2, "test2.txt"), "content 2");

            int detectedCount = 0;
            orchestrator.MonitorService.FileDetected += (s, e) => Interlocked.Increment(ref detectedCount);

            await orchestrator.ScanAllWatchFoldersAsync();

            Assert.Equal(2, detectedCount);
        }

        [Fact]
        public async Task InPlace_OtherFiles_Rule_SortsUnclassifiedFilesIntoOthersFolder()
        {
            string configPath = Path.Combine(_testTempDir, "others_config.json");
            var configService = new ConfigService(configPath);
            configService.CurrentConfig.Rules.Clear();
            configService.LoadInPlaceOrganizationPresets();

            string watchDir = Path.Combine(_testTempDir, "others_watch");
            Directory.CreateDirectory(watchDir);

            // Create files across each expanded category
            string docFile = Path.Combine(watchDir, "report.docx");
            string confFile = Path.Combine(watchDir, "settings.conf");
            string csvFile = Path.Combine(watchDir, "data.csv");
            string scriptFile = Path.Combine(watchDir, "install.ps1");
            string isoFile = Path.Combine(watchDir, "windows.iso");
            string tarFile = Path.Combine(watchDir, "backup.tar.gz");
            string webpFile = Path.Combine(watchDir, "banner.webp");
            string videoFile = Path.Combine(watchDir, "stream.m4v");
            string audioFile = Path.Combine(watchDir, "podcast.opus");

            // Create unclassified files that must go into Others
            string outFile = Path.Combine(watchDir, "firmware.out");
            string romFile = Path.Combine(watchDir, "bios.rom");
            string xyzFile = Path.Combine(watchDir, "test.xyz");
            string noExtFile = Path.Combine(watchDir, "LICENSE");

            File.WriteAllText(docFile, "doc content");
            File.WriteAllText(confFile, "conf content");
            File.WriteAllText(csvFile, "csv content");
            File.WriteAllText(scriptFile, "script content");
            File.WriteAllText(isoFile, "iso content");
            File.WriteAllText(tarFile, "tar content");
            File.WriteAllText(webpFile, "webp content");
            File.WriteAllText(videoFile, "video content");
            File.WriteAllText(audioFile, "audio content");
            File.WriteAllText(outFile, "out content");
            File.WriteAllText(romFile, "rom content");
            File.WriteAllText(xyzFile, "xyz content");
            File.WriteAllText(noExtFile, "license content");

            using var orchestrator = new FileOrgyOrchestrator(configService);

            await orchestrator.ProcessFileAsync(docFile);
            await orchestrator.ProcessFileAsync(confFile);
            await orchestrator.ProcessFileAsync(csvFile);
            await orchestrator.ProcessFileAsync(scriptFile);
            await orchestrator.ProcessFileAsync(isoFile);
            await orchestrator.ProcessFileAsync(tarFile);
            await orchestrator.ProcessFileAsync(webpFile);
            await orchestrator.ProcessFileAsync(videoFile);
            await orchestrator.ProcessFileAsync(audioFile);
            await orchestrator.ProcessFileAsync(outFile);
            await orchestrator.ProcessFileAsync(romFile);
            await orchestrator.ProcessFileAsync(xyzFile);
            await orchestrator.ProcessFileAsync(noExtFile);

            // Verify Documents
            Assert.True(File.Exists(Path.Combine(watchDir, "Documents", "report.docx")));
            Assert.True(File.Exists(Path.Combine(watchDir, "Documents", "settings.conf")));
            Assert.True(File.Exists(Path.Combine(watchDir, "Documents", "data.csv")));

            // Verify Programs
            Assert.True(File.Exists(Path.Combine(watchDir, "Programs", "install.ps1")));

            // Verify Compressed
            Assert.True(File.Exists(Path.Combine(watchDir, "Compressed", "windows.iso")));
            Assert.True(File.Exists(Path.Combine(watchDir, "Compressed", "backup.tar.gz")));

            // Verify Pictures
            Assert.True(File.Exists(Path.Combine(watchDir, "Pictures", "banner.webp")));

            // Verify Videos
            Assert.True(File.Exists(Path.Combine(watchDir, "Videos", "stream.m4v")));

            // Verify Audio
            Assert.True(File.Exists(Path.Combine(watchDir, "Audio", "podcast.opus")));

            // Verify Others (all files not in above sorting filters)
            Assert.True(File.Exists(Path.Combine(watchDir, "Others", "firmware.out")));
            Assert.True(File.Exists(Path.Combine(watchDir, "Others", "bios.rom")));
            Assert.True(File.Exists(Path.Combine(watchDir, "Others", "test.xyz")));
            Assert.True(File.Exists(Path.Combine(watchDir, "Others", "LICENSE")));
        }

        [Fact]
        public void ConditionEvaluator_EvaluatesKeywordList_WithKeywordListNameOrValue()
        {
            var keywordLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["DocumentExts"] = new List<string> { ".pdf", ".docx" },
                ["FinancialKeywords"] = new List<string> { "invoice", "receipt" }
            };
            var globalVars = new Dictionary<string, string>();

            // Test condition with KeywordListName populated and Value empty
            var cond1 = new RuleCondition
            {
                Target = RuleTarget.Extension,
                Operator = ConditionOperator.InKeywordList,
                KeywordListName = "DocumentExts",
                Value = string.Empty
            };

            var contextDoc = new WorkflowContext("invoice.pdf");
            Assert.True(ConditionEvaluator.EvaluateCondition(cond1, contextDoc, keywordLists, globalVars));

            var contextImg = new WorkflowContext("photo.jpg");
            Assert.False(ConditionEvaluator.EvaluateCondition(cond1, contextImg, keywordLists, globalVars));

            // Test condition with Value populated and KeywordListName empty
            var cond2 = new RuleCondition
            {
                Target = RuleTarget.Extension,
                Operator = ConditionOperator.InKeywordList,
                KeywordListName = null,
                Value = "DocumentExts"
            };
            Assert.True(ConditionEvaluator.EvaluateCondition(cond2, contextDoc, keywordLists, globalVars));

            // Test content keyword matching
            var condContent = new RuleCondition
            {
                Target = RuleTarget.ExtractedContent,
                Operator = ConditionOperator.InKeywordList,
                KeywordListName = "FinancialKeywords"
            };
            contextDoc.ExtractedContent = "Payment for Invoice #12345 received.";
            Assert.True(ConditionEvaluator.EvaluateCondition(condContent, contextDoc, keywordLists, globalVars));

            contextDoc.ExtractedContent = "Meeting notes from Monday morning.";
            Assert.False(ConditionEvaluator.EvaluateCondition(condContent, contextDoc, keywordLists, globalVars));
        }
    }
}


