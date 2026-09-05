using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileOrgy.Core.Models;
using FileOrgy.Core.Utils;

namespace FileOrgy.Core.Services
{
    public class StepExecutionResult
    {
        public WorkflowStep Step { get; set; } = null!;
        public bool IsSuccess { get; set; }
        public string? Details { get; set; }
        public long DurationMs { get; set; }
        public LogEventType EventType { get; set; }
    }

    public class WorkflowExecutionResult
    {
        public bool IsSuccess { get; set; }
        public Rule Rule { get; set; } = null!;
        public WorkflowContext Context { get; set; } = null!;
        public List<StepExecutionResult> StepResults { get; } = new();
        public List<string> ExtractedFilesToEnqueue { get; } = new();
    }

    public static class WorkflowEngine
    {
        public static async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
            Rule rule,
            WorkflowContext context,
            Action<LogEntry>? logCallback = null)
        {
            var swTotal = Stopwatch.StartNew();
            var result = new WorkflowExecutionResult
            {
                Rule = rule,
                Context = context,
                IsSuccess = true
            };

            logCallback?.Invoke(new LogEntry
            {
                Level = LogLevel.Info,
                EventType = LogEventType.RuleMatched,
                RuleName = rule.Name,
                SourcePath = context.CurrentFilePath,
                Details = $"Matched rule '{rule.Name}'. Starting {rule.Steps.Count} workflow steps."
            });

            foreach (var step in rule.Steps)
            {
                if (!step.Enabled) continue;
                if (context.IsAborted) break;

                var stepSw = Stopwatch.StartNew();
                var stepResult = new StepExecutionResult
                {
                    Step = step,
                    EventType = MapStepToEventType(step.StepType)
                };

                try
                {
                    switch (step.StepType)
                    {
                        case StepType.InspectDocument:
                            ExecuteInspectDocument(step, context, stepResult);
                            break;

                        case StepType.ExtractDate:
                            ExecuteExtractDate(step, context, stepResult);
                            break;

                        case StepType.RegexCapture:
                            ExecuteRegexCapture(step, context, stepResult);
                            break;

                        case StepType.SmartRename:
                            ExecuteSmartRename(step, context, stepResult);
                            break;

                        case StepType.MoveFile:
                            ExecuteMoveFile(step, context, stepResult);
                            break;

                        case StepType.CopyFile:
                            ExecuteCopyFile(step, context, stepResult);
                            break;

                        case StepType.ExtractArchive:
                            ExecuteExtractArchive(step, context, stepResult, result.ExtractedFilesToEnqueue);
                            break;

                        case StepType.RecycleFile:
                            ExecuteRecycleFile(step, context, stepResult);
                            break;

                        case StepType.PermanentDelete:
                            ExecutePermanentDelete(step, context, stepResult);
                            break;

                        case StepType.CleanupFolder:
                            ExecuteCleanupFolder(step, context, stepResult);
                            break;

                        case StepType.RunCommand:
                            await ExecuteRunCommandAsync(step, context, stepResult);
                            break;
                    }

                    stepSw.Stop();
                    stepResult.DurationMs = stepSw.ElapsedMilliseconds;

                    logCallback?.Invoke(new LogEntry
                    {
                        Level = stepResult.IsSuccess ? LogLevel.Success : (step.ContinueOnError ? LogLevel.Warning : LogLevel.Error),
                        EventType = stepResult.EventType,
                        RuleName = rule.Name,
                        StepName = string.IsNullOrEmpty(step.Name) ? step.StepType.ToString() : step.Name,
                        SourcePath = context.OriginalFilePath,
                        DestinationPath = context.CurrentFilePath,
                        Details = stepResult.Details ?? "Completed successfully",
                        IsSuccess = stepResult.IsSuccess,
                        DurationMs = stepResult.DurationMs
                    });

                    result.StepResults.Add(stepResult);

                    if (!stepResult.IsSuccess && !step.ContinueOnError)
                    {
                        result.IsSuccess = false;
                        context.IsAborted = true;
                        context.AbortReason = stepResult.Details;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    stepSw.Stop();
                    stepResult.IsSuccess = false;
                    stepResult.DurationMs = stepSw.ElapsedMilliseconds;
                    stepResult.Details = $"Error: {ex.Message}";

                    logCallback?.Invoke(new LogEntry
                    {
                        Level = LogLevel.Error,
                        EventType = LogEventType.ErrorOccurred,
                        RuleName = rule.Name,
                        StepName = step.Name,
                        SourcePath = context.CurrentFilePath,
                        Details = ex.Message,
                        IsSuccess = false
                    });

                    result.StepResults.Add(stepResult);

                    if (!step.ContinueOnError)
                    {
                        result.IsSuccess = false;
                        context.IsAborted = true;
                        context.AbortReason = ex.Message;
                        break;
                    }
                }
            }

            swTotal.Stop();
            return result;
        }

        private static void ExecuteInspectDocument(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = false;
                result.Details = "File does not exist for inspection";
                return;
            }

            var inspection = DocumentInspector.Inspect(context.CurrentFilePath);
            context.ExtractedContent = inspection.TextContent;

            if (inspection.DocumentDate.HasValue)
            {
                context.ExtractedDate = inspection.DocumentDate;
            }

            if (!string.IsNullOrEmpty(inspection.Title))
            {
                context.SetVariable("doc_title", inspection.Title);
            }
            if (!string.IsNullOrEmpty(inspection.Author))
            {
                context.SetVariable("doc_author", inspection.Author);
            }

            result.IsSuccess = inspection.IsSuccess;
            result.Details = inspection.IsSuccess
                ? $"Extracted {inspection.TextContent.Length} chars text, date: {context.ExtractedDate:yyyy-MM-dd}"
                : inspection.ErrorMessage;
        }

