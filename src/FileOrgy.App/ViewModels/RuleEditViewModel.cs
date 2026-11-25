using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;
using FileOrgy.Core.Utils;
using Forms = System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace FileOrgy.App.ViewModels
{
    public class FolderScopeChoiceViewModel : ViewModelBase
    {
        private bool _isSelected;
        private readonly Action _onChanged;

        public string Id { get; }
        public string DisplayName { get; }
        public string FolderPath { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    _onChanged();
                }
            }
        }

        public FolderScopeChoiceViewModel(string id, string displayName, string folderPath, bool isSelected, Action onChanged)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(folderPath) : displayName;
            FolderPath = folderPath;
            _isSelected = isSelected;
            _onChanged = onChanged;
        }
    }

    public class RuleConditionItemViewModel : ViewModelBase
    {
        private readonly RuleCondition _condition;
        private readonly List<string> _availableKeywordLists;
        private readonly IReadOnlyDictionary<string, List<string>>? _keywordListsDict;

        public IReadOnlyList<string> AvailableKeywordLists => _availableKeywordLists;

        public string KeywordListPreview
        {
            get
            {
                if (!IsKeywordListVisible) return string.Empty;
                var key = !string.IsNullOrEmpty(KeywordListName) ? KeywordListName : Value;
                if (string.IsNullOrWhiteSpace(key)) return "Select a keyword list from the dropdown";

                if (_keywordListsDict != null && _keywordListsDict.TryGetValue(key, out var list) && list.Count > 0)
                {
                    var preview = string.Join(", ", list.Take(8));
                    var extra = list.Count > 8 ? $" (+{list.Count - 8} more)" : "";
                    return $"Matches {list.Count} items: {preview}{extra}";
                }

                return $"List: {key}";
            }
        }

        public RuleTarget Target
        {
            get => _condition.Target;
            set
            {
                if (_condition.Target != value)
                {
                    _condition.Target = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomVariableVisible));
                }
            }
        }

        public string? TargetVariable
        {
            get => _condition.TargetVariable;
            set
            {
                if (_condition.TargetVariable != value)
                {
                    _condition.TargetVariable = value;
                    OnPropertyChanged();
                }
            }
        }

        public ConditionOperator Operator
        {
            get => _condition.Operator;
            set
            {
                if (_condition.Operator != value)
                {
                    _condition.Operator = value;
                    if (IsKeywordListVisible)
                    {
                        if (string.IsNullOrEmpty(_condition.KeywordListName) && !string.IsNullOrEmpty(_condition.Value))
                        {
                            _condition.KeywordListName = _condition.Value;
                        }
                        else if (!string.IsNullOrEmpty(_condition.KeywordListName) && string.IsNullOrEmpty(_condition.Value))
                        {
                            _condition.Value = _condition.KeywordListName;
                        }
                        else if (string.IsNullOrEmpty(_condition.KeywordListName) && string.IsNullOrEmpty(_condition.Value) && _availableKeywordLists.Count > 0)
                        {
                            _condition.KeywordListName = _availableKeywordLists[0];
                            _condition.Value = _availableKeywordLists[0];
                        }
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsKeywordListVisible));
                    OnPropertyChanged(nameof(IsValueVisible));
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(KeywordListName));
                    OnPropertyChanged(nameof(KeywordListPreview));
                }
            }
        }

        public string Value
        {
            get => IsKeywordListVisible && !string.IsNullOrEmpty(_condition.KeywordListName)
                ? _condition.KeywordListName
                : _condition.Value;
            set
            {
                var val = value ?? string.Empty;
                if (_condition.Value != val || (IsKeywordListVisible && _condition.KeywordListName != val))
                {
                    _condition.Value = val;
                    if (IsKeywordListVisible)
                    {
                        _condition.KeywordListName = val;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(KeywordListName));
                    OnPropertyChanged(nameof(KeywordListPreview));
                }
            }
        }

        public string? KeywordListName
        {
            get => !string.IsNullOrEmpty(_condition.KeywordListName)
                ? _condition.KeywordListName
                : _condition.Value;
            set
            {
                if (_condition.KeywordListName != value)
                {
                    _condition.KeywordListName = value;
                    _condition.Value = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(KeywordListPreview));
                }
            }
        }

        public bool CaseSensitive
        {
            get => _condition.CaseSensitive;
            set
            {
                if (_condition.CaseSensitive != value)
                {
                    _condition.CaseSensitive = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsKeywordListVisible =>
            Operator == ConditionOperator.InKeywordList || Operator == ConditionOperator.NotInKeywordList;

        public bool IsValueVisible =>
            !IsKeywordListVisible && Operator != ConditionOperator.IsEmptyFile;

        public bool IsCustomVariableVisible =>
            Target == RuleTarget.CustomVariable;

        public RuleCondition Model => _condition;

        public RuleConditionItemViewModel(
            RuleCondition condition,
            List<string>? availableKeywordLists = null,
            IReadOnlyDictionary<string, List<string>>? keywordListsDict = null)
        {
            _condition = condition;
            _availableKeywordLists = availableKeywordLists ?? new List<string>();
            _keywordListsDict = keywordListsDict;

            if (IsKeywordListVisible)
            {
                if (string.IsNullOrEmpty(_condition.Value) && !string.IsNullOrEmpty(_condition.KeywordListName))
                {
                    _condition.Value = _condition.KeywordListName;
                }
                else if (string.IsNullOrEmpty(_condition.KeywordListName) && !string.IsNullOrEmpty(_condition.Value))
                {
                    _condition.KeywordListName = _condition.Value;
                }
            }
        }
    }

    public class WorkflowStepItemViewModel : ViewModelBase
    {
        private readonly WorkflowStep _step;

        public string Name
        {
            get => string.IsNullOrEmpty(_step.Name) ? _step.StepType.ToString() : _step.Name;
            set
            {
                if (_step.Name != value)
                {
                    _step.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public StepType StepType
        {
            get => _step.StepType;
            set
            {
                if (_step.StepType != value)
                {
                    _step.StepType = value;
                    if (string.IsNullOrEmpty(_step.Name))
                    {
                        Name = value.ToString();
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRenameVisible));
                    OnPropertyChanged(nameof(IsMoveOrCopyVisible));
                    OnPropertyChanged(nameof(IsArchiveVisible));
                    OnPropertyChanged(nameof(IsRegexVisible));
                    OnPropertyChanged(nameof(IsCleanupVisible));
                    OnPropertyChanged(nameof(IsCommandVisible));
                    OnPropertyChanged(nameof(IsInspectVisible));
                    OnPropertyChanged(nameof(IsRecycleOrDeleteVisible));
                    OnPropertyChanged(nameof(IsCustomExtractPathVisible));
                    OnPropertyChanged(nameof(IsArchiveMoveTargetVisible));
                    OnPropertyChanged(nameof(LivePatternPreview));
                }
            }
        }

        public bool Enabled
        {
            get => _step.Enabled;
            set
            {
                if (_step.Enabled != value)
                {
                    _step.Enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ContinueOnError
        {
            get => _step.ContinueOnError;
            set
            {
                if (_step.ContinueOnError != value)
                {
                    _step.ContinueOnError = value;
                    OnPropertyChanged();
                }
            }
        }

        public string RenamePattern
        {
            get => _step.RenamePattern;
            set
            {
                if (_step.RenamePattern != value)
                {
                    _step.RenamePattern = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LivePatternPreview));
                }
            }
        }

        public string DestinationTemplate
        {
            get => _step.DestinationTemplate;
            set
            {
                if (_step.DestinationTemplate != value)
                {
                    _step.DestinationTemplate = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LivePatternPreview));
                }
            }
        }

        public ConflictResolution ConflictResolution
        {
            get => _step.ConflictResolution;
            set
            {
                if (_step.ConflictResolution != value)
                {
                    _step.ConflictResolution = value;
                    OnPropertyChanged();
                }
            }
        }

        public CaseTransform CaseTransform
        {
            get => _step.CaseTransform;
            set
            {
                if (_step.CaseTransform != value)
                {
                    _step.CaseTransform = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LivePatternPreview));
                }
            }
        }

        public ArchiveExtractDestination ExtractDestination
        {
            get => _step.ExtractDestination;
            set
            {
                if (_step.ExtractDestination != value)
                {
                    _step.ExtractDestination = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomExtractPathVisible));
                }
            }
        }

        public string CustomExtractPath
        {
            get => _step.CustomExtractPath;
            set
            {
                if (_step.CustomExtractPath != value)
                {
                    _step.CustomExtractPath = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public ArchivePostAction ArchivePostAction
        {
            get => _step.ArchivePostAction;
            set
            {
                if (_step.ArchivePostAction != value)
                {
                    _step.ArchivePostAction = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsArchiveMoveTargetVisible));
                }
            }
        }

        public string ArchiveMoveTargetFolder
        {
            get => _step.ArchiveMoveTargetFolder;
            set
            {
                if (_step.ArchiveMoveTargetFolder != value)
                {
                    _step.ArchiveMoveTargetFolder = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnqueueExtractedFiles
        {
            get => _step.EnqueueExtractedFiles;
            set
            {
                if (_step.EnqueueExtractedFiles != value)
                {
                    _step.EnqueueExtractedFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public string RegexPattern
        {
            get => _step.RegexPattern;
            set
            {
                if (_step.RegexPattern != value)
                {
                    _step.RegexPattern = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public string RegexSource
        {
            get => _step.RegexSource;
            set
            {
                if (_step.RegexSource != value)
                {
                    _step.RegexSource = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public int OlderThanDays
        {
            get => _step.OlderThanDays;
            set
            {
                int clamped = Math.Max(0, value);
                if (_step.OlderThanDays != clamped)
                {
                    _step.OlderThanDays = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public bool RemoveEmptyFolders
        {
            get => _step.RemoveEmptyFolders;
            set
            {
                if (_step.RemoveEmptyFolders != value)
                {
                    _step.RemoveEmptyFolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SafeRecycle
        {
            get => _step.SafeRecycle;
            set
            {
                if (_step.SafeRecycle != value)
                {
                    _step.SafeRecycle = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CommandExecutable
        {
            get => _step.CommandExecutable;
            set
            {
                if (_step.CommandExecutable != value)
                {
                    _step.CommandExecutable = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public string CommandArguments
        {
            get => _step.CommandArguments;
            set
            {
                if (_step.CommandArguments != value)
                {
                    _step.CommandArguments = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsRenameVisible => StepType == StepType.SmartRename;
        public bool IsMoveOrCopyVisible => StepType == StepType.MoveFile || StepType == StepType.CopyFile;
        public bool IsArchiveVisible => StepType == StepType.ExtractArchive;
        public bool IsRegexVisible => StepType == StepType.RegexCapture;
        public bool IsCleanupVisible => StepType == StepType.CleanupFolder;
        public bool IsCommandVisible => StepType == StepType.RunCommand;
        public bool IsInspectVisible => StepType == StepType.InspectDocument;
        public bool IsRecycleOrDeleteVisible => StepType == StepType.RecycleFile || StepType == StepType.PermanentDelete;
        public bool IsCustomExtractPathVisible => IsArchiveVisible && ExtractDestination == ArchiveExtractDestination.CustomFolder;
        public bool IsArchiveMoveTargetVisible => IsArchiveVisible && ArchivePostAction == ArchivePostAction.MoveArchiveToFolder;

        public string LivePatternPreview
        {
            get
            {
                try
                {
                    string sampleDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    string samplePath = Path.Combine(sampleDir, "invoice_2026_sample.pdf");
                    var sampleContext = new WorkflowContext(samplePath)
                    {
                        ExtractedDate = DateTime.Today
                    };

                    if (IsRenameVisible)
                    {
                        string evaluated = SmartRenamer.EvaluateTemplate(RenamePattern, sampleContext);
                        evaluated = SmartRenamer.ApplyCaseTransform(evaluated, CaseTransform);
                        evaluated = PathHelper.SanitizeFileName(evaluated);
                        if (!Path.HasExtension(evaluated))
                        {
                            evaluated += ".pdf";
                        }
                        return evaluated;
                    }

                    if (IsMoveOrCopyVisible)
                    {
                        if (string.IsNullOrWhiteSpace(DestinationTemplate))
                        {
                            return "(enter destination path template)";
                        }
                        string dest = SmartRenamer.EvaluateTemplate(DestinationTemplate, sampleContext);
                        return Path.Combine(dest, "invoice_2026_sample.pdf");
                    }
                }
                catch
                {
                    return "(invalid template)";
                }

                return string.Empty;
            }
        }

        public WorkflowStep Model => _step;

        public ICommand AppendTokenCommand { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand BrowseCustomExtractPathCommand { get; }
        public ICommand BrowseArchiveMoveTargetCommand { get; }
        public ICommand BrowseCommandExecutableCommand { get; }

        public WorkflowStepItemViewModel(WorkflowStep step)
        {
            _step = step;

            AppendTokenCommand = new RelayCommand(param =>
            {
                if (param is string token && !string.IsNullOrEmpty(token))
                {
                    if (IsRenameVisible)
                    {
                        RenamePattern = string.IsNullOrEmpty(RenamePattern) ? token : RenamePattern + token;
                    }
                    else if (IsMoveOrCopyVisible)
                    {
                        var prefix = string.IsNullOrEmpty(DestinationTemplate)
                            ? ""
                            : (DestinationTemplate.EndsWith("\\") || DestinationTemplate.EndsWith("/") ? "" : "\\");
                        DestinationTemplate = (DestinationTemplate ?? string.Empty) + prefix + token;
                    }
                }
            });

            BrowseDestinationCommand = new RelayCommand(() =>
            {
                string? picked = BrowseFolder("Select Destination Folder", DestinationTemplate);
                if (!string.IsNullOrWhiteSpace(picked))
                {
                    DestinationTemplate = picked;
                }
            });

            BrowseCustomExtractPathCommand = new RelayCommand(() =>
            {
                string? picked = BrowseFolder("Select Custom Extraction Folder", CustomExtractPath);
                if (!string.IsNullOrWhiteSpace(picked))
                {
                    CustomExtractPath = picked;
                }
            });

            BrowseArchiveMoveTargetCommand = new RelayCommand(() =>
            {
                string? picked = BrowseFolder("Select Archive Post-Move Folder", ArchiveMoveTargetFolder);
                if (!string.IsNullOrWhiteSpace(picked))
                {
                    ArchiveMoveTargetFolder = picked;
                }
            });

            BrowseCommandExecutableCommand = new RelayCommand(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select Executable or Script",
                    Filter = "Executables & Scripts (*.exe;*.bat;*.cmd;*.ps1)|*.exe;*.bat;*.cmd;*.ps1|All Files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    CommandExecutable = dialog.FileName;
                }
            });
        }

        private static string? BrowseFolder(string description, string currentPath)
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(currentPath) ? currentPath : string.Empty
            };

            return dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath)
                ? dialog.SelectedPath
                : null;
        }
    }

    public class RuleEditViewModel : ViewModelBase
    {
        private readonly Rule _rule;
        private readonly ConfigService? _configService;
        private RuleConditionItemViewModel? _selectedCondition;
        private WorkflowStepItemViewModel? _selectedStep;

        public string Name
        {
            get => _rule.Name;
            set
            {
                if (_rule.Name != value)
                {
                    _rule.Name = value;
                    OnPropertyChanged();
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
                }
            }
        }

        public bool StopOnFirstMatch
        {
            get => _rule.StopOnFirstMatch;
            set
            {
                if (_rule.StopOnFirstMatch != value)
                {
                    _rule.StopOnFirstMatch = value;
                    OnPropertyChanged();
                }
            }
        }

        public ConditionMatchLogic MatchLogic
        {
            get => _rule.MatchLogic;
            set
            {
                if (_rule.MatchLogic != value)
                {
                    _rule.MatchLogic = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMatchAll));
                    OnPropertyChanged(nameof(IsMatchAny));
                }
            }
        }

        public bool IsMatchAll
        {
            get => MatchLogic == ConditionMatchLogic.All;
            set { if (value) MatchLogic = ConditionMatchLogic.All; }
        }

        public bool IsMatchAny
        {
            get => MatchLogic == ConditionMatchLogic.Any;
            set { if (value) MatchLogic = ConditionMatchLogic.Any; }
        }

        public ObservableCollection<FolderScopeChoiceViewModel> FolderScopeChoices { get; } = new();
        public bool IsScopedToAllFolders => FolderScopeChoices.All(c => !c.IsSelected);
        public string FolderScopeSummary
        {
            get
            {
                var selected = FolderScopeChoices.Where(c => c.IsSelected).Select(c => c.DisplayName).ToList();
                return selected.Count == 0
                    ? "All monitored folders"
                    : $"Scoped to: {string.Join(", ", selected)}";
            }
        }

        public ObservableCollection<RuleConditionItemViewModel> Conditions { get; } = new();
        public ObservableCollection<WorkflowStepItemViewModel> Steps { get; } = new();
        public List<string> AvailableKeywordLists => _configService?.CurrentConfig?.KeywordLists?.Keys?.OrderBy(k => k).ToList() ?? new List<string>();

        public RuleConditionItemViewModel? SelectedCondition
        {
            get => _selectedCondition;
            set => SetProperty(ref _selectedCondition, value);
        }

        public WorkflowStepItemViewModel? SelectedStep
        {
            get => _selectedStep;
            set => SetProperty(ref _selectedStep, value);
        }

        public ICommand AddConditionCommand { get; }
        public ICommand RemoveConditionCommand { get; }
        public ICommand AddStepCommand { get; }
        public ICommand RemoveStepCommand { get; }
        public ICommand MoveStepUpCommand { get; }
        public ICommand MoveStepDownCommand { get; }
        public ICommand ClearFolderScopeCommand { get; }

        public Rule Rule => _rule;
        public AppConfiguration ConfigSnapshot => _configService?.CurrentConfig ?? new AppConfiguration();

        public RuleEditViewModel(Rule rule, ConfigService configService)
        {
            _rule = rule;
            _configService = configService;

            var keywordDict = _configService?.CurrentConfig?.KeywordLists;
            var watchFolders = _configService?.CurrentConfig?.WatchFolders ?? new List<WatchFolderConfig>();
            var scopedIds = _rule.ScopedFolderIds ?? new List<string>();

            foreach (var wf in watchFolders)
            {
                bool isSelected = scopedIds.Contains(wf.Id);
                FolderScopeChoices.Add(new FolderScopeChoiceViewModel(wf.Id, wf.DisplayName, wf.FolderPath, isSelected, SyncScopedFolders));
            }

            foreach (var cond in _rule.Conditions)
            {
                Conditions.Add(new RuleConditionItemViewModel(cond, AvailableKeywordLists, keywordDict));
            }
            SelectedCondition = Conditions.FirstOrDefault();

            foreach (var step in _rule.Steps)
            {
                Steps.Add(new WorkflowStepItemViewModel(step));
            }
            SelectedStep = Steps.FirstOrDefault();

            AddConditionCommand = new RelayCommand(AddCondition);
            RemoveConditionCommand = new RelayCommand(p => RemoveCondition(p as RuleConditionItemViewModel));
            AddStepCommand = new RelayCommand(AddStep);
            RemoveStepCommand = new RelayCommand(p => RemoveStep(p as WorkflowStepItemViewModel));
            MoveStepUpCommand = new RelayCommand(MoveStepUp, () => SelectedStep != null && Steps.IndexOf(SelectedStep) > 0);
            MoveStepDownCommand = new RelayCommand(MoveStepDown, () => SelectedStep != null && Steps.IndexOf(SelectedStep) < Steps.Count - 1);
            ClearFolderScopeCommand = new RelayCommand(() =>
            {
                foreach (var choice in FolderScopeChoices)
                {
                    choice.IsSelected = false;
                }
                SyncScopedFolders();
            });
        }

        private void SyncScopedFolders()
        {
            _rule.ScopedFolderIds = FolderScopeChoices
                .Where(c => c.IsSelected)
                .Select(c => c.Id)
                .ToList();
            OnPropertyChanged(nameof(IsScopedToAllFolders));
            OnPropertyChanged(nameof(FolderScopeSummary));
        }

        private void AddCondition()
        {
            var newCond = new RuleCondition
            {
                Target = RuleTarget.Extension,
                Operator = ConditionOperator.Equals,
                Value = "pdf"
            };
            _rule.Conditions.Add(newCond);
            var vm = new RuleConditionItemViewModel(newCond, AvailableKeywordLists, _configService?.CurrentConfig?.KeywordLists);
            Conditions.Add(vm);
            SelectedCondition = vm;
        }

        private void RemoveCondition(RuleConditionItemViewModel? condition = null)
        {
            var target = condition ?? SelectedCondition;
            if (target == null) return;
            _rule.Conditions.Remove(target.Model);
            Conditions.Remove(target);
            SelectedCondition = Conditions.FirstOrDefault();
        }

        private void AddStep()
        {
            var newStep = new WorkflowStep
            {
                StepType = StepType.SmartRename,
                Name = "Rename File",
                RenamePattern = "{basename}{dotext}"
            };
            _rule.Steps.Add(newStep);
            var vm = new WorkflowStepItemViewModel(newStep);
            Steps.Add(vm);
            SelectedStep = vm;
        }

        private void RemoveStep(WorkflowStepItemViewModel? step = null)
        {
            var target = step ?? SelectedStep;
            if (target == null) return;
            _rule.Steps.Remove(target.Model);
            Steps.Remove(target);
            SelectedStep = Steps.FirstOrDefault();
        }

        private void MoveStepUp()
        {
            if (SelectedStep == null) return;
            int index = Steps.IndexOf(SelectedStep);
            if (index > 0)
            {
                var item = SelectedStep;
                var model = item.Model;

                Steps.RemoveAt(index);
                _rule.Steps.RemoveAt(index);

                Steps.Insert(index - 1, item);
                _rule.Steps.Insert(index - 1, model);

                SelectedStep = item;
            }
        }

        private void MoveStepDown()
        {
            if (SelectedStep == null) return;
            int index = Steps.IndexOf(SelectedStep);
            if (index >= 0 && index < Steps.Count - 1)
            {
                var item = SelectedStep;
                var model = item.Model;

                Steps.RemoveAt(index);
                _rule.Steps.RemoveAt(index);

                Steps.Insert(index + 1, item);
                _rule.Steps.Insert(index + 1, model);

                SelectedStep = item;
            }
        }
    }
}
