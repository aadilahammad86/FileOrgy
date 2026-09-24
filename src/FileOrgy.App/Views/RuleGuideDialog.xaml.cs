using System.Windows;

namespace FileOrgy.App.Views
{
    public partial class RuleGuideDialog : Window
    {
        public RuleGuideDialog()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
