using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FileOrgy.App.Services;
using FileOrgy.App.ViewModels;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using UserControl = System.Windows.Controls.UserControl;
using WpfBrush = System.Windows.Media.Brush;

namespace FileOrgy.App.Views
{
    public partial class OverviewView : UserControl
    {
        public OverviewView()
        {
            InitializeComponent();
        }

        private void OnSimulatorDragEnter(object sender, DragEventArgs e)
        {
            HandleDragCheck(e);
        }

        private void OnSimulatorDragOver(object sender, DragEventArgs e)
        {
            HandleDragCheck(e);
        }

        private void OnSimulatorDragLeave(object sender, DragEventArgs e)
        {
            ResetDropZoneHighlight();
        }

        private void OnSimulatorDrop(object sender, DragEventArgs e)
        {
            ResetDropZoneHighlight();

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths || droppedPaths.Length == 0)
            {
                return;
            }

            string? firstFile = droppedPaths.FirstOrDefault(File.Exists);
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(firstFile))
            {
                vm.Notifications.ShowToast(
                    "File Required",
                    "Drop an individual file (not a folder) to run a dry-run pipeline simulation.",
                    ToastSeverity.Info);
                return;
            }

            vm.PipelinePreviewVM.SimulatePath(firstFile);
            e.Handled = true;
        }

        private void HandleDragCheck(DragEventArgs e)
        {
            bool hasFile = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                hasFile = paths.Any(File.Exists);
            }

            e.Effects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
            if (hasFile && TryFindResource("Brush.Accent") is WpfBrush accentBrush)
            {
                DropZoneBorder.BorderBrush = accentBrush;
            }
            else
            {
                ResetDropZoneHighlight();
            }

            e.Handled = true;
        }

        private void ResetDropZoneHighlight()
        {
            DropZoneBorder.ClearValue(System.Windows.Controls.Border.BorderBrushProperty);
        }
    }
}
