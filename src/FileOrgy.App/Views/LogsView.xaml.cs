using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace FileOrgy.App.Views
{
    public partial class LogsView : UserControl
    {
        public LogsView()
        {
            InitializeComponent();
        }

        private void OnCloseDrawerClick(object sender, RoutedEventArgs e)
        {
            LogsDataGrid.SelectedItem = null;
        }
    }
}