        private static void ExecuteExtractDate(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (context.ExtractedDate.HasValue)
            {
                result.IsSuccess = true;
                result.Details = $"Date already extracted: {context.ExtractedDate:yyyy-MM-dd}";
                return;
            }

            if (string.IsNullOrEmpty(context.ExtractedContent) && File.Exists(context.CurrentFilePath))
            {
                var inspection = DocumentInspector.Inspect(context.CurrentFilePath);
                context.ExtractedContent = inspection.TextContent;
                context.ExtractedDate = inspection.DocumentDate;
            }

            if (context.ExtractedDate.HasValue)
            {
                result.IsSuccess = true;
                result.Details = $"Extracted date: {context.ExtractedDate:yyyy-MM-dd}";
            }
            else
            {
                context.ExtractedDate = context.CreatedDate;
                result.IsSuccess = true;
                result.Details = $"Document date not found; defaulted to file creation date: {context.ExtractedDate:yyyy-MM-dd}";
            }
        }

        private static void ExecuteRegexCapture(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (string.IsNullOrWhiteSpace(step.RegexPattern))
            {
                result.IsSuccess = false;
                result.Details = "Regex pattern is empty";
                return;
            }

            string sourceText = step.RegexSource switch
            {
                "FileName" => context.FileName,
                "FullPath" => context.CurrentFilePath,
                _ => context.ExtractedContent ?? string.Empty
            };

            var match = Regex.Match(sourceText, step.RegexPattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                result.IsSuccess = false;
                result.Details = $"Regex pattern did not match in {step.RegexSource}";
                return;
            }

            // Store indexed matches and named groups
            for (int i = 0; i < match.Groups.Count; i++)
            {
                context.SetVariable($"match_{i}", match.Groups[i].Value);
            }

            foreach (var groupName in match.Groups.Keys)
            {
                if (!int.TryParse(groupName, out _))
                {
                    context.SetVariable(groupName, match.Groups[groupName].Value);
                    context.SetVariable("regex_" + groupName, match.Groups[groupName].Value);
                }
            }

            result.IsSuccess = true;
            result.Details = $"Captured {match.Groups.Count} regex groups";
        }

        private static void ExecuteSmartRename(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = false;
                result.Details = "Source file does not exist for renaming";
                return;
            }

            string rawNewName = SmartRenamer.EvaluateTemplate(step.RenamePattern, context);
            rawNewName = SmartRenamer.ApplyCaseTransform(rawNewName, step.CaseTransform);
            rawNewName = PathHelper.SanitizeFileName(rawNewName);

            // Ensure extension is preserved if template didn't specify one
            if (!Path.HasExtension(rawNewName) && !string.IsNullOrEmpty(context.Extension))
            {
                rawNewName += context.Extension;
            }

            string currentDir = context.DirectoryPath;
            string targetPath = Path.Combine(currentDir, rawNewName);

            if (string.Equals(context.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                result.IsSuccess = true;
                result.Details = "Filename is already up to date";
                return;
            }

            string? resolvedPath = ResolveDestinationConflict(targetPath, step.ConflictResolution, context.CurrentFilePath);
            if (resolvedPath == null)
            {
                result.IsSuccess = true;
                result.Details = "Rename skipped due to conflict resolution";
                return;
            }

            File.Move(context.CurrentFilePath, resolvedPath);
            context.CurrentFilePath = resolvedPath;

            result.IsSuccess = true;
            result.Details = $"Renamed to '{Path.GetFileName(resolvedPath)}'";
        }

