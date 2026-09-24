using System;
using System.Collections.Generic;

namespace FileOrgy.Core.Models
{
    public enum RuleTarget
    {
        FileName,
        BaseName,
        Extension,
        FullPath,
        FileSizeBytes,
        FileCreatedDate,
        FileModifiedDate,
        ExtractedContent,
        ExtractedDate,
        CustomVariable
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        MatchesWildcard,
        MatchesRegex,
        InKeywordList,
        NotInKeywordList,
        GreaterThan,
        LessThan,
        OlderThanDays,
        NewerThanDays,
        IsEmptyFile
    }

    public enum ConditionMatchLogic
    {
        All, // AND
        Any  // OR
    }

    public class RuleCondition
    {
        public RuleTarget Target { get; set; } = RuleTarget.Extension;
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;
        public string Value { get; set; } = string.Empty;
        public string? TargetVariable { get; set; }
        public string? KeywordListName { get; set; }
        public bool CaseSensitive { get; set; } = false;
    }

    public enum ConflictResolution
    {
        AutoRenameUnique, // e.g. file (1).pdf
        Overwrite,
        Skip,
        KeepNewer
    }

    public enum CaseTransform
    {
        None,
        LowerCase,
        UpperCase,
        TitleCase,
        SnakeCase,
        KebabCase
    }

    public enum StepType
    {
        ExtractArchive,
        InspectDocument,
        ExtractDate,
        RegexCapture,
        SmartRename,
        MoveFile,
        CopyFile,
        RecycleFile,
        PermanentDelete,
        CleanupFolder,
        RunCommand
    }

    public enum ArchivePostAction
    {
        KeepArchive,
        RecycleArchive,
        DeleteArchivePermanently,
        MoveArchiveToFolder
    }

    public enum ArchiveExtractDestination
    {
        SameFolder,
        SubfolderNamedAfterArchive,
        CustomFolder
    }

    public class WorkflowStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public StepType StepType { get; set; } = StepType.SmartRename;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool ContinueOnError { get; set; } = false;

        // Smart Rename / Move / Copy
        public string DestinationTemplate { get; set; } = string.Empty;
        public string RenamePattern { get; set; } = "{basename}{dotext}";
        public CaseTransform CaseTransform { get; set; } = CaseTransform.None;
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.AutoRenameUnique;

        // Archive Extraction
        public ArchiveExtractDestination ExtractDestination { get; set; } = ArchiveExtractDestination.SubfolderNamedAfterArchive;
        public string CustomExtractPath { get; set; } = string.Empty;
        public ArchivePostAction ArchivePostAction { get; set; } = ArchivePostAction.RecycleArchive;
        public string ArchiveMoveTargetFolder { get; set; } = string.Empty;
        public bool EnqueueExtractedFiles { get; set; } = true;

        // Regex Capture
        public string RegexPattern { get; set; } = string.Empty;
        public string RegexSource { get; set; } = "Content"; // "Content", "FileName", "FullPath"

        // Cleanup
        public int OlderThanDays { get; set; } = 30;
        public bool RemoveEmptyFolders { get; set; } = true;
        public bool SafeRecycle { get; set; } = true;

        // External Command
        public string CommandExecutable { get; set; } = string.Empty;
        public string CommandArguments { get; set; } = string.Empty;
    }

    public class Rule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New Rule";
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 0;
        public bool StopOnFirstMatch { get; set; } = true;

        // Specific watch folders (empty = applies to all watch folders)
        public List<string> ScopedFolderIds { get; set; } = new();

        public ConditionMatchLogic MatchLogic { get; set; } = ConditionMatchLogic.All;
        public List<RuleCondition> Conditions { get; set; } = new();
        public List<WorkflowStep> Steps { get; set; } = new();
    }

    public class WatchFolderConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FolderPath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool IncludeSubdirectories { get; set; } = false;
        public int DebounceDelayMs { get; set; } = 1500;
        public List<string> IgnorePatterns { get; set; } = new()
        {
            "*.tmp", "*.crdownload", "*.part", "~$*", "desktop.ini", "Thumbs.db", "*.lock"
        };
        public List<string> SpecificRuleIds { get; set; } = new(); // empty = all global rules
    }

    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public enum LogEventType
    {
        FileDetected,
        RuleMatched,
        FileMoved,
        FileCopied,
        FileRenamed,
        ArchiveExtracted,
        FileRecycled,
        FileDeleted,
        CleanupExecuted,
        CommandExecuted,
        ErrorOccurred,
        SystemInfo
    }

    public class LogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public LogEventType EventType { get; set; } = LogEventType.SystemInfo;
        public string RuleName { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = true;
        public long DurationMs { get; set; }
    }

    public class AppSettings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool ShowDesktopNotifications { get; set; } = true;
        public bool ScanOnStartup { get; set; } = true;
        public int GlobalDebounceDelayMs { get; set; } = 1200;
        public int MaxConcurrentOperations { get; set; } = 2;
        public int LogRetentionDays { get; set; } = 30;
        public bool DarkMode { get; set; } = true;
        public bool AutoCheckForUpdates { get; set; } = true;
    }

    public class AppConfiguration
    {
        public AppSettings Settings { get; set; } = new();
        public List<WatchFolderConfig> WatchFolders { get; set; } = new();
        public List<Rule> Rules { get; set; } = new();
        public Dictionary<string, string> CustomVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> KeywordLists { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
