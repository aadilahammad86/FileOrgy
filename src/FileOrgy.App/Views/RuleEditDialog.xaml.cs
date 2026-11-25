using System;
using System.IO;
using System.Linq;
using System.Windows;
using FileOrgy.App.Services;
using FileOrgy.App.ViewModels;
using FileOrgy.Core.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfBrush = System.Windows.Media.Brush;

namespace FileOrgy.App.Views
{
    public partial class RuleEditDialog : Window
    {
        public RuleEditViewModel ViewModel { get; }

        public RuleEditDialog(RuleEditViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            SourceInitialized += (_, _) => ThemeService.ApplyWindowDarkMode(this, ThemeService.IsDark);
        }

        private void OnGuideClick(object sender, RoutedEventArgs e)
        {
            var guide = new RuleGuideDialog
            {
                Owner = this,
                Icon = Icon
            };
            guide.ShowDialog();
        }

        private void OnBrowseSampleClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Sample File for Rule Simulation",
                Filter = "All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                TxtTestSampleFile.Text = dialog.FileName;
                RunDraftSimulation();
            }
        }

        private void OnTestDraftRuleClick(object sender, RoutedEventArgs e)
        {
            RunDraftSimulation();
        }

        private void RunDraftSimulation()
        {
            string rawSample = TxtTestSampleFile.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawSample))
            {
                rawSample = "invoice_2026_sample.pdf";
                TxtTestSampleFile.Text = rawSample;
            }

            string samplePath = ResolveSamplePath(rawSample);

            try
            {
                var eval = PipelineSimulator.SimulateRule(ViewModel.Rule, samplePath, ViewModel.ConfigSnapshot);
                TestResultBadge.Visibility = Visibility.Visible;

                if (eval.IsMatch)
                {
                    bool allStepsOk = eval.StepResults.All(s => !s.Enabled || s.WouldSucceed);
                    ApplyBadgeTheme(allStepsOk ? "Brush.SuccessSubtle" : "Brush.WarningSubtle",
                                    allStepsOk ? "Brush.SuccessText" : "Brush.WarningText");
                    TxtTestVerdict.Text = allStepsOk ? "Matched" : "Matched (Check Steps)";

                    var lastExecuted = eval.StepResults.LastOrDefault(s => s.Enabled);
                    string outputText = !string.IsNullOrWhiteSpace(eval.PredictedOutputPath) &&
                                        !string.Equals(eval.PredictedOutputPath, eval.InputPath, StringComparison.OrdinalIgnoreCase)
                        ? $"-> {eval.PredictedOutputPath}"
                        : lastExecuted?.Details ?? "Rule matched (path unchanged)";
                    TxtTestOutput.Text = outputText;
                }
                else
                {
                    ApplyBadgeTheme("Brush.WarningSubtle", "Brush.WarningText");
                    TxtTestVerdict.Text = "No Match";
                    TxtTestOutput.Text = string.IsNullOrWhiteSpace(eval.SkipReason)
                        ? "Conditions did not match sample file."
                        : eval.SkipReason;
                }
            }
            catch (Exception ex)
            {
                TestResultBadge.Visibility = Visibility.Visible;
                ApplyBadgeTheme("Brush.DangerSubtle", "Brush.DangerText");
                TxtTestVerdict.Text = "Error";
                TxtTestOutput.Text = ex.Message;
            }
        }

        private string ResolveSamplePath(string rawSample)
        {
            if (Path.IsPathRooted(rawSample))
            {
                return rawSample;
            }

            string? scopedDir = ViewModel.FolderScopeChoices
                .Where(c => c.IsSelected && !string.IsNullOrWhiteSpace(c.FolderPath))
                .Select(c => c.FolderPath)
                .FirstOrDefault();

            string? fallbackWatchDir = ViewModel.ConfigSnapshot.WatchFolders
                .Where(w => !string.IsNullOrWhiteSpace(w.FolderPath))
                .Select(w => w.FolderPath)
                .FirstOrDefault();

            string baseDir = scopedDir
                ?? fallbackWatchDir
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            return Path.Combine(baseDir, rawSample);
        }

        private void ApplyBadgeTheme(string backgroundKey, string foregroundKey)
        {
            if (TryFindResource(backgroundKey) is WpfBrush bg)
            {
                TestResultBadge.Background = bg;
            }
            if (TryFindResource(foregroundKey) is WpfBrush fg)
            {
                TxtTestVerdict.Foreground = fg;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
