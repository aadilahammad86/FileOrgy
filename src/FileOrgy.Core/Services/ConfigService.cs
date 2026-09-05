using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileOrgy.Core.Models;

namespace FileOrgy.Core.Services
{
    public class ConfigService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _configFilePath;
        private AppConfiguration? _currentConfig;

        public AppConfiguration CurrentConfig => _currentConfig ??= LoadConfig();

        public ConfigService(string? customPath = null)
        {
            if (string.IsNullOrWhiteSpace(customPath))
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(appData, "FileOrgy");
                Directory.CreateDirectory(folder);
                _configFilePath = Path.Combine(folder, "config.json");
            }
            else
            {
                _configFilePath = customPath;
                string? dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
        }

        public AppConfiguration LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
                    if (config != null)
                    {
                        EnsureDefaultListsAndVariables(config);
                        _currentConfig = config;
                        return config;
                    }
                }
            }
            catch { }

            var defaultConfig = CreateDefaultConfiguration();
            SaveConfig(defaultConfig);
            _currentConfig = defaultConfig;
            return defaultConfig;
        }

        public void SaveConfig(AppConfiguration config)
        {
            try
            {
                _currentConfig = config;
                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        public static AppConfiguration CreateDefaultConfiguration()
        {
            string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string userDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            var config = new AppConfiguration
            {
                Settings = new AppSettings
                {
                    StartWithWindows = false,
                    MinimizeToTrayOnClose = true,
                    ShowDesktopNotifications = true,
                    GlobalDebounceDelayMs = 1200,
                    MaxConcurrentOperations = 2,
                    LogRetentionDays = 30,
                    DarkMode = true
                }
            };

            // Custom Variables
            config.CustomVariables["BaseDocuments"] = userDocs;
            config.CustomVariables["ArchiveBackup"] = Path.Combine(userDocs, "FileOrgy_Backups");
            config.CustomVariables["TaxYear"] = DateTime.Now.Year.ToString();

            // Reusable Keyword Lists
            config.KeywordLists["FinancialKeywords"] = new List<string>
            {
                "invoice", "receipt", "statement", "bill", "tax", "payment", "bank", "due date"
            };

            config.KeywordLists["DocumentExts"] = new List<string>
            {
                "pdf", "docx", "xlsx", "pptx", "txt", "csv", "md"
            };

            config.KeywordLists["ArchiveExts"] = new List<string>
            {
                "zip", "7z", "rar", "tar", "gz"
            };

            config.KeywordLists["MediaExts"] = new List<string>
            {
                "mp4", "mkv", "avi", "mov", "mp3", "flac", "wav"
            };

            // Default Watch Folder (Downloads)
            if (Directory.Exists(userDownloads))
            {
                config.WatchFolders.Add(new WatchFolderConfig
                {
                    Id = "downloads-folder",
                    DisplayName = "Downloads Folder",
                    FolderPath = userDownloads,
                    Enabled = true,
                    IncludeSubdirectories = false,
                    DebounceDelayMs = 1500
                });
            }

            // Default Rule 1: Invoice & Financial Documents Organizer
            var invoiceRule = new Rule
            {
                Id = "rule-invoices",
                Name = "Organize Invoices & Receipts",
                Description = "Detects financial documents, extracts document date, and sorts into Year/Month folder",
                Enabled = true,
                Priority = 1,
                StopOnFirstMatch = true,
                MatchLogic = ConditionMatchLogic.All,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition
                    {
                        Target = RuleTarget.Extension,
                        Operator = ConditionOperator.InKeywordList,
                        KeywordListName = "DocumentExts"
                    },
                    new RuleCondition
                    {
                        Target = RuleTarget.ExtractedContent,
                        Operator = ConditionOperator.InKeywordList,
                        KeywordListName = "FinancialKeywords"
                    }
                },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepType = StepType.ExtractDate,
                        Name = "Extract Document Date"
                    },
                    new WorkflowStep
                    {
                        StepType = StepType.SmartRename,
                        Name = "Standardize Invoice Name",
                        RenamePattern = "{doc_date:yyyy-MM-dd}_{basename}{dotext}",
                        ConflictResolution = ConflictResolution.AutoRenameUnique
                    },
                    new WorkflowStep
                    {
                        StepType = StepType.MoveFile,
                        Name = "Sort by Year and Month",
                        DestinationTemplate = Path.Combine(userDocs, "Invoices", "{doc_date:yyyy}", "{doc_date:MM}"),
                        ConflictResolution = ConflictResolution.AutoRenameUnique
                    }
                }
            };
            config.Rules.Add(invoiceRule);

            // Default Rule 2: Automatic Archive Extraction
            var archiveRule = new Rule
            {
                Id = "rule-archives",
                Name = "Auto-Extract Archives",
                Description = "Extracts compressed archives into a dedicated folder and moves archive to Recycle Bin",
                Enabled = true,
                Priority = 2,
                StopOnFirstMatch = true,
                MatchLogic = ConditionMatchLogic.All,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition
                    {
                        Target = RuleTarget.Extension,
                        Operator = ConditionOperator.InKeywordList,
                        KeywordListName = "ArchiveExts"
                    }
                },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepType = StepType.ExtractArchive,
                        Name = "Extract Archive",
                        ExtractDestination = ArchiveExtractDestination.SubfolderNamedAfterArchive,
                        ArchivePostAction = ArchivePostAction.RecycleArchive,
                        EnqueueExtractedFiles = true
                    }
                }
            };
            config.Rules.Add(archiveRule);

            // Default Rule 3: Photo & Screenshot Renamer
            var photoRule = new Rule
            {
                Id = "rule-photos",
                Name = "Organize Photos & Images",
                Description = "Sorts photos into Pictures by year and month using photo/file dates",
                Enabled = true,
                Priority = 3,
                StopOnFirstMatch = true,
                MatchLogic = ConditionMatchLogic.Any,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "jpg" },
                    new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "jpeg" },
                    new RuleCondition { Target = RuleTarget.Extension, Operator = ConditionOperator.Equals, Value = "png" }
                },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepType = StepType.ExtractDate,
                        Name = "Extract Photo Date"
                    },
                    new WorkflowStep
                    {
                        StepType = StepType.MoveFile,
                        Name = "Move to Photos",
                        DestinationTemplate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Organized", "{doc_date:yyyy}", "{doc_date:MM}"),
                        ConflictResolution = ConflictResolution.AutoRenameUnique
                    }
                }
            };
            config.Rules.Add(photoRule);

            return config;
        }

        private static void EnsureDefaultListsAndVariables(AppConfiguration config)
        {
            config.CustomVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            config.KeywordLists ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            config.WatchFolders ??= new List<WatchFolderConfig>();
            config.Rules ??= new List<Rule>();
            config.Settings ??= new AppSettings();
        }
    }
}
