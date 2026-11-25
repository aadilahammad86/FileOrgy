using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FileOrgy.App.ViewModels;

namespace FileOrgy.App.Views
{
    public partial class WatchFoldersView : System.Windows.Controls.UserControl
    {
        public WatchFoldersView()
        {
            InitializeComponent();
        }

        private void OnIgnorePatternKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var folder = (sender as FrameworkElement)?.DataContext as WatchFolderItemViewModel
                         ?? (DataContext as WatchFoldersViewModel)?.SelectedFolder;

            if (folder != null && folder.AddIgnorePatternCommand.CanExecute(null))
            {
                folder.AddIgnorePatternCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnDebounceDelayKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is System.Windows.Controls.TextBox tb)
            {
                BindingOperations.GetBindingExpression(tb, System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
            }
        }
    }
}
