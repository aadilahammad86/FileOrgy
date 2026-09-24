using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App.ViewModels
{
    public class RuleItemViewModel : ViewModelBase
    {
        private readonly Rule _rule;
        private readonly Action _onChanged;

        public string Id => _rule.Id;

        public string Name
        {
            get => _rule.Name;
            set
            {
                if (_rule.Name != value)
                {
                    _rule.Name = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public string Description
        {
            get => _rule.Description;
            set
            {
                if (_rule.Description != value)
                {
                    _rule.Description = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public bool Enabled
        {
            get => _rule.Enabled;
            set
            {
                if (_rule.Enabled != value)
                {
                    _rule.Enabled = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public int Priority
        {
            get => _rule.Priority;
            set
            {
                if (_rule.Priority != value)
                {
                    _rule.Priority = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public int ConditionsCount => _rule.Conditions.Count;
        public int StepsCount => _rule.Steps.Count;

        public string SummaryText => $"{ConditionsCount} conditions, {StepsCount} workflow steps";

        public Rule Rule => _rule;

        public RuleItemViewModel(Rule rule, Action onChanged)
        {
            _rule = rule;
            _onChanged = onChanged;
        }

        public void NotifyUpdated()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(ConditionsCount));
            OnPropertyChanged(nameof(StepsCount));
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    public class RulesViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private RuleItemViewModel? _selectedRule;

        public ObservableCollection<RuleItemViewModel> Rules { get; } = new();

        public RuleItemViewModel? SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }

        public ICommand AddRuleCommand { get; }
        public ICommand EditRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand DuplicateRuleCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand LoadInPlacePresetsCommand { get; }

        public event Action<Rule>? RequestEditRule;

        public RulesViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;

            AddRuleCommand = new RelayCommand(AddRule);
            EditRuleCommand = new RelayCommand(EditRule, () => SelectedRule != null);
            RemoveRuleCommand = new RelayCommand(RemoveRule, () => SelectedRule != null);
            DuplicateRuleCommand = new RelayCommand(DuplicateRule, () => SelectedRule != null);
            MoveUpCommand = new RelayCommand(MoveUp, () => SelectedRule != null && Rules.IndexOf(SelectedRule) > 0);
            MoveDownCommand = new RelayCommand(MoveDown, () => SelectedRule != null && Rules.IndexOf(SelectedRule) < Rules.Count - 1);
            LoadInPlacePresetsCommand = new RelayCommand(LoadInPlacePresets);

            LoadRules();
        }

        private void LoadInPlacePresets()
        {
            var existingPresets = _orchestrator.ConfigService.CurrentConfig.Rules
                .Where(r => r.Id.StartsWith("rule-inplace-", StringComparison.OrdinalIgnoreCase) ||
                            r.Id.StartsWith("preset-inplace-", StringComparison.OrdinalIgnoreCase) ||
                            r.Name.StartsWith("In-Place:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            bool overwrite = false;
            if (existingPresets.Count > 0)
            {
                var promptResult = System.Windows.MessageBox.Show(
                    $"In-place organizer rules are already present ({existingPresets.Count} detected).\n\nDo you want to reset them to default presets?",
                    "Restore In-Place Presets",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (promptResult != MessageBoxResult.Yes)
                {
                    return;
                }
                overwrite = true;
            }
            else
            {
                var result = System.Windows.MessageBox.Show(
                    "Load In-Place Organization Presets?\n\nThis will add rules to automatically sort files into Programs, Documents, Compressed, Pictures, Videos, Audio, and Others folders directly inside each monitored directory using dynamic in-place relocation.",
                    "Load In-Place Presets",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _orchestrator.ConfigService.LoadInPlaceOrganizationPresets(overwrite);
            LoadRules();
            SelectedRule = Rules.FirstOrDefault(r => r.Id.StartsWith("rule-inplace-", StringComparison.OrdinalIgnoreCase) ||
                                                    r.Id.StartsWith("preset-inplace-", StringComparison.OrdinalIgnoreCase)) ?? Rules.FirstOrDefault();

            System.Windows.MessageBox.Show(
                "7 In-Place Organization Presets loaded successfully:\n\n" +
                "• In-Place: Programs & Installers (.exe, .msi, .dmg)\n" +
                "• In-Place: Documents & Tables (.pdf, .docx, .doc, .xlsx, .csv, .txt, etc.)\n" +
                "• In-Place: Compressed Archives (.zip, .rar, .7z, .tar, .gz, etc.)\n" +
                "• In-Place: Pictures & Graphics (.jpg, .jpeg, .png, .gif, .svg, etc.)\n" +
                "• In-Place: Videos & Movies (.mp4, .mkv, .avi, .mov, etc.)\n" +
                "• In-Place: Audio & Music (.mp3, .wav, .flac, .m4a, etc.)\n" +
                "• In-Place: Other Files (Any unclassified file type not matching above filters)\n\n" +
                "Target subfolders are organized in-place under each monitored directory.",
                "Presets Loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void LoadRules()
        {
            Rules.Clear();
            var sorted = _orchestrator.ConfigService.CurrentConfig.Rules.OrderBy(r => r.Priority).ToList();
            foreach (var rule in sorted)
            {
                Rules.Add(new RuleItemViewModel(rule, SaveAndApply));
            }
            SelectedRule = Rules.FirstOrDefault();
        }

        private void AddRule()
        {
            var newRule = new Rule
            {
                Name = "New Automation Rule",
                Description = "Custom workflow rule",
                Priority = Rules.Count + 1,
                Enabled = true,
                Steps = new()
                {
                    new WorkflowStep { StepType = StepType.SmartRename, Name = "Rename File" }
                }
            };

            _orchestrator.ConfigService.CurrentConfig.Rules.Add(newRule);
            var vm = new RuleItemViewModel(newRule, SaveAndApply);
            Rules.Add(vm);
            SelectedRule = vm;
            SaveAndApply();

            RequestEditRule?.Invoke(newRule);
        }

        private void EditRule()
        {
            if (SelectedRule != null)
            {
                RequestEditRule?.Invoke(SelectedRule.Rule);
            }
        }

        private void DuplicateRule()
        {
            if (SelectedRule == null) return;

            var orig = SelectedRule.Rule;
            var copy = new Rule
            {
                Name = $"{orig.Name} (Copy)",
                Description = orig.Description,
                Priority = Rules.Count + 1,
                Enabled = true,
                MatchLogic = orig.MatchLogic,
                StopOnFirstMatch = orig.StopOnFirstMatch,
                ScopedFolderIds = new(orig.ScopedFolderIds),
                Conditions = orig.Conditions.Select(c => new RuleCondition
                {
                    Target = c.Target,
                    Operator = c.Operator,
                    Value = c.Value,
                    KeywordListName = c.KeywordListName,
                    CaseSensitive = c.CaseSensitive,
                    TargetVariable = c.TargetVariable
                }).ToList(),
                Steps = orig.Steps.Select(s => new WorkflowStep
                {
                    StepType = s.StepType,
                    Name = s.Name,
                    Enabled = s.Enabled,
                    ContinueOnError = s.ContinueOnError,
                    RenamePattern = s.RenamePattern,
                    DestinationTemplate = s.DestinationTemplate,
                    ConflictResolution = s.ConflictResolution,
                    ExtractDestination = s.ExtractDestination,
                    ArchivePostAction = s.ArchivePostAction,
                    RegexPattern = s.RegexPattern,
                    RegexSource = s.RegexSource,
                    OlderThanDays = s.OlderThanDays,
                    RemoveEmptyFolders = s.RemoveEmptyFolders,
                    SafeRecycle = s.SafeRecycle
                }).ToList()
            };

            _orchestrator.ConfigService.CurrentConfig.Rules.Add(copy);
            var vm = new RuleItemViewModel(copy, SaveAndApply);
            Rules.Add(vm);
            SelectedRule = vm;
            SaveAndApply();
        }

        private void RemoveRule()
        {
            if (SelectedRule == null) return;

            var res = System.Windows.MessageBox.Show(
                $"Delete rule '{SelectedRule.Name}'?",
                "Delete Rule",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _orchestrator.ConfigService.CurrentConfig.Rules.Remove(SelectedRule.Rule);
                Rules.Remove(SelectedRule);
                SelectedRule = Rules.FirstOrDefault();
                ReindexPriorities();
                SaveAndApply();
            }
        }

        private void MoveUp()
        {
            if (SelectedRule == null) return;
            int idx = Rules.IndexOf(SelectedRule);
            if (idx > 0)
            {
                var item = SelectedRule;
                Rules.RemoveAt(idx);
                Rules.Insert(idx - 1, item);
                SelectedRule = item;
                ReindexPriorities();
                SaveAndApply();
            }
        }

        private void MoveDown()
        {
            if (SelectedRule == null) return;
            int idx = Rules.IndexOf(SelectedRule);
            if (idx >= 0 && idx < Rules.Count - 1)
            {
                var item = SelectedRule;
                Rules.RemoveAt(idx);
                Rules.Insert(idx + 1, item);
                SelectedRule = item;
                ReindexPriorities();
                SaveAndApply();
            }
        }

        private void ReindexPriorities()
        {
            for (int i = 0; i < Rules.Count; i++)
            {
                Rules[i].Priority = i + 1;
            }
        }

        public void SaveAndApply()
        {
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
        }
    }
}
