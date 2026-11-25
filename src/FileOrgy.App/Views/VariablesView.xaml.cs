using System.Windows;
using System.Windows.Input;
using FileOrgy.App.ViewModels;

namespace FileOrgy.App.Views
{
    public partial class VariablesView : System.Windows.Controls.UserControl
    {
        public VariablesView()
        {
            InitializeComponent();
        }

        private void OnNewVariableKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (DataContext is VariablesViewModel vm && vm.AddVariableWithInputsCommand.CanExecute(null))
            {
                vm.AddVariableWithInputsCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnNewKeywordListKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (DataContext is VariablesViewModel vm && vm.AddNamedKeywordListCommand.CanExecute(null))
            {
                vm.AddNamedKeywordListCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnNewKeywordInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var list = (sender as FrameworkElement)?.DataContext as KeywordListItemViewModel
                       ?? (DataContext as VariablesViewModel)?.SelectedKeywordList;

            if (list != null && list.AddKeywordChipCommand.CanExecute(null))
            {
                list.AddKeywordChipCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
