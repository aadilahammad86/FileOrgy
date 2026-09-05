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
    }
}
