using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FileOrgy.App.Services;
using FileOrgy.Core.Services;
using WpfClipboard = System.Windows.Clipboard;

namespace FileOrgy.App.ViewModels
{
    public class VariableItemViewModel : ViewModelBase
    {
        private string _key;
        private string _value;
        private readonly Action _onChanged;

        public string Key
        {
            get => _key;
            set
            {
                if (SetProperty(ref _key, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(TokenSyntax));
                    _onChanged();
                }
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value ?? string.Empty))
                {
                    _onChanged();
                }
            }
        }

        public string TokenSyntax => $"{{var:{Key}}}";

        public VariableItemViewModel(string key, string value, Action onChanged)
        {
            _key = key;
            _value = value;
            _onChanged = onChanged;
        }
    }

    public class KeywordListItemViewModel : ViewModelBase
    {
        private string _name;
        private string _keywordsText;
        private string _newKeywordInput = string.Empty;
        private readonly Action _onChanged;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value ?? string.Empty))
                {
                    _onChanged();
                }
            }
        }

        public string KeywordsText
        {
            get => _keywordsText;
            set
            {
                if (SetProperty(ref _keywordsText, value ?? string.Empty))
                {
                    SyncChipsFromText();
                    OnPropertyChanged(nameof(ChipCount));
                    _onChanged();
                }
            }
        }

        public string NewKeywordInput
        {
            get => _newKeywordInput;
            set => SetProperty(ref _newKeywordInput, value ?? string.Empty);
        }

        public int ChipCount => KeywordChips.Count;

        public ObservableCollection<string> KeywordChips { get; } = new();

        public ICommand AddKeywordChipCommand { get; }
        public ICommand RemoveKeywordChipCommand { get; }
        public ICommand BulkAddKeywordsCommand { get; }

        public KeywordListItemViewModel(string name, IEnumerable<string> keywords, Action onChanged)
        {
            _name = name;
            var initial = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                          ?? new List<string>();
            _keywordsText = string.Join(", ", initial);
            _onChanged = onChanged;

            SyncChipsFromText();

            AddKeywordChipCommand = new RelayCommand(AddKeywordChip);
            RemoveKeywordChipCommand = new RelayCommand(param =>
            {
                if (param is string chip)
                {
                    RemoveKeywordChip(chip);
                }
            });
            BulkAddKeywordsCommand = new RelayCommand(param => BulkAddKeywords(param as string));
        }

        public List<string> GetKeywordsList()
        {
            return KeywordChips.ToList();
        }

        private void SyncChipsFromText()
        {
            KeywordChips.Clear();
            var parsed = (_keywordsText ?? string.Empty)
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var item in parsed)
            {
                KeywordChips.Add(item);
            }
        }

        private void AddKeywordChip()
        {
            BulkAddKeywords(NewKeywordInput);
            NewKeywordInput = string.Empty;
        }

        private void BulkAddKeywords(string? rawInput)
        {
            string source = !string.IsNullOrWhiteSpace(rawInput) ? rawInput : NewKeywordInput;
            if (string.IsNullOrWhiteSpace(source)) return;

            var tokens = source
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t));

            bool addedAny = false;
            foreach (var token in tokens)
            {
                if (!KeywordChips.Any(c => string.Equals(c, token, StringComparison.OrdinalIgnoreCase)))
                {
                    KeywordChips.Add(token);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                _keywordsText = string.Join(", ", KeywordChips);
                OnPropertyChanged(nameof(KeywordsText));
                OnPropertyChanged(nameof(ChipCount));
                _onChanged();
            }

            if (ReferenceEquals(source, NewKeywordInput))
            {
                NewKeywordInput = string.Empty;
            }
        }

        private void RemoveKeywordChip(string chip)
        {
            var existing = KeywordChips.FirstOrDefault(c => string.Equals(c, chip, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                KeywordChips.Remove(existing);
                _keywordsText = string.Join(", ", KeywordChips);
                OnPropertyChanged(nameof(KeywordsText));
                OnPropertyChanged(nameof(ChipCount));
                _onChanged();
            }
        }
    }

    public class VariablesViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly NotificationService? _notifications;
        private VariableItemViewModel? _selectedVariable;
        private KeywordListItemViewModel? _selectedKeywordList;
        private string _newVariableKey = string.Empty;
        private string _newVariableValue = string.Empty;
        private string _newKeywordListName = string.Empty;

        public ObservableCollection<VariableItemViewModel> Variables { get; } = new();
        public ObservableCollection<KeywordListItemViewModel> KeywordLists { get; } = new();

        public VariableItemViewModel? SelectedVariable
        {
            get => _selectedVariable;
            set
            {
                if (SetProperty(ref _selectedVariable, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public KeywordListItemViewModel? SelectedKeywordList
        {
            get => _selectedKeywordList;
            set
            {
                if (SetProperty(ref _selectedKeywordList, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string NewVariableKey
        {
            get => _newVariableKey;
            set => SetProperty(ref _newVariableKey, value ?? string.Empty);
        }

        public string NewVariableValue
        {
            get => _newVariableValue;
            set => SetProperty(ref _newVariableValue, value ?? string.Empty);
        }

        public string NewKeywordListName
        {
            get => _newKeywordListName;
            set => SetProperty(ref _newKeywordListName, value ?? string.Empty);
        }

        public ICommand AddVariableCommand { get; }
        public ICommand AddVariableWithInputsCommand { get; }
        public ICommand RemoveVariableCommand { get; }
        public ICommand CopyVariableTokenCommand { get; }
        public ICommand AddKeywordListCommand { get; }
        public ICommand AddNamedKeywordListCommand { get; }
        public ICommand RemoveKeywordListCommand { get; }

        public VariablesViewModel(FileOrgyOrchestrator orchestrator, NotificationService? notifications = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _notifications = notifications;

            AddVariableCommand = new RelayCommand(AddVariable);
            AddVariableWithInputsCommand = new RelayCommand(AddVariableWithInputs);
            RemoveVariableCommand = new RelayCommand(
                param => RemoveVariable(param as VariableItemViewModel),
                param => (param as VariableItemViewModel ?? SelectedVariable) != null);
            CopyVariableTokenCommand = new RelayCommand(
                param => CopyVariableToken(param as VariableItemViewModel ?? SelectedVariable));
            AddKeywordListCommand = new RelayCommand(AddKeywordList);
            AddNamedKeywordListCommand = new RelayCommand(AddNamedKeywordList);
            RemoveKeywordListCommand = new RelayCommand(
                param => RemoveKeywordList(param as KeywordListItemViewModel),
                param => (param as KeywordListItemViewModel ?? SelectedKeywordList) != null);

            LoadData();
        }

        public void LoadData()
        {
            Variables.Clear();
            foreach (var kvp in _orchestrator.ConfigService.CurrentConfig.CustomVariables)
            {
                Variables.Add(new VariableItemViewModel(kvp.Key, kvp.Value, SaveVariables));
            }
            SelectedVariable = Variables.FirstOrDefault();

            KeywordLists.Clear();
            foreach (var kvp in _orchestrator.ConfigService.CurrentConfig.KeywordLists)
            {
                KeywordLists.Add(new KeywordListItemViewModel(kvp.Key, kvp.Value, SaveKeywordLists));
            }
            SelectedKeywordList = KeywordLists.FirstOrDefault();
        }

        private void AddVariable()
        {
            string baseKey = "NewVariable";
            int counter = 1;
            while (Variables.Any(v => v.Key.Equals(baseKey, StringComparison.OrdinalIgnoreCase)))
            {
                baseKey = $"NewVariable{counter++}";
            }

            var vm = new VariableItemViewModel(baseKey, "Value", SaveVariables);
            Variables.Add(vm);
            SelectedVariable = vm;
            SaveVariables();
        }

        private void AddVariableWithInputs()
        {
            string key = NewVariableKey.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                _notifications?.ShowToast(
                    "Variable Key Required",
                    "Enter a variable key name before adding.",
                    ToastSeverity.Warning);
                return;
            }

            if (Variables.Any(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                _notifications?.ShowToast(
                    "Duplicate Variable",
                    $"A variable named '{key}' already exists.",
                    ToastSeverity.Warning);
                return;
            }

            var vm = new VariableItemViewModel(key, NewVariableValue.Trim(), SaveVariables);
            Variables.Add(vm);
            SelectedVariable = vm;
            SaveVariables();

            NewVariableKey = string.Empty;
            NewVariableValue = string.Empty;

            _notifications?.ShowToast(
                "Variable Added",
                $"Added {vm.TokenSyntax}.",
                ToastSeverity.Success);
        }

        private void RemoveVariable(VariableItemViewModel? target = null)
        {
            var item = target ?? SelectedVariable;
            if (item == null) return;
            Variables.Remove(item);
            SelectedVariable = Variables.FirstOrDefault();
            SaveVariables();

            _notifications?.ShowToast(
                "Variable Removed",
                $"Removed '{item.Key}'.",
                ToastSeverity.Info);
        }

        private void CopyVariableToken(VariableItemViewModel? target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.Key)) return;
            try
            {
                WpfClipboard.SetText(target.TokenSyntax);
                _notifications?.ShowToast(
                    "Copied Token",
                    $"Copied {target.TokenSyntax} to clipboard.",
                    ToastSeverity.Success);
            }
            catch { }
        }

        private void AddKeywordList()
        {
            string baseName = "NewKeywordList";
            int counter = 1;
            while (KeywordLists.Any(k => k.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
            {
                baseName = $"NewKeywordList{counter++}";
            }

            var vm = new KeywordListItemViewModel(baseName, new[] { "keyword1", "keyword2" }, SaveKeywordLists);
            KeywordLists.Add(vm);
            SelectedKeywordList = vm;
            SaveKeywordLists();
        }

        private void AddNamedKeywordList()
        {
            string name = NewKeywordListName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                _notifications?.ShowToast(
                    "List Name Required",
                    "Enter a name for the new keyword list.",
                    ToastSeverity.Warning);
                return;
            }

            if (KeywordLists.Any(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _notifications?.ShowToast(
                    "Duplicate Keyword List",
                    $"A keyword list named '{name}' already exists.",
                    ToastSeverity.Warning);
                return;
            }

            var vm = new KeywordListItemViewModel(name, Array.Empty<string>(), SaveKeywordLists);
            KeywordLists.Add(vm);
            SelectedKeywordList = vm;
            SaveKeywordLists();

            NewKeywordListName = string.Empty;

            _notifications?.ShowToast(
                "Keyword List Created",
                $"Created keyword list '{name}'.",
                ToastSeverity.Success);
        }

        private void RemoveKeywordList(KeywordListItemViewModel? target = null)
        {
            var item = target ?? SelectedKeywordList;
            if (item == null) return;
            KeywordLists.Remove(item);
            SelectedKeywordList = KeywordLists.FirstOrDefault();
            SaveKeywordLists();

            _notifications?.ShowToast(
                "Keyword List Removed",
                $"Removed '{item.Name}'.",
                ToastSeverity.Info);
        }

        private void SaveVariables()
        {
            var dict = _orchestrator.ConfigService.CurrentConfig.CustomVariables;
            dict.Clear();
            foreach (var v in Variables)
            {
                if (!string.IsNullOrWhiteSpace(v.Key))
                {
                    dict[v.Key.Trim()] = v.Value ?? string.Empty;
                }
            }
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
        }

        private void SaveKeywordLists()
        {
            var dict = _orchestrator.ConfigService.CurrentConfig.KeywordLists;
            dict.Clear();
            foreach (var kl in KeywordLists)
            {
                if (!string.IsNullOrWhiteSpace(kl.Name))
                {
                    dict[kl.Name.Trim()] = kl.GetKeywordsList();
                }
            }
            _orchestrator.ConfigService.SaveConfig(_orchestrator.ConfigService.CurrentConfig);
        }
    }
}
