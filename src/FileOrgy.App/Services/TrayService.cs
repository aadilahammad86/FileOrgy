using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Forms;
using FileOrgy.Core.Services;
using DColor = System.Drawing.Color;
using DPoint = System.Drawing.Point;

namespace FileOrgy.App.Services
{
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Window _mainWindow;
        private readonly FileOrgyOrchestrator _orchestrator;
        private readonly ToolStripMenuItem _pauseMenuItem;
        private bool _isDisposed;

        public event Action? OpenRequested;
        public event Action? ScanRequested;
        public event Action? ExitRequested;

        public TrayService(Window mainWindow, FileOrgyOrchestrator orchestrator)
        {
            _mainWindow = mainWindow;
            _orchestrator = orchestrator;

            _notifyIcon = new NotifyIcon
            {
                Text = "FileOrgy - Active Folder Automation",
                Visible = true,
                Icon = CreateAppIcon()
            };

            var contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open FileOrgy", null, (s, e) => OpenRequested?.Invoke());
            openItem.Font = new System.Drawing.Font(contextMenu.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(openItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            _pauseMenuItem = new ToolStripMenuItem("Pause Monitoring", null, (s, e) => TogglePause());
            contextMenu.Items.Add(_pauseMenuItem);

            var scanItem = new ToolStripMenuItem("Scan Folder Now...", null, (s, e) => ScanRequested?.Invoke());
            contextMenu.Items.Add(scanItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit FileOrgy", null, (s, e) => ExitRequested?.Invoke());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => OpenRequested?.Invoke();
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_orchestrator.ConfigService.CurrentConfig.Settings.ShowDesktopNotifications)
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, icon);
            }
        }

        public void UpdateMonitoringStatus(bool isPaused)
        {
            _pauseMenuItem.Text = isPaused ? "Resume Monitoring" : "Pause Monitoring";
            _notifyIcon.Text = isPaused
                ? "FileOrgy - Monitoring Paused"
                : "FileOrgy - Active Folder Automation";
        }

        private void TogglePause()
        {
            _orchestrator.IsMonitoringPaused = !_orchestrator.IsMonitoringPaused;
            UpdateMonitoringStatus(_orchestrator.IsMonitoringPaused);
        }

        private static Icon CreateAppIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(DColor.Transparent);

            using (var brush = new LinearGradientBrush(
                new Rectangle(2, 6, 28, 22),
                DColor.FromArgb(99, 102, 241),
                DColor.FromArgb(168, 85, 247),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillPolygon(brush, new DPoint[]
                {
                    new DPoint(2, 8),
                    new DPoint(12, 8),
                    new DPoint(15, 12),
                    new DPoint(2, 12)
                });

                g.FillRoundedRectangle(brush, 2, 11, 28, 17, 4);
            }

            using (var boltBrush = new SolidBrush(DColor.FromArgb(254, 240, 138)))
            {
                var boltPoints = new DPoint[]
                {
                    new DPoint(18, 13),
                    new DPoint(12, 19),
                    new DPoint(16, 19),
                    new DPoint(14, 25),
                    new DPoint(20, 18),
                    new DPoint(16, 18)
                };
                g.FillPolygon(boltBrush, boltPoints);
            }

            IntPtr hIcon = bmp.GetHicon();
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }

    internal static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
        {
            using var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}