        private static void ExecuteMoveFile(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = false;
                result.Details = "Source file does not exist for moving";
                return;
            }

            string targetDir = SmartRenamer.EvaluateTemplate(step.DestinationTemplate, context);
            targetDir = Path.GetFullPath(targetDir);
            Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, context.FileName);
            if (string.Equals(context.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                result.IsSuccess = true;
                result.Details = "File is already in target destination";
                return;
            }

            string? resolvedPath = ResolveDestinationConflict(targetPath, step.ConflictResolution, context.CurrentFilePath);
            if (resolvedPath == null)
            {
                result.IsSuccess = true;
                result.Details = "Move skipped due to conflict resolution";
                return;
            }

            File.Move(context.CurrentFilePath, resolvedPath, true);
            context.CurrentFilePath = resolvedPath;

            result.IsSuccess = true;
            result.Details = $"Moved to '{resolvedPath}'";
        }

        private static void ExecuteCopyFile(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = false;
                result.Details = "Source file does not exist for copying";
                return;
            }

            string targetDir = SmartRenamer.EvaluateTemplate(step.DestinationTemplate, context);
            targetDir = Path.GetFullPath(targetDir);
            Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, context.FileName);
            string? resolvedPath = ResolveDestinationConflict(targetPath, step.ConflictResolution, context.CurrentFilePath);
            if (resolvedPath == null)
            {
                result.IsSuccess = true;
                result.Details = "Copy skipped due to conflict resolution";
                return;
            }

            File.Copy(context.CurrentFilePath, resolvedPath, true);
            context.CurrentFilePath = resolvedPath;

            result.IsSuccess = true;
            result.Details = $"Copied to '{resolvedPath}'";
        }

        private static void ExecuteExtractArchive(
            WorkflowStep step,
            WorkflowContext context,
            StepExecutionResult result,
            List<string> extractedFilesToEnqueue)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = false;
                result.Details = "Archive file not found";
                return;
            }

            string archivePath = context.CurrentFilePath;
            string parentDir = Path.GetDirectoryName(archivePath) ?? string.Empty;
            string archiveName = Path.GetFileNameWithoutExtension(archivePath);

            string extractDir = step.ExtractDestination switch
            {
                ArchiveExtractDestination.SameFolder => parentDir,
                ArchiveExtractDestination.SubfolderNamedAfterArchive => Path.Combine(parentDir, archiveName),
                ArchiveExtractDestination.CustomFolder => Path.GetFullPath(SmartRenamer.EvaluateTemplate(step.CustomExtractPath, context)),
                _ => Path.Combine(parentDir, archiveName)
            };

            var extractResult = ArchiveExtractor.Extract(archivePath, extractDir);
            if (!extractResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.Details = $"Extraction failed: {extractResult.ErrorMessage}";
                return;
            }

            context.ExtractedFiles.AddRange(extractResult.ExtractedFiles);
            if (step.EnqueueExtractedFiles)
            {
                extractedFilesToEnqueue.AddRange(extractResult.ExtractedFiles);
            }

            // Handle multipart archive companion files
            var allVolumes = ArchiveExtractor.FindMultipartVolumes(archivePath);

            // Post-action on archive file
            switch (step.ArchivePostAction)
            {
                case ArchivePostAction.RecycleArchive:
                    foreach (var vol in allVolumes)
                    {
                        WindowsRecycleBin.SendToRecycleBin(vol);
                    }
                    break;

                case ArchivePostAction.DeleteArchivePermanently:
                    foreach (var vol in allVolumes)
                    {
                        WindowsRecycleBin.PermanentDelete(vol);
                    }
                    break;

                case ArchivePostAction.MoveArchiveToFolder:
                    if (!string.IsNullOrWhiteSpace(step.ArchiveMoveTargetFolder))
                    {
                        string targetFolder = Path.GetFullPath(SmartRenamer.EvaluateTemplate(step.ArchiveMoveTargetFolder, context));
                        Directory.CreateDirectory(targetFolder);
                        foreach (var vol in allVolumes)
                        {
                            string dest = Path.Combine(targetFolder, Path.GetFileName(vol));
                            File.Move(vol, PathHelper.GetUniqueFilePath(dest));
                        }
                    }
                    break;
            }

            result.IsSuccess = true;
            result.Details = $"Extracted {extractResult.ExtractedFiles.Count} files to '{extractDir}'";
        }

        private static void ExecuteRecycleFile(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = true;
                result.Details = "File already deleted";
                return;
            }

            bool recycled = WindowsRecycleBin.SendToRecycleBin(context.CurrentFilePath);
            result.IsSuccess = recycled;
            result.Details = recycled ? "Safely sent to Windows Recycle Bin" : "Failed to send to Recycle Bin";
            context.IsAborted = true; // No more steps on deleted file
        }

        private static void ExecutePermanentDelete(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (!File.Exists(context.CurrentFilePath))
            {
                result.IsSuccess = true;
                result.Details = "File already deleted";
                return;
            }

            bool deleted = WindowsRecycleBin.PermanentDelete(context.CurrentFilePath);
            result.IsSuccess = deleted;
            result.Details = deleted ? "Permanently deleted" : "Failed to delete file";
            context.IsAborted = true;
        }

        private static void ExecuteCleanupFolder(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            string targetDir = context.DirectoryPath;
            if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
            {
                result.IsSuccess = false;
                result.Details = "Cleanup directory does not exist";
                return;
            }

            if (PathHelper.IsSystemOrProtectedDirectory(targetDir))
            {
                result.IsSuccess = false;
                result.Details = "Refused to clean protected system directory";
                return;
            }

            int filesCleaned = 0;
            var now = DateTime.Now;

            // Clean files older than OlderThanDays
            foreach (var file in Directory.GetFiles(targetDir))
            {
                var fi = new FileInfo(file);
                if ((now - fi.LastWriteTime).TotalDays >= step.OlderThanDays)
                {
                    if (step.SafeRecycle)
                    {
                        WindowsRecycleBin.SendToRecycleBin(file);
                    }
                    else
                    {
                        WindowsRecycleBin.PermanentDelete(file);
                    }
                    filesCleaned++;
                }
            }

            int foldersRemoved = 0;
            if (step.RemoveEmptyFolders)
            {
                foreach (var dir in Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories))
                {
                    if (Directory.Exists(dir) &&
                        Directory.GetFileSystemEntries(dir).Length == 0 &&
                        !PathHelper.IsSystemOrProtectedDirectory(dir))
                    {
                        try
                        {
                            Directory.Delete(dir);
                            foldersRemoved++;
                        }
                        catch { }
                    }
                }
            }

            result.IsSuccess = true;
            result.Details = $"Cleaned {filesCleaned} outdated files, removed {foldersRemoved} empty folders";
        }

        private static async Task ExecuteRunCommandAsync(WorkflowStep step, WorkflowContext context, StepExecutionResult result)
        {
            if (string.IsNullOrWhiteSpace(step.CommandExecutable))
            {
                result.IsSuccess = false;
                result.Details = "Executable path is empty";
                return;
            }

            string exe = SmartRenamer.EvaluateTemplate(step.CommandExecutable, context);
            string args = SmartRenamer.EvaluateTemplate(step.CommandArguments, context);

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                result.IsSuccess = false;
                result.Details = "Failed to launch process";
                return;
            }

            await process.WaitForExitAsync();
            result.IsSuccess = process.ExitCode == 0;
            result.Details = $"Process exited with code {process.ExitCode}";
        }

        private static string? ResolveDestinationConflict(string targetPath, ConflictResolution resolution, string sourcePath)
        {
            if (!File.Exists(targetPath)) return targetPath;

            return resolution switch
            {
                ConflictResolution.AutoRenameUnique => PathHelper.GetUniqueFilePath(targetPath),
                ConflictResolution.Overwrite => targetPath,
                ConflictResolution.Skip => null,
                ConflictResolution.KeepNewer => File.GetLastWriteTime(sourcePath) > File.GetLastWriteTime(targetPath) ? targetPath : null,
                _ => PathHelper.GetUniqueFilePath(targetPath)
            };
        }

        private static LogEventType MapStepToEventType(StepType stepType) => stepType switch
        {
            StepType.ExtractArchive => LogEventType.ArchiveExtracted,
            StepType.SmartRename => LogEventType.FileRenamed,
            StepType.MoveFile => LogEventType.FileMoved,
            StepType.CopyFile => LogEventType.FileCopied,
            StepType.RecycleFile => LogEventType.FileRecycled,
            StepType.PermanentDelete => LogEventType.FileDeleted,
            StepType.CleanupFolder => LogEventType.CleanupExecuted,
            StepType.RunCommand => LogEventType.CommandExecuted,
            _ => LogEventType.SystemInfo
        };
    }
}
