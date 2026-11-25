using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileOrgy.Core.Models;
using FileOrgy.Core.Utils;

namespace FileOrgy.Core.Services
{
    public sealed class SimulatedConditionResult
    {
        public RuleCondition Condition { get; init; } = null!;
        public RuleTarget Target { get; init; }
        public ConditionOperator Operator { get; init; }
        public string ExpectedValue { get; init; } = string.Empty;
        public string ActualValue { get; init; } = string.Empty;
        public bool Matched { get; init; }
        public string Summary { get; init; } = string.Empty;
    }

    public sealed class SimulatedStepResult
    {
        public WorkflowStep Step { get; init; } = null!;
        public StepType StepType { get; init; }
        public string StepName { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public bool WouldSucceed { get; init; }
        public string BeforePath { get; init; } = string.Empty;
        public string AfterPath { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public bool AbortedPipeline { get; init; }
    }

    public sealed class SimulatedRuleEvaluation
    {
        public Rule Rule { get; init; } = null!;
        public string RuleId { get; init; } = string.Empty;
        public string RuleName { get; init; } = string.Empty;
        public int Priority { get; init; }
        public bool IsEnabled { get; init; }
        public bool IsInFolderScope { get; init; }
        public bool ConditionsMatched { get; init; }
        public bool IsMatch { get; init; }
        public bool WasSkippedDueToPriorMatch { get; set; }
        public string SkipReason { get; set; } = string.Empty;
        public List<SimulatedConditionResult> ConditionResults { get; } = new();
        public List<SimulatedStepResult> StepResults { get; } = new();
        public string InputPath { get; init; } = string.Empty;
        public string PredictedOutputPath { get; set; } = string.Empty;
    }

    public sealed class PipelineSimulationReport
    {
        public string OriginalFilePath { get; init; } = string.Empty;
        public string FinalFilePath { get; set; } = string.Empty;
        public string OriginalFileName => SafeGetFileName(OriginalFilePath);
        public string FinalFileName => SafeGetFileName(FinalFilePath);
        public string FinalDirectory => SafeGetDirectoryName(FinalFilePath);
        public bool FileExistsOnDisk { get; init; }
        public bool IsSecondaryMultipartVolume { get; set; }
        public bool AnyRuleMatched { get; set; }
        public SimulatedRuleEvaluation? PrimaryMatchedRule { get; set; }
        public List<SimulatedRuleEvaluation> RuleEvaluations { get; } = new();
        public List<SimulatedStepResult> ExecutedSteps { get; } = new();
        public bool WouldDeleteOrRecycle { get; set; }
        public string Summary { get; set; } = string.Empty;
        public Dictionary<string, string> ResolvedVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime? ExtractedDate { get; set; }

        private static string SafeGetFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFileName(path); } catch { return path; }
        }

