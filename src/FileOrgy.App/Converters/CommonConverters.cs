using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FileOrgy.Core.Models;

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
                    StepType.MoveFile => "📁 Move File to Folder",
                    StepType.CopyFile => "📋 Copy File to Folder",
                    StepType.SmartRename => "✏️ Smart Rename File",
                    StepType.ExtractArchive => "📦 Extract Archive (.zip, .rar, etc.)",
                    StepType.ExtractDate => "📅 Extract Document Date",
                    StepType.RegexCapture => "🔍 Capture Variables via Regex",
                    StepType.CleanupFolder => "🧹 Clean Up Old Files",
                    StepType.RunCommand => "⚙️ Run External Script / Command",
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

    public class BoolToStatusBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(16, 185, 129)); // #10B981
        private static readonly SolidColorBrush AmberBrush = new(System.Windows.Media.Color.FromRgb(245, 158, 11)); // #F59E0B

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? GreenBrush : AmberBrush;
            }
            return AmberBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(16, 185, 129));
        private static readonly SolidColorBrush RedBrush = new(System.Windows.Media.Color.FromRgb(239, 68, 68));
        private static readonly SolidColorBrush YellowBrush = new(System.Windows.Media.Color.FromRgb(245, 158, 11));
        private static readonly SolidColorBrush BlueBrush = new(System.Windows.Media.Color.FromRgb(59, 130, 246));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
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
}
