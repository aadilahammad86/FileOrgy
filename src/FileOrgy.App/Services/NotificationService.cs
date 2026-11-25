using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfApp = System.Windows.Application;

namespace FileOrgy.App.Services
{
    public enum ToastSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    public sealed class ToastMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public ToastSeverity Severity { get; init; } = ToastSeverity.Info;
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }

    public sealed class ConfirmationRequest
    {
        private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string ConfirmLabel { get; init; } = "Confirm";
        public string CancelLabel { get; init; } = "Cancel";
        public bool IsDestructive { get; init; }

        internal Task<bool> Task => _tcs.Task;

        internal void Complete(bool accepted)
        {
            _tcs.TrySetResult(accepted);
        }
    }

    public sealed class NotificationService : INotifyPropertyChanged
    {
        private readonly Dictionary<Guid, DispatcherTimer> _timers = new();
        private readonly object _lock = new();
        private ConfirmationRequest? _activeConfirmation;

        public ObservableCollection<ToastMessage> Toasts { get; } = new();

        public ConfirmationRequest? ActiveConfirmation
        {
            get => _activeConfirmation;
            private set
            {
                if (!ReferenceEquals(_activeConfirmation, value))
                {
                    _activeConfirmation = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasActiveConfirmation));
                }
            }
        }

        public bool HasActiveConfirmation => ActiveConfirmation != null;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void ShowToast(string title, string message, ToastSeverity severity = ToastSeverity.Info, int durationMs = 4000)
        {
            var toast = new ToastMessage
            {
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Severity = severity
            };

            RunOnUiThread(() =>
            {
                Toasts.Add(toast);
                while (Toasts.Count > 4)
                {
                    var oldest = Toasts[0];
                    DismissToastOnUiThread(oldest.Id);
                }

                if (durationMs > 0)
                {
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(durationMs)
                    };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        DismissToastOnUiThread(toast.Id);
                    };

                    lock (_lock)
                    {
                        _timers[toast.Id] = timer;
                    }
                    timer.Start();
                }
            });
        }

        public void DismissToast(Guid id)
        {
            RunOnUiThread(() => DismissToastOnUiThread(id));
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string confirmLabel = "Confirm",
            string cancelLabel = "Cancel",
            bool isDestructive = false)
        {
            var request = new ConfirmationRequest
            {
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "Confirm" : confirmLabel,
                CancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel,
                IsDestructive = isDestructive
            };

            RunOnUiThread(() =>
            {
                if (_activeConfirmation != null)
                {
                    _activeConfirmation.Complete(false);
                }
                ActiveConfirmation = request;
            });

            return request.Task;
        }

        public void ResolveConfirmation(bool accepted)
        {
            RunOnUiThread(() =>
            {
                var current = ActiveConfirmation;
                if (current == null) return;

                ActiveConfirmation = null;
                current.Complete(accepted);
            });
        }

        private void DismissToastOnUiThread(Guid id)
        {
            lock (_lock)
            {
                if (_timers.TryGetValue(id, out var timer))
                {
                    timer.Stop();
                    _timers.Remove(id);
                }
            }

            var existing = Toasts.FirstOrDefault(t => t.Id == id);
            if (existing != null)
            {
                Toasts.Remove(existing);
            }
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = WpfApp.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