        private static string SafeGetDirectoryName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetDirectoryName(path) ?? string.Empty; } catch { return string.Empty; }
        }
    }

    /// <summary>
    /// 100% non-destructive dry-run pipeline simulator.
    /// Evaluates folder scopes, conditions, document inspection, regex captures, and path/name transforms in memory.
    /// Never creates directories, moves/copies/deletes files, extracts archives, or spawns processes.
    /// </summary>
    public static class PipelineSimulator
    {
        public static PipelineSimulationReport SimulateFile(string filePath, AppConfiguration config)
        {
            config ??= new AppConfiguration();
            string safePath = filePath ?? string.Empty;
            bool exists = !string.IsNullOrWhiteSpace(safePath) && SafeFileExists(safePath);

            var report = new PipelineSimulationReport
            {
                OriginalFilePath = safePath,
                FinalFilePath = safePath,
                FileExistsOnDisk = exists
            };

            if (string.IsNullOrWhiteSpace(safePath))
            {
                report.Summary = "No file path provided for simulation.";
                return report;
            }

            if (ArchiveExtractor.IsSecondaryMultipartVolume(safePath))
            {
                report.IsSecondaryMultipartVolume = true;
                report.Summary = "Secondary multipart archive volume; skipped because part 1 handles extraction.";
                return report;
            }

            var context = CreateContext(safePath, config);
            var orderedRules = (config.Rules ?? new List<Rule>())
                .OrderBy(r => r.Priority)
                .ToList();

            bool stopped = false;
            string? stopRuleName = null;

            foreach (var rule in orderedRules)
            {
                if (rule == null) continue;

                if (stopped || context.IsAborted)
                {
                    report.RuleEvaluations.Add(new SimulatedRuleEvaluation
                    {
                        Rule = rule,
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Priority = rule.Priority,
                        IsEnabled = rule.Enabled,
                        IsInFolderScope = IsRuleInFolderScope(rule, context.OriginalFilePath, config),
                        ConditionsMatched = false,
                        IsMatch = false,
                        WasSkippedDueToPriorMatch = true,
                        SkipReason = context.IsAborted
                            ? "Skipped: file would already be deleted or recycled by an earlier rule."
                            : $"Skipped: stopped after matching '{stopRuleName}'.",
                        InputPath = context.CurrentFilePath,
                        PredictedOutputPath = context.CurrentFilePath
                    });
                    continue;
                }

                var evaluation = EvaluateAndSimulateRuleInternal(rule, context, config);
                report.RuleEvaluations.Add(evaluation);

                if (evaluation.IsMatch)
                {
                    report.AnyRuleMatched = true;
                    report.PrimaryMatchedRule ??= evaluation;
                    report.ExecutedSteps.AddRange(evaluation.StepResults);

                    if (evaluation.StepResults.Any(s =>
                        s.StepType is StepType.RecycleFile or StepType.PermanentDelete ||
                        (s.StepType == StepType.ExtractArchive &&
                         s.Step.ArchivePostAction is ArchivePostAction.RecycleArchive or ArchivePostAction.DeleteArchivePermanently)))
                    {
                        report.WouldDeleteOrRecycle = true;
                    }

                    if (rule.StopOnFirstMatch)
                    {
                        stopped = true;
                        stopRuleName = rule.Name;
                    }
                }
            }

            report.FinalFilePath = context.CurrentFilePath;
            report.ExtractedDate = context.ExtractedDate;
            foreach (var kvp in context.Variables)
            {
                report.ResolvedVariables[kvp.Key] = kvp.Value;
            }

            report.Summary = BuildReportSummary(report);
            return report;
        }

        public static SimulatedRuleEvaluation SimulateRule(Rule rule, string filePath, AppConfiguration config)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            config ??= new AppConfiguration();
            var context = CreateContext(filePath ?? string.Empty, config);
            return EvaluateAndSimulateRuleInternal(rule, context, config);
        }

        public static bool IsRuleInFolderScope(Rule rule, string filePath, AppConfiguration config)
        {
            if (rule.ScopedFolderIds == null || rule.ScopedFolderIds.Count == 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(filePath) || config.WatchFolders == null || config.WatchFolders.Count == 0)
            {
                return false;
            }

            string? parentDir;
            try
            {
                parentDir = Path.GetDirectoryName(filePath);
            }
            catch
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parentDir))
            {
                return false;
            }

            string normalizedParent = NormalizeDirectoryPath(parentDir);
            if (string.IsNullOrEmpty(normalizedParent))
            {
                return false;
            }

            foreach (var wf in config.WatchFolders)
            {
                if (!rule.ScopedFolderIds.Contains(wf.Id)) continue;
                string normalizedWatch = NormalizeDirectoryPath(wf.FolderPath);
                if (!string.IsNullOrEmpty(normalizedWatch) &&
                    normalizedParent.StartsWith(normalizedWatch, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string NormalizeDirectoryPath(string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) return string.Empty;
            try
            {
                string full = Path.GetFullPath(directoryPath.Trim());
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static WorkflowContext CreateContext(string filePath, AppConfiguration config)
        {
            var vars = config.CustomVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                return new WorkflowContext(filePath, vars);
            }
            catch
            {
                return new WorkflowContext(string.Empty, vars);
            }
        }

        private static SimulatedRuleEvaluation EvaluateAndSimulateRuleInternal(
            Rule rule,
            WorkflowContext context,
            AppConfiguration config)
        {
            var keywordLists = config.KeywordLists ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var globalVars = config.CustomVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool inScope = IsRuleInFolderScope(rule, context.OriginalFilePath, config);

            var eval = new SimulatedRuleEvaluation
            {
                Rule = rule,
                RuleId = rule.Id,
                RuleName = rule.Name,
                Priority = rule.Priority,
                IsEnabled = rule.Enabled,
                IsInFolderScope = inScope,
                InputPath = context.CurrentFilePath,
                PredictedOutputPath = context.CurrentFilePath
            };

            var conditions = rule.Conditions ?? new List<RuleCondition>();
            foreach (var cond in conditions)
            {
                if (cond == null) continue;
                eval.ConditionResults.Add(EvaluateConditionWithDetails(cond, context, keywordLists, globalVars));
            }

            bool conditionsMatched = conditions.Count == 0
                ? true
                : rule.MatchLogic == ConditionMatchLogic.All
                    ? eval.ConditionResults.All(c => c.Matched)
                    : eval.ConditionResults.Any(c => c.Matched);

            bool isMatch = rule.Enabled && inScope && conditionsMatched;

            // Populate helpful skip reason if not matched
            string skipReason = string.Empty;
            if (!rule.Enabled)
            {
                skipReason = "Rule is disabled.";
            }
            else if (!inScope)
            {
                skipReason = "File is outside the rule's scoped watch folders.";
            }
            else if (!conditionsMatched)
            {
                skipReason = rule.MatchLogic == ConditionMatchLogic.All
                    ? "One or more required (ALL) conditions did not match."
                    : "None of the (ANY) conditions matched.";
            }

            var finalized = new SimulatedRuleEvaluation
            {
                Rule = rule,
                RuleId = rule.Id,
                RuleName = rule.Name,
                Priority = rule.Priority,
                IsEnabled = rule.Enabled,
                IsInFolderScope = inScope,
                ConditionsMatched = conditionsMatched,
                IsMatch = isMatch,
                SkipReason = skipReason,
                InputPath = context.CurrentFilePath,
                PredictedOutputPath = context.CurrentFilePath
            };
            finalized.ConditionResults.AddRange(eval.ConditionResults);

            if (!isMatch)
            {
                return finalized;
            }

            foreach (var step in rule.Steps ?? new List<WorkflowStep>())
            {
                if (step == null) continue;

                if (!step.Enabled)
                {
                    finalized.StepResults.Add(new SimulatedStepResult
                    {
                        Step = step,
                        StepType = step.StepType,
                        StepName = string.IsNullOrWhiteSpace(step.Name) ? step.StepType.ToString() : step.Name,
                        Enabled = false,
                        WouldSucceed = true,
                        BeforePath = context.CurrentFilePath,
                        AfterPath = context.CurrentFilePath,
                        Details = "Step disabled (skipped)"
                    });
                    continue;
                }

                if (context.IsAborted)
                {
                    break;
                }

                var stepResult = SimulateStep(step, context);
                finalized.StepResults.Add(stepResult);

                if (!stepResult.WouldSucceed && !step.ContinueOnError)
                {
                    context.IsAborted = true;
                    context.AbortReason = stepResult.Details;
                    break;
                }
            }

            finalized.PredictedOutputPath = context.CurrentFilePath;
            return finalized;
        }

        private static SimulatedConditionResult EvaluateConditionWithDetails(
            RuleCondition condition,
            WorkflowContext context,
            IReadOnlyDictionary<string, List<string>> keywordLists,
            IReadOnlyDictionary<string, string> globalVariables)
        {
            bool matched = false;
            string? regexError = null;

            if (condition.Operator == ConditionOperator.MatchesRegex && !string.IsNullOrEmpty(condition.Value))
            {
                try
                {
                    _ = new Regex(condition.Value);
                }
                catch (ArgumentException ex)
                {
                    regexError = ex.Message;
                }
            }

            if (regexError == null)
            {
                try
                {
                    matched = ConditionEvaluator.EvaluateCondition(condition, context, keywordLists, globalVariables);
                }
                catch
                {
                    matched = false;
                }
            }

            string actual = GetActualTargetDisplay(condition, context, globalVariables);
            string expected = GetExpectedDisplay(condition);
            string summary = regexError != null
                ? $"{condition.Target}: invalid regex '{condition.Value}' ({regexError})"
                : $"{condition.Target} ({actual}) {condition.Operator} '{expected}' -> {(matched ? "Match" : "No match")}";

            return new SimulatedConditionResult
            {
                Condition = condition,
                Target = condition.Target,
                Operator = condition.Operator,
                ExpectedValue = expected,
                ActualValue = actual,
                Matched = matched,
                Summary = summary
            };
        }

        private static string GetActualTargetDisplay(
            RuleCondition condition,
            WorkflowContext context,
            IReadOnlyDictionary<string, string> globalVariables)
        {
            try
            {
                return condition.Target switch
                {
                    RuleTarget.FileName => context.FileName,
                    RuleTarget.BaseName => context.BaseName,
                    RuleTarget.Extension => context.Extension.TrimStart('.'),
                    RuleTarget.FullPath => context.CurrentFilePath,
                    RuleTarget.FileSizeBytes => $"{context.FileSizeBytes} bytes",
                    RuleTarget.FileCreatedDate => context.CreatedDate == default ? "(unavailable)" : context.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    RuleTarget.FileModifiedDate => context.ModifiedDate == default ? "(unavailable)" : context.ModifiedDate.ToString("yyyy-MM-dd HH:mm"),
                    RuleTarget.ExtractedContent => string.IsNullOrEmpty(context.ExtractedContent)
                        ? "(no extracted text)"
                        : Truncate(context.ExtractedContent.Replace('\r', ' ').Replace('\n', ' '), 60),
                    RuleTarget.ExtractedDate => context.ExtractedDate.HasValue
                        ? context.ExtractedDate.Value.ToString("yyyy-MM-dd")
                        : "(no extracted date)",
                    RuleTarget.CustomVariable => ResolveCustomVariableDisplay(condition.TargetVariable, context, globalVariables),
                    _ => string.Empty
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveCustomVariableDisplay(
            string? varName,
            WorkflowContext context,
            IReadOnlyDictionary<string, string> globalVariables)
        {
            string key = varName ?? string.Empty;
            string? val = context.GetVariable(key);
            if (val != null) return val;
            return globalVariables.TryGetValue(key, out var gv) ? gv : string.Empty;
        }

        private static string GetExpectedDisplay(RuleCondition condition)
        {
            if (condition.Operator is ConditionOperator.InKeywordList or ConditionOperator.NotInKeywordList)
            {
                string listName = !string.IsNullOrEmpty(condition.KeywordListName)
                    ? condition.KeywordListName
                    : condition.Value;
                return $"[{listName}]";
            }

            if (condition.Operator == ConditionOperator.IsEmptyFile)
            {
                return "(empty)";
            }

            return condition.Value ?? string.Empty;
        }

        private static SimulatedStepResult SimulateStep(WorkflowStep step, WorkflowContext context)
        {
            string stepName = string.IsNullOrWhiteSpace(step.Name) ? step.StepType.ToString() : step.Name;
            string beforePath = context.CurrentFilePath;

            try
            {
                switch (step.StepType)
                {
                    case StepType.InspectDocument:
                        return SimulateInspectDocument(step, stepName, context, beforePath);

                    case StepType.ExtractDate:
                        return SimulateExtractDate(step, stepName, context, beforePath);

                    case StepType.RegexCapture:
                        return SimulateRegexCapture(step, stepName, context, beforePath);

                    case StepType.SmartRename:
                        return SimulateSmartRename(step, stepName, context, beforePath);

                    case StepType.MoveFile:
                        return SimulateMoveOrCopyFile(step, stepName, context, beforePath, isCopy: false);

                    case StepType.CopyFile:
                        return SimulateMoveOrCopyFile(step, stepName, context, beforePath, isCopy: true);

                    case StepType.ExtractArchive:
                        return SimulateExtractArchive(step, stepName, context, beforePath);

                    case StepType.RecycleFile:
                        context.IsAborted = true;
                        context.AbortReason = "File recycled in simulation";
                        return new SimulatedStepResult
                        {
                            Step = step,
                            StepType = step.StepType,
                            StepName = stepName,
                            Enabled = true,
                            WouldSucceed = true,
                            BeforePath = beforePath,
                            AfterPath = "(Recycle Bin)",
                            Details = "Would safely send file to Windows Recycle Bin",
                            AbortedPipeline = true
                        };

                    case StepType.PermanentDelete:
                        context.IsAborted = true;
                        context.AbortReason = "File permanently deleted in simulation";
                        return new SimulatedStepResult
                        {
                            Step = step,
                            StepType = step.StepType,
                            StepName = stepName,
                            Enabled = true,
                            WouldSucceed = true,
                            BeforePath = beforePath,
                            AfterPath = "(Permanently Deleted)",
                            Details = "Would permanently delete file from disk",
                            AbortedPipeline = true
                        };

                    case StepType.CleanupFolder:
                        return SimulateCleanupFolder(step, stepName, context, beforePath);

                    case StepType.RunCommand:
                        return SimulateRunCommand(step, stepName, context, beforePath);

                    default:
                        return new SimulatedStepResult
                        {
                            Step = step,
                            StepType = step.StepType,
                            StepName = stepName,
                            Enabled = true,
                            WouldSucceed = false,
                            BeforePath = beforePath,
                            AfterPath = beforePath,
                            Details = $"Unsupported step type: {step.StepType}"
                        };
                }
            }
            catch (Exception ex)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = context.CurrentFilePath,
                    Details = $"Simulation error: {ex.Message}"
                };
            }
        }

        private static SimulatedStepResult SimulateInspectDocument(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            if (!SafeFileExists(context.CurrentFilePath))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Sample file does not exist on disk; document inspection skipped"
                };
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

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = inspection.IsSuccess,
                BeforePath = beforePath,
                AfterPath = beforePath,
                Details = inspection.IsSuccess
                    ? $"Inspected read-only: {inspection.TextContent.Length} chars, doc_date={context.ExtractedDate:yyyy-MM-dd}"
                    : inspection.ErrorMessage ?? "Document inspection failed"
            };
        }

        private static SimulatedStepResult SimulateExtractDate(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            if (context.ExtractedDate.HasValue)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Using extracted document date: {context.ExtractedDate:yyyy-MM-dd}"
                };
            }

            if (string.IsNullOrEmpty(context.ExtractedContent) && SafeFileExists(context.CurrentFilePath))
            {
                var inspection = DocumentInspector.Inspect(context.CurrentFilePath);
                context.ExtractedContent = inspection.TextContent;
                context.ExtractedDate = inspection.DocumentDate;
            }

            if (context.ExtractedDate.HasValue)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Extracted date from document: {context.ExtractedDate:yyyy-MM-dd}"
                };
            }

            if (context.CreatedDate != default)
            {
                context.ExtractedDate = context.CreatedDate;
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"No date in content; defaulted to file created date: {context.ExtractedDate:yyyy-MM-dd}"
                };
            }

            context.ExtractedDate = DateTime.Now;
            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = beforePath,
                Details = $"Sample file not on disk; previewing doc_date with today's date: {context.ExtractedDate:yyyy-MM-dd}"
            };
        }

        private static SimulatedStepResult SimulateRegexCapture(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            if (string.IsNullOrWhiteSpace(step.RegexPattern))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Regex pattern is empty"
                };
            }

            if (string.Equals(step.RegexSource, "Content", StringComparison.OrdinalIgnoreCase) &&
                context.ExtractedContent == null &&
                SafeFileExists(context.CurrentFilePath))
            {
                var inspection = DocumentInspector.Inspect(context.CurrentFilePath);
                context.ExtractedContent = inspection.TextContent;
                if (inspection.DocumentDate.HasValue && !context.ExtractedDate.HasValue)
                {
                    context.ExtractedDate = inspection.DocumentDate;
                }
            }

            string sourceText = step.RegexSource switch
            {
                "FileName" => context.FileName,
                "FullPath" => context.CurrentFilePath,
                _ => context.ExtractedContent ?? string.Empty
            };

            Match match;
            try
            {
                match = Regex.Match(sourceText, step.RegexPattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Invalid regex pattern: {ex.Message}"
                };
            }
            catch (RegexMatchTimeoutException)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Regex evaluation timed out"
                };
            }

            if (!match.Success)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Regex did not match in {step.RegexSource}"
                };
            }

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

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = beforePath,
                Details = $"Captured {match.Groups.Count} regex group(s) from {step.RegexSource}"
            };
        }

        private static SimulatedStepResult SimulateSmartRename(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            string rawNewName = SmartRenamer.EvaluateTemplate(step.RenamePattern, context);
            rawNewName = SmartRenamer.ApplyCaseTransform(rawNewName, step.CaseTransform);
            rawNewName = PathHelper.SanitizeFileName(rawNewName);

            if (!Path.HasExtension(rawNewName) && !string.IsNullOrEmpty(context.Extension))
            {
                rawNewName += context.Extension;
            }

            string currentDir = context.DirectoryPath;
            string targetPath = string.IsNullOrEmpty(currentDir) ? rawNewName : Path.Combine(currentDir, rawNewName);

            if (string.Equals(context.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Filename is already up to date"
                };
            }

            string? resolvedPath = SimulateConflictResolution(targetPath, step.ConflictResolution, context.CurrentFilePath);
            if (resolvedPath == null)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Would skip rename (conflict policy: {step.ConflictResolution})"
                };
            }

            context.CurrentFilePath = resolvedPath;

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = resolvedPath,
                Details = $"Would rename to '{Path.GetFileName(resolvedPath)}'"
            };
        }

        private static SimulatedStepResult SimulateMoveOrCopyFile(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath,
            bool isCopy)
        {
            if (string.IsNullOrWhiteSpace(step.DestinationTemplate))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Destination folder template is empty"
                };
            }

            string evaluatedDir = SmartRenamer.EvaluateTemplate(step.DestinationTemplate, context);
            string targetDir;
            try
            {
                targetDir = Path.GetFullPath(evaluatedDir);
            }
            catch (Exception ex)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Invalid destination path '{evaluatedDir}': {ex.Message}"
                };
            }

            string targetPath = Path.Combine(targetDir, context.FileName);

            if (!isCopy && string.Equals(context.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "File is already in target destination"
                };
            }

            string? resolvedPath = SimulateConflictResolution(targetPath, step.ConflictResolution, context.CurrentFilePath);
            if (resolvedPath == null)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = true,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Would skip {(isCopy ? "copy" : "move")} (conflict policy: {step.ConflictResolution})"
                };
            }

            context.CurrentFilePath = resolvedPath;

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = resolvedPath,
                Details = $"Would {(isCopy ? "copy" : "move")} to '{resolvedPath}'"
            };
        }

        private static SimulatedStepResult SimulateExtractArchive(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            string parentDir = context.DirectoryPath;
            string archiveName = context.BaseName;

            string extractDir;
            try
            {
                extractDir = step.ExtractDestination switch
                {
                    ArchiveExtractDestination.SameFolder => parentDir,
                    ArchiveExtractDestination.SubfolderNamedAfterArchive => Path.Combine(parentDir, archiveName),
                    ArchiveExtractDestination.CustomFolder => string.IsNullOrWhiteSpace(step.CustomExtractPath)
                        ? Path.Combine(parentDir, archiveName)
                        : Path.GetFullPath(SmartRenamer.EvaluateTemplate(step.CustomExtractPath, context)),
                    _ => Path.Combine(parentDir, archiveName)
                };
            }
            catch (Exception ex)
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = $"Invalid custom extraction path: {ex.Message}"
                };
            }

            string postActionText = step.ArchivePostAction switch
            {
                ArchivePostAction.KeepArchive => "keep original archive",
                ArchivePostAction.RecycleArchive => "send archive to Recycle Bin",
                ArchivePostAction.DeleteArchivePermanently => "permanently delete archive",
                ArchivePostAction.MoveArchiveToFolder => $"move archive to '{SmartRenamer.EvaluateTemplate(step.ArchiveMoveTargetFolder, context)}'",
                _ => "keep archive"
            };

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = extractDir,
                Details = $"Would extract archive to '{extractDir}' and {postActionText}"
            };
        }

        private static SimulatedStepResult SimulateCleanupFolder(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            string targetDir = context.DirectoryPath;
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Cleanup directory path is empty"
                };
            }

            if (PathHelper.IsSystemOrProtectedDirectory(targetDir))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Refused: protected system directory"
                };
            }

            int eligibleFiles = 0;
            if (Directory.Exists(targetDir))
            {
                try
                {
                    var now = DateTime.Now;
                    foreach (var file in Directory.GetFiles(targetDir))
                    {
                        var fi = new FileInfo(file);
                        if ((now - fi.LastWriteTime).TotalDays >= step.OlderThanDays)
                        {
                            eligibleFiles++;
                        }
                    }
                }
                catch { }
            }

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = beforePath,
                Details = $"Would clean {eligibleFiles} file(s) older than {step.OlderThanDays} day(s) in '{targetDir}'"
            };
        }

        private static SimulatedStepResult SimulateRunCommand(
            WorkflowStep step,
            string stepName,
            WorkflowContext context,
            string beforePath)
        {
            if (string.IsNullOrWhiteSpace(step.CommandExecutable))
            {
                return new SimulatedStepResult
                {
                    Step = step,
                    StepType = step.StepType,
                    StepName = stepName,
                    Enabled = true,
                    WouldSucceed = false,
                    BeforePath = beforePath,
                    AfterPath = beforePath,
                    Details = "Executable path is empty"
                };
            }

            string exe = SmartRenamer.EvaluateTemplate(step.CommandExecutable, context);
            string args = SmartRenamer.EvaluateTemplate(step.CommandArguments, context);
            string cmdPreview = string.IsNullOrWhiteSpace(args) ? exe : $"{exe} {args}";

            return new SimulatedStepResult
            {
                Step = step,
                StepType = step.StepType,
                StepName = stepName,
                Enabled = true,
                WouldSucceed = true,
                BeforePath = beforePath,
                AfterPath = beforePath,
                Details = $"Would run command (not executed in simulation): {cmdPreview}"
            };
        }

        private static string? SimulateConflictResolution(string targetPath, ConflictResolution resolution, string sourcePath)
        {
            if (!SafeFileExists(targetPath)) return targetPath;

            return resolution switch
            {
                ConflictResolution.AutoRenameUnique => PathHelper.GetUniqueFilePath(targetPath),
                ConflictResolution.Overwrite => targetPath,
                ConflictResolution.Skip => null,
                ConflictResolution.KeepNewer => SafeFileExists(sourcePath) && File.GetLastWriteTime(sourcePath) > File.GetLastWriteTime(targetPath)
                    ? targetPath
                    : null,
                _ => PathHelper.GetUniqueFilePath(targetPath)
            };
        }

        private static bool SafeFileExists(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try { return File.Exists(path); } catch { return false; }
        }

        private static string BuildReportSummary(PipelineSimulationReport report)
        {
            if (!report.AnyRuleMatched)
            {
                return $"No active rule matched '{report.OriginalFileName}'. File would remain untouched.";
            }

            if (report.WouldDeleteOrRecycle)
            {
                return $"Matched '{report.PrimaryMatchedRule?.RuleName}'. File would be recycled or deleted.";
            }

            if (string.Equals(report.OriginalFilePath, report.FinalFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return $"Matched '{report.PrimaryMatchedRule?.RuleName}'. Executed {report.ExecutedSteps.Count} step(s); path unchanged.";
            }

            return $"Matched '{report.PrimaryMatchedRule?.RuleName}'. Final path: {report.FinalFilePath}";
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen) return value;
            return value[..maxLen] + "...";
        }
    }
}
