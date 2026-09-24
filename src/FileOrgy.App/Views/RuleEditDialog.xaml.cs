using System.Windows;
using FileOrgy.App.ViewModels;

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
