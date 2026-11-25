using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FileOrgy.App.Services;
using FileOrgy.Core.Models;
using WpfApp = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;

namespace FileOrgy.App.Converters
{
    public class EnumFriendlyNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            return value switch
            {
                RuleTarget target => target switch
                {
                    RuleTarget.Extension => "File Extension (e.g. pdf, zip)",
                    RuleTarget.FileName => "File Name with Extension (e.g. invoice.pdf)",
                    RuleTarget.BaseName => "File Name without Extension (e.g. invoice)",
                    RuleTarget.FullPath => "Full File Path",
                    RuleTarget.FileSizeBytes => "File Size (in bytes)",
                    RuleTarget.FileCreatedDate => "File Creation Date",
                    RuleTarget.FileModifiedDate => "File Modification Date",
                    RuleTarget.ExtractedContent => "Document Text Content",
                    RuleTarget.ExtractedDate => "Extracted Invoice / Document Date",
                    RuleTarget.CustomVariable => "Custom Variable Value",
                    _ => target.ToString()
                },
                ConditionOperator op => op switch
                {
                    ConditionOperator.InKeywordList => "Matches Keyword List",
                    ConditionOperator.NotInKeywordList => "Does NOT Match Keyword List",
                    ConditionOperator.Equals => "Equals (exact match)",
                    ConditionOperator.NotEquals => "Does Not Equal",
                    ConditionOperator.Contains => "Contains Text",
                    ConditionOperator.NotContains => "Does Not Contain Text",
                    ConditionOperator.StartsWith => "Starts With",
                    ConditionOperator.EndsWith => "Ends With",
                    ConditionOperator.MatchesWildcard => "Matches Wildcard (*.txt, img_??.*)",
                    ConditionOperator.MatchesRegex => "Matches Regular Expression",
                    ConditionOperator.GreaterThan => "Greater Than (>)",
                    ConditionOperator.LessThan => "Less Than (<)",
                    ConditionOperator.OlderThanDays => "Older Than (Days)",
                    ConditionOperator.NewerThanDays => "Newer Than (Days)",
                    ConditionOperator.IsEmptyFile => "Is Empty (0 bytes)",
                    _ => op.ToString()
                },
                StepType step => step switch
                {
                    StepType.InspectDocument => "Inspect Document Content",
                    StepType.ExtractDate => "Extract Document Date",
                    StepType.RegexCapture => "Capture Variables via Regex",
                    StepType.SmartRename => "Smart Rename File",
                    StepType.MoveFile => "Move File to Folder",
                    StepType.CopyFile => "Copy File to Folder",
                    StepType.ExtractArchive => "Extract Archive (.zip, .rar, .7z)",
                    StepType.RecycleFile => "Send File to Recycle Bin",
                    StepType.PermanentDelete => "Permanently Delete File",
                    StepType.CleanupFolder => "Clean Up Old Files",
                    StepType.RunCommand => "Run External Script / Command",
                    _ => step.ToString()
                },
                ConflictResolution res => res switch
                {
                    ConflictResolution.AutoRenameUnique => "Auto-rename unique (e.g. file (1).pdf)",
                    ConflictResolution.Overwrite => "Overwrite existing destination file",
                    ConflictResolution.Skip => "Skip / Do nothing",
                    ConflictResolution.KeepNewer => "Keep newer file only",
                    _ => res.ToString()
                },
                CaseTransform ct => ct switch
                {
                    CaseTransform.None => "Keep original case",
                    CaseTransform.LowerCase => "lowercase (all small letters)",
                    CaseTransform.UpperCase => "UPPERCASE (ALL CAPITAL LETTERS)",
                    CaseTransform.TitleCase => "Title Case (Capitalize Each Word)",
                    CaseTransform.SnakeCase => "snake_case (underscore separated)",
                    CaseTransform.KebabCase => "kebab-case (hyphen separated)",
                    _ => ct.ToString()
                },
                ArchiveExtractDestination dest => dest switch
                {
                    ArchiveExtractDestination.SubfolderNamedAfterArchive => "Subfolder named after archive",
                    ArchiveExtractDestination.SameFolder => "In the same folder as archive",
                    ArchiveExtractDestination.CustomFolder => "Custom output folder path",
                    _ => dest.ToString()
                },
                ArchivePostAction act => act switch
                {
                    ArchivePostAction.KeepArchive => "Keep original archive intact",
                    ArchivePostAction.RecycleArchive => "Send original archive to Recycle Bin",
                    ArchivePostAction.DeleteArchivePermanently => "Permanently delete archive file",
                    ArchivePostAction.MoveArchiveToFolder => "Move original archive to folder",
                    _ => act.ToString()
                },
                StartupMode mode => mode switch
                {
                    StartupMode.Disabled => "Disabled",
                    StartupMode.RegistryCurrentUser => "Standard User Startup (HKCU Run)",
                    StartupMode.TaskSchedulerElevated => "Elevated Task Scheduler",
                    StartupMode.BlockedOrForeignTask => "Blocked or Foreign Task",
                    _ => mode.ToString()
                },
                _ => value.ToString() ?? string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool flag && flag;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool b = v == Visibility.Visible;
                return Invert ? !b : b;
            }
            return false;
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNotNull = value != null;
            if (Invert) isNotNull = !isNotNull;
            return isNotNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasText = value is string s && !string.IsNullOrWhiteSpace(s);
            if (Invert) hasText = !hasText;
            return hasText ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public bool ShowWhenZero { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = 0;
            if (value is int i) count = i;
            else if (value is long l) count = (int)Math.Min(int.MaxValue, l);

            bool visible = ShowWhenZero ? count == 0 : count > 0;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new(WpfColor.FromRgb(34, 197, 94));
        private static readonly SolidColorBrush AmberBrush = new(WpfColor.FromRgb(245, 158, 11));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return WpfApp.Current?.TryFindResource("Brush.Success") as WpfBrush ?? GreenBrush;
            }
            return WpfApp.Current?.TryFindResource("Brush.Warning") as WpfBrush ?? AmberBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StepTypeToIconKeyConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not StepType step) return null;

            string key = step switch
            {
                StepType.InspectDocument => "Icon.FileText",
                StepType.ExtractDate => "Icon.Calendar",
                StepType.RegexCapture => "Icon.Search",
                StepType.SmartRename => "Icon.Edit",
                StepType.MoveFile => "Icon.Folder",
                StepType.CopyFile => "Icon.Copy",
                StepType.ExtractArchive => "Icon.Archive",
                StepType.RecycleFile => "Icon.Trash",
                StepType.PermanentDelete => "Icon.Error",
                StepType.CleanupFolder => "Icon.Filter",
                StepType.RunCommand => "Icon.Terminal",
                _ => "Icon.Workflow"
            };

            return WpfApp.Current?.TryFindResource(key) as Geometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new(WpfColor.FromRgb(34, 197, 94));
        private static readonly SolidColorBrush RedBrush = new(WpfColor.FromRgb(239, 68, 68));
        private static readonly SolidColorBrush YellowBrush = new(WpfColor.FromRgb(245, 158, 11));
        private static readonly SolidColorBrush BlueBrush = new(WpfColor.FromRgb(56, 189, 248));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
            string key = s switch
            {
                "Success" => "Brush.Success",
                "Error" => "Brush.Danger",
                "Warning" => "Brush.Warning",
                _ => "Brush.Info"
            };

            if (WpfApp.Current?.TryFindResource(key) is WpfBrush themedBrush)
            {
                return themedBrush;
            }

            return s switch
            {
                "Success" => GreenBrush,
                "Error" => RedBrush,
                "Warning" => YellowBrush,
                _ => BlueBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToBackgroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessSubtle = new(WpfColor.FromRgb(18, 43, 30));
        private static readonly SolidColorBrush DangerSubtle = new(WpfColor.FromRgb(46, 22, 25));
        private static readonly SolidColorBrush WarningSubtle = new(WpfColor.FromRgb(45, 34, 17));
        private static readonly SolidColorBrush InfoSubtle = new(WpfColor.FromRgb(19, 38, 56));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
            string key = s switch
            {
                "Success" => "Brush.SuccessSubtle",
                "Error" => "Brush.DangerSubtle",
                "Warning" => "Brush.WarningSubtle",
                _ => "Brush.InfoSubtle"
            };

            if (WpfApp.Current?.TryFindResource(key) is WpfBrush themedBrush)
            {
                return themedBrush;
            }

            return s switch
            {
                "Success" => SuccessSubtle,
                "Error" => DangerSubtle,
                "Warning" => WarningSubtle,
                _ => InfoSubtle
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToForegroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessText = new(WpfColor.FromRgb(74, 222, 128));
        private static readonly SolidColorBrush DangerText = new(WpfColor.FromRgb(248, 113, 113));
        private static readonly SolidColorBrush WarningText = new(WpfColor.FromRgb(251, 191, 36));
        private static readonly SolidColorBrush InfoText = new(WpfColor.FromRgb(125, 211, 252));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
            string key = s switch
            {
                "Success" => "Brush.SuccessText",
                "Error" => "Brush.DangerText",
                "Warning" => "Brush.WarningText",
                _ => "Brush.InfoText"
            };

            if (WpfApp.Current?.TryFindResource(key) is WpfBrush themedBrush)
            {
                return themedBrush;
            }

            return s switch
            {
                "Success" => SuccessText,
                "Error" => DangerText,
                "Warning" => WarningText,
                _ => InfoText
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ToastSeverityToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessBrush = new(WpfColor.FromRgb(34, 197, 94));
        private static readonly SolidColorBrush WarningBrush = new(WpfColor.FromRgb(245, 158, 11));
        private static readonly SolidColorBrush DangerBrush = new(WpfColor.FromRgb(239, 68, 68));
        private static readonly SolidColorBrush InfoBrush = new(WpfColor.FromRgb(56, 189, 248));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToastSeverity severity)
            {
                string key = severity switch
                {
                    ToastSeverity.Success => "Brush.Success",
                    ToastSeverity.Warning => "Brush.Warning",
                    ToastSeverity.Error => "Brush.Danger",
                    _ => "Brush.Info"
                };

                if (WpfApp.Current?.TryFindResource(key) is WpfBrush themedBrush)
                {
                    return themedBrush;
                }

                return severity switch
                {
                    ToastSeverity.Success => SuccessBrush,
                    ToastSeverity.Warning => WarningBrush,
                    ToastSeverity.Error => DangerBrush,
                    _ => InfoBrush
                };
            }

            return WpfApp.Current?.TryFindResource("Brush.Info") as WpfBrush ?? InfoBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
