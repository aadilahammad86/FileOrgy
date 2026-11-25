using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace FileOrgy.App.ViewModels
{
    public class PipelinePreviewViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly NotificationService? _notifications;

        private string _sampleFilePath = string.Empty;
        private PipelineSimulationReport? _report;
        private bool _isExecutingLive;

        public string SampleFilePath
        {
            get => _sampleFilePath;
            set
            {
                if (SetProperty(ref _sampleFilePath, value ?? string.Empty))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public PipelineSimulationReport? Report
        {
            get => _report;
            private set
            {
                if (SetProperty(ref _report, value))
                {
                    OnPropertyChanged(nameof(HasReport));
                    OnPropertyChanged(nameof(HasMatch));
                    OnPropertyChanged(nameof(IsNoMatch));
                    OnPropertyChanged(nameof(WouldDeleteOrRecycle));
                    OnPropertyChanged(nameof(SummaryHeadline));
                    OnPropertyChanged(nameof(BeforePathDisplay));
                    OnPropertyChanged(nameof(AfterPathDisplay));
                    OnPropertyChanged(nameof(MatchedRuleName));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsExecutingLive
        {
            get => _isExecutingLive;
            private set
            {
                if (SetProperty(ref _isExecutingLive, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasReport => Report != null;
        public bool HasMatch => Report?.AnyRuleMatched == true;
        public bool IsNoMatch => Report != null && !Report.AnyRuleMatched;
        public bool WouldDeleteOrRecycle => Report?.WouldDeleteOrRecycle == true;

        public string SummaryHeadline
        {
            get
            {
                if (Report == null) return "Drop or browse a sample file to preview pipeline execution without modifying disk.";
                if (Report.IsSecondaryMultipartVolume) return "Secondary multipart archive volume (skipped; part 1 handles extraction).";
                if (!Report.AnyRuleMatched) return "No enabled rule matched this file — it would remain untouched.";
                if (Report.WouldDeleteOrRecycle) return $"Matched '{MatchedRuleName}' — destructive action (Recycle/Delete) would run.";
                return $"Matched '{MatchedRuleName}' — {Report.ExecutedSteps.Count} step(s) simulated.";
            }
        }

        public string BeforePathDisplay => Report?.OriginalFilePath ?? string.Empty;

        public string AfterPathDisplay
        {
            get
            {
                if (Report == null) return string.Empty;
                if (Report.WouldDeleteOrRecycle)
                {
                    var lastStep = Report.ExecutedSteps.LastOrDefault();
                    if (lastStep != null && !string.IsNullOrWhiteSpace(lastStep.AfterPath))
                    {
                        return lastStep.AfterPath;
                    }
                }
                return Report.FinalFilePath;
            }
        }

        public string MatchedRuleName => Report?.PrimaryMatchedRule?.RuleName ?? string.Empty;

        public ObservableCollection<SimulatedRuleEvaluation> RuleEvaluations { get; } = new();
        public ObservableCollection<SimulatedStepResult> MatchedSteps { get; } = new();
        public ObservableCollection<KeyValuePair<string, string>> ResolvedVariables { get; } = new();

        public ICommand PickSampleFileCommand { get; }
        public ICommand RunSimulationCommand { get; }
        public ICommand ExecuteSampleLiveCommand { get; }
        public ICommand ClearSimulationCommand { get; }

        public PipelinePreviewViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _notifications = notifications;

            PickSampleFileCommand = new RelayCommand(PickSampleFile);
            RunSimulationCommand = new RelayCommand(
                () => SimulatePath(SampleFilePath),
                () => !string.IsNullOrWhiteSpace(SampleFilePath));
            ExecuteSampleLiveCommand = new RelayCommand(
                async () => await ExecuteSampleLiveAsync(),
                () => !IsExecutingLive && !string.IsNullOrWhiteSpace(SampleFilePath) && File.Exists(SampleFilePath));
            ClearSimulationCommand = new RelayCommand(
                ClearSimulation,
                () => HasReport || !string.IsNullOrWhiteSpace(SampleFilePath));
        }

        public void SimulatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                ClearSimulation();
                return;
            }

            SampleFilePath = path.Trim();
            var report = PipelineSimulator.SimulateFile(SampleFilePath, _orchestrator.ConfigService.CurrentConfig);
            ApplyReport(report);
        }

        public void SimulateRuleDraft(Rule rule)
        {
            if (rule == null) return;

            string targetPath = !string.IsNullOrWhiteSpace(SampleFilePath)
                ? SampleFilePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "invoice_2026_sample.pdf");

            if (string.IsNullOrWhiteSpace(SampleFilePath))
            {
                SampleFilePath = targetPath;
            }

            var config = _orchestrator.ConfigService.CurrentConfig;
            var evaluation = PipelineSimulator.SimulateRule(rule, targetPath, config);

            var report = new PipelineSimulationReport
            {
                OriginalFilePath = targetPath,
                FinalFilePath = evaluation.PredictedOutputPath,
                FileExistsOnDisk = File.Exists(targetPath),
                AnyRuleMatched = evaluation.IsMatch,
                PrimaryMatchedRule = evaluation.IsMatch ? evaluation : null,
                WouldDeleteOrRecycle = evaluation.StepResults.Any(s =>
                    s.StepType is StepType.RecycleFile or StepType.PermanentDelete ||
                    (s.StepType == StepType.ExtractArchive &&
                     s.Step.ArchivePostAction is ArchivePostAction.RecycleArchive or ArchivePostAction.DeleteArchivePermanently)),
                Summary = evaluation.IsMatch
                    ? $"Draft rule '{rule.Name}' matched with {evaluation.StepResults.Count} step(s)."
                    : evaluation.SkipReason
            };

            report.RuleEvaluations.Add(evaluation);
            if (evaluation.IsMatch)
            {
                report.ExecutedSteps.AddRange(evaluation.StepResults);
            }

            ApplyReport(report);
        }

        public void ClearSimulation()
        {
            SampleFilePath = string.Empty;
            RuleEvaluations.Clear();
            MatchedSteps.Clear();
            ResolvedVariables.Clear();
            Report = null;
        }

        private void PickSampleFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a Sample File for Dry-Run Pipeline Simulation",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                SimulatePath(dialog.FileName);
            }
        }

        private async Task ExecuteSampleLiveAsync()
        {
            if (string.IsNullOrWhiteSpace(SampleFilePath) || !File.Exists(SampleFilePath))
            {
                _notifications?.ShowToast(
                    "File Not Found",
                    "The selected sample file no longer exists on disk.",
                    ToastSeverity.Warning);
                return;
            }

            if (Report == null)
            {
                SimulatePath(SampleFilePath);
            }

            if (WouldDeleteOrRecycle && _notifications != null)
            {
                bool confirmed = await _notifications.ConfirmAsync(
                    "Confirm Destructive Action",
                    $"Running the pipeline on '{Path.GetFileName(SampleFilePath)}' will recycle or delete the file.\n\nDo you want to execute it live?",
                    confirmLabel: "Run Live",
                    cancelLabel: "Cancel",
                    isDestructive: true);

                if (!confirmed)
                {
                    return;
                }
            }

            IsExecutingLive = true;
            try
            {
                string fileName = Path.GetFileName(SampleFilePath);
                bool matched = await _orchestrator.ProcessFileAsync(SampleFilePath);

                if (matched)
                {
                    _notifications?.ShowToast(
                        "Pipeline Executed",
                        $"Processed '{fileName}' against matching automation rules.",
                        ToastSeverity.Success);
                }
                else
                {
                    _notifications?.ShowToast(
                        "No Rule Matched",
                        $"'{fileName}' did not match any enabled rule and was left untouched.",
                        ToastSeverity.Info);
                }

                if (File.Exists(SampleFilePath))
                {
                    SimulatePath(SampleFilePath);
                }
            }
            catch (Exception ex)
            {
                _notifications?.ShowToast(
                    "Execution Failed",
                    ex.Message,
                    ToastSeverity.Error);
            }
            finally
            {
                IsExecutingLive = false;
            }
        }

        private void ApplyReport(PipelineSimulationReport report)
        {
            RuleEvaluations.Clear();
            foreach (var eval in report.RuleEvaluations)
            {
                RuleEvaluations.Add(eval);
            }

            MatchedSteps.Clear();
            foreach (var step in report.ExecutedSteps)
            {
                MatchedSteps.Add(step);
            }

            ResolvedVariables.Clear();
            foreach (var kvp in report.ResolvedVariables.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                ResolvedVariables.Add(kvp);
            }

            Report = report;
        }
    }
}
