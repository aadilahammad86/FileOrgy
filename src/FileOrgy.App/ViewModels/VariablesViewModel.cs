using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FileOrgy.Core.Services;

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
                if (SetProperty(ref _key, value))
                {
                    _onChanged();
                }
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    _onChanged();
                }
            }
        }

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
        private readonly Action _onChanged;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
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
                if (SetProperty(ref _keywordsText, value))
                {
                    _onChanged();
                }
            }
        }

        public KeywordListItemViewModel(string name, IEnumerable<string> keywords, Action onChanged)
        {
            _name = name;
            _keywordsText = string.Join(", ", keywords);
            _onChanged = onChanged;
        }

        public List<string> GetKeywordsList()
        {
            return _keywordsText
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }

    public class VariablesViewModel : ViewModelBase
    {
        private readonly FileOrgyOrchestrator _orchestrator;
        private VariableItemViewModel? _selectedVariable;
        private KeywordListItemViewModel? _selectedKeywordList;

        public ObservableCollection<VariableItemViewModel> Variables { get; } = new();
        public ObservableCollection<KeywordListItemViewModel> KeywordLists { get; } = new();

        public VariableItemViewModel? SelectedVariable
        {
            get => _selectedVariable;
            set => SetProperty(ref _selectedVariable, value);
        }

        public KeywordListItemViewModel? SelectedKeywordList
        {
            get => _selectedKeywordList;
            set => SetProperty(ref _selectedKeywordList, value);
        }

        public ICommand AddVariableCommand { get; }
        public ICommand RemoveVariableCommand { get; }
        public ICommand AddKeywordListCommand { get; }
        public ICommand RemoveKeywordListCommand { get; }

        public VariablesViewModel(FileOrgyOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;

            AddVariableCommand = new RelayCommand(AddVariable);
            RemoveVariableCommand = new RelayCommand(RemoveVariable, () => SelectedVariable != null);
            AddKeywordListCommand = new RelayCommand(AddKeywordList);
            RemoveKeywordListCommand = new RelayCommand(RemoveKeywordList, () => SelectedKeywordList != null);

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

        private void RemoveVariable()
        {
            if (SelectedVariable == null) return;
            Variables.Remove(SelectedVariable);
            SelectedVariable = Variables.FirstOrDefault();
            SaveVariables();
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

        private void RemoveKeywordList()
        {
            if (SelectedKeywordList == null) return;
            KeywordLists.Remove(SelectedKeywordList);
            SelectedKeywordList = KeywordLists.FirstOrDefault();
            SaveKeywordLists();
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
