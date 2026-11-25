using System.Windows;
using FileOrgy.App.Services;

namespace FileOrgy.App.Views
{
    public partial class RuleGuideDialog : Window
    {
        public RuleGuideDialog()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ThemeService.ApplyWindowDarkMode(this, ThemeService.IsDark);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
