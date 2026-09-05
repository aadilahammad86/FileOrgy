using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using FileOrgy.Core.Models;
using FileOrgy.Core.Services;

namespace FileOrgy.App.ViewModels
{
    public class RuleConditionItemViewModel : ViewModelBase
    {
        private readonly RuleCondition _condition;

        public RuleTarget Target
        {
            get => _condition.Target;
            set
            {
                if (_condition.Target != value)
                {
                    _condition.Target = value;
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
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsKeywordListVisible));
                }
            }
        }

        public string Value
        {
            get => _condition.Value;
            set
            {
                if (_condition.Value != value)
                {
                    _condition.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? KeywordListName
        {
            get => _condition.KeywordListName;
            set
            {
                if (_condition.KeywordListName != value)
                {
                    _condition.KeywordListName = value;
                    OnPropertyChanged();
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

        public RuleCondition Model => _condition;

        public RuleConditionItemViewModel(RuleCondition condition)
        {
            _condition = condition;
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

        public string RenamePattern
        {
            get => _step.RenamePattern;
            set
            {
                if (_step.RenamePattern != value)
                {
                    _step.RenamePattern = value;
                    OnPropertyChanged();
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
                    _step.DestinationTemplate = value;
                    OnPropertyChanged();
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

        public ArchiveExtractDestination ExtractDestination
        {
            get => _step.ExtractDestination;
            set
            {
                if (_step.ExtractDestination != value)
                {
                    _step.ExtractDestination = value;
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
                    _step.RegexPattern = value;
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
                    _step.RegexSource = value;
                    OnPropertyChanged();
                }
            }
        }

        public int OlderThanDays
        {
            get => _step.OlderThanDays;
            set
            {
                if (_step.OlderThanDays != value)
                {
                    _step.OlderThanDays = value;
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
                    _step.CommandExecutable = value;
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
                    _step.CommandArguments = value;
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

        public WorkflowStep Model => _step;

        public WorkflowStepItemViewModel(WorkflowStep step)
        {
            _step = step;
        }
    }

    public class RuleEditViewModel : ViewModelBase
    {
        private readonly Rule _rule;
        private readonly ConfigService _configService;
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
                }
            }
        }

        public ObservableCollection<RuleConditionItemViewModel> Conditions { get; } = new();
        public ObservableCollection<WorkflowStepItemViewModel> Steps { get; } = new();
        public List<string> AvailableKeywordLists => _configService.CurrentConfig.KeywordLists.Keys.ToList();

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

        public Rule Rule => _rule;

        public RuleEditViewModel(Rule rule, ConfigService configService)
        {
            _rule = rule;
            _configService = configService;

            foreach (var cond in _rule.Conditions)
            {
                Conditions.Add(new RuleConditionItemViewModel(cond));
            }
            SelectedCondition = Conditions.FirstOrDefault();

            foreach (var step in _rule.Steps)
            {
                Steps.Add(new WorkflowStepItemViewModel(step));
            }
            SelectedStep = Steps.FirstOrDefault();

            AddConditionCommand = new RelayCommand(AddCondition);
            RemoveConditionCommand = new RelayCommand(RemoveCondition, () => SelectedCondition != null);
            AddStepCommand = new RelayCommand(AddStep);
            RemoveStepCommand = new RelayCommand(RemoveStep, () => SelectedStep != null);
            MoveStepUpCommand = new RelayCommand(MoveStepUp, () => SelectedStep != null && Steps.IndexOf(SelectedStep) > 0);
            MoveStepDownCommand = new RelayCommand(MoveStepDown, () => SelectedStep != null && Steps.IndexOf(SelectedStep) < Steps.Count - 1);
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
            var vm = new RuleConditionItemViewModel(newCond);
            Conditions.Add(vm);
            SelectedCondition = vm;
        }

        private void RemoveCondition()
        {
            if (SelectedCondition == null) return;
            _rule.Conditions.Remove(SelectedCondition.Model);
            Conditions.Remove(SelectedCondition);
            SelectedCondition = Conditions.FirstOrDefault();
        }

        private void AddStep()
        {
            var newStep = new WorkflowStep
            {
                StepType = StepType.SmartRename,
                Name = "Smart Rename",
                RenamePattern = "{doc_date:yyyy-MM-dd}_{basename}{dotext}"
            };
            _rule.Steps.Add(newStep);
            var vm = new WorkflowStepItemViewModel(newStep);
            Steps.Add(vm);
            SelectedStep = vm;
        }

        private void RemoveStep()
        {
            if (SelectedStep == null) return;
            _rule.Steps.Remove(SelectedStep.Model);
            Steps.Remove(SelectedStep);
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
