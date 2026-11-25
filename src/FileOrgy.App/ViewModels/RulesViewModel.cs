using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrgy.App.Services;
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

        public string SummaryText => $"{ConditionsCount} condition(s)  •  {StepsCount} workflow step(s)";

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
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(Priority));
            OnPropertyChanged(nameof(ConditionsCount));
            OnPropertyChanged(nameof(StepsCount));
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    public class RulesViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly NotificationService? _notifications;
        private RuleItemViewModel? _selectedRule;
        private string _searchQuery = string.Empty;

        public ObservableCollection<RuleItemViewModel> Rules { get; } = new();
        public ObservableCollection<RuleItemViewModel> FilteredRules { get; } = new();
        public ObservableCollection<WorkflowStep> SelectedRulePipelinePreview { get; } = new();

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        public RuleItemViewModel? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (SetProperty(ref _selectedRule, value))
                {
                    RefreshSelectedRuleInspector();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SelectedRuleFolderScopeSummary
        {
            get
            {
                if (SelectedRule == null) return string.Empty;
                var scopedIds = SelectedRule.Rule.ScopedFolderIds;
                if (scopedIds == null || scopedIds.Count == 0)
                {
                    return "Applies to all monitored folders";
                }

                var folders = _orchestrator.ConfigService.CurrentConfig.WatchFolders;
                var names = scopedIds
                    .Select(id => folders.FirstOrDefault(f => f.Id == id))
                    .Where(f => f != null)
                    .Select(f => string.IsNullOrWhiteSpace(f!.DisplayName) ? f.FolderPath : f.DisplayName)
                    .ToList();

                return names.Count == 0
                    ? "Scoped folder(s) no longer exist"
                    : $"Scoped to: {string.Join(", ", names)}";
            }
        }

        public ICommand AddRuleCommand { get; }
        public ICommand EditRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand DuplicateRuleCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand LoadInPlacePresetsCommand { get; }

        public event Action<Rule>? RequestEditRule;
        public event Action? RulesChanged;

        public RulesViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _notifications = notifications;

            AddRuleCommand = new RelayCommand(AddRule);
            EditRuleCommand = new RelayCommand(EditRule, () => SelectedRule != null);
            RemoveRuleCommand = new RelayCommand(async () => await RemoveRuleAsync(), () => SelectedRule != null);
            DuplicateRuleCommand = new RelayCommand(DuplicateRule, () => SelectedRule != null);
            MoveUpCommand = new RelayCommand(MoveUp, () => SelectedRule != null && Rules.IndexOf(SelectedRule) > 0);
            MoveDownCommand = new RelayCommand(MoveDown, () => SelectedRule != null && Rules.IndexOf(SelectedRule) < Rules.Count - 1);
            LoadInPlacePresetsCommand = new RelayCommand(async () => await LoadInPlacePresetsAsync());

            LoadRules();
        }

        public void LoadRules()
        {
            string? previousSelectedId = SelectedRule?.Id;

            Rules.Clear();
            var sorted = _orchestrator.ConfigService.CurrentConfig.Rules.OrderBy(r => r.Priority).ToList();
            foreach (var rule in sorted)
            {
                Rules.Add(new RuleItemViewModel(rule, SaveAndApply));
            }

            ApplyFilter();

            SelectedRule = (!string.IsNullOrEmpty(previousSelectedId)
                ? FilteredRules.FirstOrDefault(r => r.Id == previousSelectedId)
                : null) ?? FilteredRules.FirstOrDefault() ?? Rules.FirstOrDefault();

            RefreshSelectedRuleInspector();
            RulesChanged?.Invoke();
        }

        public void ApplyFilter()
        {
            var previousId = SelectedRule?.Id;
            FilteredRules.Clear();

            string query = SearchQuery.Trim();
            foreach (var item in Rules)
            {
                if (string.IsNullOrEmpty(query) ||
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredRules.Add(item);
                }
            }

            if (SelectedRule == null || !FilteredRules.Contains(SelectedRule))
            {
                SelectedRule = (!string.IsNullOrEmpty(previousId)
                    ? FilteredRules.FirstOrDefault(r => r.Id == previousId)
                    : null) ?? FilteredRules.FirstOrDefault();
            }
        }

        public void NotifyRulesChanged()
        {
            SelectedRule?.NotifyUpdated();
            RefreshSelectedRuleInspector();
            RulesChanged?.Invoke();
        }

        private void RefreshSelectedRuleInspector()
        {
            SelectedRulePipelinePreview.Clear();
            if (SelectedRule?.Rule.Steps != null)
            {
                foreach (var step in SelectedRule.Rule.Steps)
                {
                    SelectedRulePipelinePreview.Add(step);
                }
            }
            OnPropertyChanged(nameof(SelectedRuleFolderScopeSummary));
        }

        private async Task LoadInPlacePresetsAsync()
        {
            var existingPresets = _orchestrator.ConfigService.CurrentConfig.Rules
                .Where(r => r.Id.StartsWith("rule-inplace-", StringComparison.OrdinalIgnoreCase) ||
                            r.Id.StartsWith("preset-inplace-", StringComparison.OrdinalIgnoreCase) ||
                            r.Name.StartsWith("In-Place:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            bool overwrite = false;
            if (existingPresets.Count > 0)
            {
                if (_notifications != null)
                {
                    bool confirmed = await _notifications.ConfirmAsync(
                        "Restore In-Place Presets",
                        $"In-place organizer rules are already present ({existingPresets.Count} detected). Reset them to the 7 default presets?",
                        confirmLabel: "Reset Presets",
                        cancelLabel: "Cancel");

                    if (!confirmed) return;
                }
                overwrite = true;
            }
            else
            {
                if (_notifications != null)
                {
                    bool confirmed = await _notifications.ConfirmAsync(
                        "Load In-Place Presets",
                        "Add 7 In-Place Organization rules (Programs, Documents, Compressed, Pictures, Videos, Audio, Others) inside each monitored directory?",
                        confirmLabel: "Load Presets",
                        cancelLabel: "Cancel");

                    if (!confirmed) return;
                }
            }

            _orchestrator.ConfigService.LoadInPlaceOrganizationPresets(overwrite);
            LoadRules();
            SelectedRule = Rules.FirstOrDefault(r => r.Id.StartsWith("rule-inplace-", StringComparison.OrdinalIgnoreCase) ||
                                                    r.Id.StartsWith("preset-inplace-", StringComparison.OrdinalIgnoreCase)) ?? Rules.FirstOrDefault();

            _notifications?.ShowToast(
                "In-Place Presets Loaded",
                "7 category rules are active for in-place organization inside monitored folders.",
                ToastSeverity.Success);
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
            ApplyFilter();
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
                    DestinationTemplate = s.DestinationTemplate,
                    RenamePattern = s.RenamePattern,
                    CaseTransform = s.CaseTransform,
                    ConflictResolution = s.ConflictResolution,
                    ExtractDestination = s.ExtractDestination,
                    CustomExtractPath = s.CustomExtractPath,
                    ArchivePostAction = s.ArchivePostAction,
                    ArchiveMoveTargetFolder = s.ArchiveMoveTargetFolder,
                    EnqueueExtractedFiles = s.EnqueueExtractedFiles,
                    RegexPattern = s.RegexPattern,
                    RegexSource = s.RegexSource,
                    OlderThanDays = s.OlderThanDays,
                    RemoveEmptyFolders = s.RemoveEmptyFolders,
                    SafeRecycle = s.SafeRecycle,
                    CommandExecutable = s.CommandExecutable,
                    CommandArguments = s.CommandArguments
                }).ToList()
            };

            _orchestrator.ConfigService.CurrentConfig.Rules.Add(copy);
            var vm = new RuleItemViewModel(copy, SaveAndApply);
            Rules.Add(vm);
            ApplyFilter();
            SelectedRule = vm;
            SaveAndApply();

            _notifications?.ShowToast(
                "Rule Duplicated",
                $"Created '{copy.Name}'.",
                ToastSeverity.Success);
        }

        private async Task RemoveRuleAsync()
        {
            if (SelectedRule == null) return;
            var target = SelectedRule;

            if (_notifications != null)
            {
                bool confirmed = await _notifications.ConfirmAsync(
                    "Delete Automation Rule",
                    $"Delete rule '{target.Name}'? This cannot be undone.",
                    confirmLabel: "Delete Rule",
                    cancelLabel: "Cancel",
                    isDestructive: true);

                if (!confirmed) return;
            }

            _orchestrator.ConfigService.CurrentConfig.Rules.Remove(target.Rule);
            Rules.Remove(target);
            ReindexPriorities();
            ApplyFilter();
            SelectedRule = FilteredRules.FirstOrDefault() ?? Rules.FirstOrDefault();
            SaveAndApply();

            _notifications?.ShowToast(
                "Rule Deleted",
                $"Removed '{target.Name}'.",
                ToastSeverity.Info);
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
                ReindexPriorities();
                ApplyFilter();
                SelectedRule = item;
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
                ReindexPriorities();
                ApplyFilter();
                SelectedRule = item;
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
            NotifyRulesChanged();
        }
    }
}
