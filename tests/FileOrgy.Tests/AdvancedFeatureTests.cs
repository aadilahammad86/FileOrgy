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
    public class AdvancedFeatureTests : IDisposable
    {
        private readonly string _tempFolder;

        public AdvancedFeatureTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "FileOrgy_AdvTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempFolder))
                {
                    Directory.Delete(_tempFolder, true);
                }
            }
            catch { }
        }

        [Fact]
        public void DocumentInspector_ExtractsDocxContentAndDates()
        {
            string docxPath = Path.Combine(_tempFolder, "sample_contract.docx");

            // Build a valid minimal DOCX package in memory
            using (var zipStream = new FileStream(docxPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Add word/document.xml
                var docEntry = archive.CreateEntry("word/document.xml");
                using (var writer = new StreamWriter(docEntry.Open(), Encoding.UTF8))
                {
                    writer.Write("<w:document><w:body><w:p><w:r><w:t>Confidential Agreement dated September 12, 2025 between Party A and Party B.</w:t></w:r></w:p></w:body></w:document>");
                }

                // Add docProps/core.xml
                var coreEntry = archive.CreateEntry("docProps/core.xml");
                using (var writer = new StreamWriter(coreEntry.Open(), Encoding.UTF8))
                {
                    writer.Write("<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\"><dc:title>Sample Contract</dc:title><dcterms:created>2025-09-12T08:00:00Z</dcterms:created></cp:coreProperties>");
                }
            }

            var result = DocumentInspector.Inspect(docxPath);

            Assert.True(result.IsSuccess);
            Assert.Contains("Confidential Agreement", result.TextContent);
            Assert.Equal("Sample Contract", result.Title);
            Assert.NotNull(result.DocumentDate);
            Assert.Equal(2025, result.DocumentDate.Value.Year);
            Assert.Equal(9, result.DocumentDate.Value.Month);
            Assert.Equal(12, result.DocumentDate.Value.Day);
        }

        [Fact]
        public void DocumentInspector_ExtractsPdfContentAndDates()
        {
            string pdfPath = Path.Combine(_tempFolder, "real_invoice.pdf");

            var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            builder.DocumentInformation.Title = "Invoice #98124";
            var page = builder.AddPage(595, 842);
            var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
            page.AddText("Invoice Date: 2026-05-18", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
            page.AddText("Total Due: $1,450.00", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 680), font);

            byte[] pdfBytes = builder.Build();
            File.WriteAllBytes(pdfPath, pdfBytes);

            var result = DocumentInspector.Inspect(pdfPath);

            Assert.True(result.IsSuccess);
            Assert.Equal("Invoice #98124", result.Title);
            Assert.Contains("Total Due", result.TextContent);
            Assert.NotNull(result.DocumentDate);
            Assert.Equal(2026, result.DocumentDate.Value.Year);
            Assert.Equal(5, result.DocumentDate.Value.Month);
            Assert.Equal(18, result.DocumentDate.Value.Day);
        }

        [Fact]
        public void DocumentInspector_ExtractsXlsxSharedStrings()
        {
            string xlsxPath = Path.Combine(_tempFolder, "financial_summary.xlsx");

            using (var zipStream = new FileStream(xlsxPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var sharedEntry = archive.CreateEntry("xl/sharedStrings.xml");
                using (var writer = new StreamWriter(sharedEntry.Open(), Encoding.UTF8))
                {
                    writer.Write("<sst count=\"3\"><si><t>Revenue: $450,000</t></si><si><t>Q3 Profit</t></si><si><t>Invoice Date: 2026-03-31</t></si></sst>");
                }
            }

            var result = DocumentInspector.Inspect(xlsxPath);

            Assert.True(result.IsSuccess);
            Assert.Contains("Revenue: $450,000", result.TextContent);
            Assert.NotNull(result.DocumentDate);
            Assert.Equal(2026, result.DocumentDate.Value.Year);
            Assert.Equal(3, result.DocumentDate.Value.Month);
            Assert.Equal(31, result.DocumentDate.Value.Day);
        }

        [Fact]
        public void ArchiveExtractor_DetectsMultipartArchivesCorrectly()
        {
            Assert.True(ArchiveExtractor.IsSupportedArchive("package.zip"));
            Assert.True(ArchiveExtractor.IsSupportedArchive("backup.tar"));
            Assert.True(ArchiveExtractor.IsSupportedArchive("data.7z"));
            Assert.True(ArchiveExtractor.IsSupportedArchive("large_media.part01.rar"));
            Assert.True(ArchiveExtractor.IsSupportedArchive("large_media.part1.rar"));
            Assert.True(ArchiveExtractor.IsSupportedArchive("split.zip.001"));

            // Secondary volumes should not be treated as separate standalone archives
            Assert.False(ArchiveExtractor.IsSupportedArchive("large_media.part02.rar"));
            Assert.False(ArchiveExtractor.IsSupportedArchive("split.zip.002"));
            Assert.True(ArchiveExtractor.IsSecondaryMultipartVolume("large_media.part02.rar"));
            Assert.True(ArchiveExtractor.IsSecondaryMultipartVolume("split.zip.002"));
        }

        [Fact]
        public async Task CleanUpEngine_CleansOldFilesAndRemovesEmptyFolders()
        {
            string cleanupDir = Path.Combine(_tempFolder, "CleanupTarget");
            string subDir1 = Path.Combine(cleanupDir, "EmptySubDir");
            string subDir2 = Path.Combine(cleanupDir, "ActiveSubDir");
            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);

            string oldFile = Path.Combine(cleanupDir, "outdated.tmp");
            File.WriteAllText(oldFile, "temp content");
            // Set last write time to 40 days ago
            File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-40));

            string newFile = Path.Combine(subDir2, "active.txt");
            File.WriteAllText(newFile, "active content");

            var context = new WorkflowContext(oldFile);

            var step = new WorkflowStep
            {
                StepType = StepType.CleanupFolder,
                OlderThanDays = 30,
                RemoveEmptyFolders = true,
                SafeRecycle = true
            };

            var rule = new Rule
            {
                Name = "Test Cleanup",
                Steps = new List<WorkflowStep> { step }
            };

            var execResult = await WorkflowEngine.ExecuteWorkflowAsync(rule, context);

            Assert.True(execResult.IsSuccess);
            // Old file should be cleaned
            Assert.False(File.Exists(oldFile));
            // Empty subfolder should be deleted
            Assert.False(Directory.Exists(subDir1));
            // Non-empty subfolder with new file should remain
            Assert.True(Directory.Exists(subDir2));
            Assert.True(File.Exists(newFile));
        }

        [Fact]
        public void SmartRenamer_HandlesComplexTokenChaining()
        {
            string sampleFile = Path.Combine(_tempFolder, "my_scanned_receipt_123.pdf");
            File.WriteAllText(sampleFile, "Receipt text");

            var context = new WorkflowContext(sampleFile);
            context.ExtractedDate = new DateTime(2026, 4, 15);
            context.SetVariable("Vendor", "HomeDepot");
            context.SetVariable("regex_category", "Supplies");

            string template = "{upper:var:Vendor}_{regex:category}_{doc_date:yyyy-MM-dd}_{counter:001}{dotext}";
            string result = SmartRenamer.EvaluateTemplate(template, context, 5);

            Assert.Equal("HOMEDEPOT_Supplies_2026-04-15_005.pdf", result);
        }

        [Fact]
        public void WindowsRecycleBin_HandlesPermanentDeleteGracefully()
        {
            string testFile = Path.Combine(_tempFolder, "delete_me.txt");
            File.WriteAllText(testFile, "delete test");

            Assert.True(File.Exists(testFile));
            bool deleted = WindowsRecycleBin.PermanentDelete(testFile);

            Assert.True(deleted);
            Assert.False(File.Exists(testFile));
        }

        [Fact]
        public async Task FileSystemMonitorService_DetectsNewFileWithDebounce()
        {
            string watchDir = Path.Combine(_tempFolder, "WatchedDirectory");
            Directory.CreateDirectory(watchDir);

            using var monitor = new FileSystemMonitorService();
            var detectedFiles = new List<string>();
            var tcs = new TaskCompletionSource<string>();

            monitor.FileDetected += (s, e) =>
            {
                detectedFiles.Add(e.FilePath);
                tcs.TrySetResult(e.FilePath);
            };

            var config = new WatchFolderConfig
            {
                Id = "test-folder",
                FolderPath = watchDir,
                Enabled = true,
                DebounceDelayMs = 200 // Quick debounce for unit test
            };

            monitor.StartWatcher(config);

            // Create a new file in watched directory
            string testFilePath = Path.Combine(watchDir, "incoming_invoice.pdf");
            await File.WriteAllTextAsync(testFilePath, "Content");

            // Wait for debounced event with 5 second timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.Equal(tcs.Task, completedTask);

            string detected = await tcs.Task;
            Assert.Equal(testFilePath, detected);
        }
    }
}
