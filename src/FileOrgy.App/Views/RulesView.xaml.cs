using System.Windows;
using System.Windows.Input;
using FileOrgy.App.ViewModels;

namespace FileOrgy.App.Views
{
    public partial class RulesView : System.Windows.Controls.UserControl
    {
        public RulesView()
        {
            InitializeComponent();
        }

        private void OnOpenRuleGuideClick(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var dialog = new RuleGuideDialog
            {
                Owner = owner,
                Icon = owner?.Icon
            };
            dialog.ShowDialog();
        }

        private void OnDryRunSelectedRuleClick(object sender, RoutedEventArgs e)
        {
            RunSelectedRuleDryRun();
        }

        private void OnDryRunPathKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            RunSelectedRuleDryRun();
            e.Handled = true;
        }

        private void RunSelectedRuleDryRun()
        {
            if (DataContext is MainViewModel vm && vm.RulesVM.SelectedRule != null)
            {
                vm.PipelinePreviewVM.SimulateRuleDraft(vm.RulesVM.SelectedRule.Rule);
            }
        }
    }
}
