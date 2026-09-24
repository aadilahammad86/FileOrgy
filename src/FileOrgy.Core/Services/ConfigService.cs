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
                    ScanOnStartup = true,
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
                        EnqueueExtractedFiles = false
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

            EnsureInPlaceKeywordLists(config);

            return config;
        }

        public static void EnsureDefaultListsAndVariables(AppConfiguration config)
        {
            config.CustomVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            config.KeywordLists ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            config.WatchFolders ??= new List<WatchFolderConfig>();
            config.Rules ??= new List<Rule>();
            config.Settings ??= new AppSettings();

            EnsureInPlaceKeywordLists(config);

            // Auto-upgrade existing configs that have in-place rules but miss the "Others" catch-all rule
            if (config.Rules.Any(r => r.Id.StartsWith("rule-inplace-", StringComparison.OrdinalIgnoreCase)) &&
                !config.Rules.Any(r => r.Id == "rule-inplace-others"))
            {
                var othersPreset = GetInPlaceOthersPreset();
                int maxPri = config.Rules.Count > 0 ? config.Rules.Max(r => r.Priority) : 10;
                othersPreset.Priority = maxPri + 1;
                config.Rules.Add(othersPreset);
            }
        }

        private static void MergeExtensions(AppConfiguration config, string key, IEnumerable<string> extensions)
        {
            if (!config.KeywordLists.TryGetValue(key, out var list))
            {
                config.KeywordLists[key] = extensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            else
            {
                foreach (var ext in extensions)
                {
                    if (!list.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(ext);
                    }
                }
            }
        }

        public static void EnsureInPlaceKeywordLists(AppConfiguration config)
        {
            config.KeywordLists ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // 1. Programs & Installers
            MergeExtensions(config, "ProgramExts", new[]
            {
                "exe", "msi", "msix", "appx", "appxbundle", "msixbundle", "msp", "com", "scr", "cpl", "msc", "gadget",
                "bat", "cmd", "ps1", "psm1", "vbs", "vbe", "js", "jse", "wsf", "wsh", "reg",
                "appimage", "apk", "xapk", "apks", "ipa", "run", "sh", "bash", "command", "jar"
            });

            // 2. Documents & Tables
            MergeExtensions(config, "DocumentExts", new[]
            {
                "pdf", "xps", "oxps",
                "doc", "docx", "docm", "dot", "dotx", "dotm", "odt", "ott", "rtf", "wps", "wpd", "pages",
                "xls", "xlsx", "xlsm", "xlsb", "xlt", "xltx", "xltm", "ods", "ots", "csv", "tsv", "dif", "numbers",
                "ppt", "pptx", "pptm", "pot", "potx", "potm", "pps", "ppsx", "ppsm", "odp", "otp", "key",
                "txt", "md", "markdown", "rst", "tex", "latex", "log", "nfo", "text",
                "xml", "json", "jsonc", "json5", "yaml", "yml", "toml", "ini", "cfg", "conf", "properties", "env", "sql",
                "html", "htm", "xhtml", "mhtml", "mht",
                "epub", "mobi", "azw", "azw3", "djvu", "fb2", "cbr", "cbz"
            });

            // 3. Compressed Archives & Disk Images
            MergeExtensions(config, "ArchiveExts", new[]
            {
                "zip", "rar", "7z", "tar", "gz", "gzip", "bz2", "bzip2", "tgz", "tbz", "tbz2", "txz", "tlz", "tzst",
                "xz", "zst", "zstd", "lz", "lzma", "lz4", "lha", "lzh", "z", "ace", "uue", "squashfs",
                "iso", "img", "vhd", "vhdx", "vmdk", "qcow2", "wim", "swm", "esd",
                "cab", "deb", "rpm", "pkg", "dmg", "cpio", "ar", "shar"
            });

            // 4. Pictures & Graphics
            MergeExtensions(config, "PictureExts", new[]
            {
                "jpg", "jpeg", "jpe", "jfif", "png", "gif", "bmp", "dib", "webp", "avif",
                "tif", "tiff", "heic", "heif", "hif", "raw", "cr2", "cr3", "nef", "arw", "orf", "rw2", "pef", "dng", "raf",
                "svg", "svgz", "ico", "icon", "cur",
                "psd", "psb", "ai", "eps", "indd", "cdr", "xcf", "sketch", "fig", "dwg", "dxf"
            });

            // 5. Videos & Movies
            MergeExtensions(config, "VideoExts", new[]
            {
                "mp4", "m4v", "mkv", "avi", "mov", "wmv", "flv", "f4v", "webm",
                "mpg", "mpeg", "mpe", "mpv", "m2v", "ts", "mts", "m2ts", "vob",
                "3gp", "3g2", "ogv", "rm", "rmvb", "asf", "divx", "xvid", "h264", "h265", "hevc"
            });

            // 6. Audio & Music
            MergeExtensions(config, "AudioExts", new[]
            {
                "mp3", "wav", "flac", "aac", "ogg", "oga", "m4a", "wma",
                "alac", "ape", "opus", "aiff", "aif", "aifc", "dsd", "dsf", "dff",
                "mid", "midi", "ac3", "eac3", "dts", "dtshd", "mka", "ra", "amr",
                "m3u", "m3u8", "pls"
            });

            // Dynamically combine all categorized extensions across all category lists AND any active rule conditions
            var allSorted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categoryKeys = new[] { "ProgramExts", "DocumentExts", "ArchiveExts", "PictureExts", "VideoExts", "AudioExts" };
            foreach (var key in categoryKeys)
            {
                if (config.KeywordLists.TryGetValue(key, out var list))
                {
                    foreach (var ext in list)
                    {
                        if (!string.IsNullOrWhiteSpace(ext))
                            allSorted.Add(ext.Trim().TrimStart('.'));
                    }
                }
            }

            // Also inspect any other active rules that specify extension filters
            if (config.Rules != null)
            {
                foreach (var rule in config.Rules.Where(r => r.Enabled && r.Id != "rule-inplace-others"))
                {
                    if (rule.Conditions == null) continue;
                    foreach (var cond in rule.Conditions.Where(c => c.Target == RuleTarget.Extension))
                    {
                        if (cond.Operator == ConditionOperator.Equals || cond.Operator == ConditionOperator.MatchesWildcard)
                        {
                            if (!string.IsNullOrWhiteSpace(cond.Value))
                                allSorted.Add(cond.Value.Trim().TrimStart('.').TrimStart('*').TrimStart('.'));
                        }
                        else if (cond.Operator == ConditionOperator.InKeywordList)
                        {
                            string listName = !string.IsNullOrEmpty(cond.KeywordListName) ? cond.KeywordListName : cond.Value;
                            if (!string.IsNullOrEmpty(listName) && config.KeywordLists.TryGetValue(listName, out var customList))
                            {
                                foreach (var ext in customList)
                                {
                                    if (!string.IsNullOrWhiteSpace(ext))
                                        allSorted.Add(ext.Trim().TrimStart('.'));
                                }
                            }
                        }
                    }
                }
            }

            config.KeywordLists["AllSortedExts"] = allSorted.OrderBy(x => x).ToList();
        }

        public static List<Rule> GetInPlaceOrganizationPresets()
        {
            return new List<Rule>
            {
                new Rule
                {
                    Id = "rule-inplace-programs",
                    Name = "In-Place: Programs & Installers",
                    Description = "Organizes software installers and executables into a Programs folder directly in the monitored directory",
                    Enabled = true,
                    Priority = 10,
                    StopOnFirstMatch = true,
                    MatchLogic = ConditionMatchLogic.All,
                    Conditions = new List<RuleCondition>
                    {
                        new RuleCondition
                        {
                            Target = RuleTarget.Extension,
                            Operator = ConditionOperator.InKeywordList,
                            KeywordListName = "ProgramExts"
                        }
                    },
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            StepType = StepType.MoveFile,
                            Name = "Move to In-Place Programs Folder",
                            DestinationTemplate = @"{directory}\Programs",
                            ConflictResolution = ConflictResolution.AutoRenameUnique
                        }
                    }
                },
                new Rule
                {
                    Id = "rule-inplace-documents",
                    Name = "In-Place: Documents & Tables",
                    Description = "Organizes PDFs, spreadsheets, Word docs, and text into a Documents folder directly in the monitored directory",
                    Enabled = true,
                    Priority = 11,
                    StopOnFirstMatch = true,
                    MatchLogic = ConditionMatchLogic.All,
                    Conditions = new List<RuleCondition>
                    {
                        new RuleCondition
                        {
                            Target = RuleTarget.Extension,
                            Operator = ConditionOperator.InKeywordList,
                            KeywordListName = "DocumentExts"
                        }
                    },
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            StepType = StepType.MoveFile,
                            Name = "Move to In-Place Documents Folder",
                            DestinationTemplate = @"{directory}\Documents",
                            ConflictResolution = ConflictResolution.AutoRenameUnique
                        }
                    }
                },
                new Rule
                {
                    Id = "rule-inplace-compressed",
                    Name = "In-Place: Compressed Archives",
                    Description = "Organizes ZIP, RAR, 7Z, and tar archives into a Compressed folder directly in the monitored directory",
                    Enabled = true,
                    Priority = 12,
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
                            StepType = StepType.MoveFile,
                            Name = "Move to In-Place Compressed Folder",
                            DestinationTemplate = @"{directory}\Compressed",
                            ConflictResolution = ConflictResolution.AutoRenameUnique
                        }
                    }
                },
                new Rule
                {
                    Id = "rule-inplace-pictures",
                    Name = "In-Place: Pictures & Graphics",
                    Description = "Organizes images and photos into a Pictures folder directly in the monitored directory",
                    Enabled = true,
                    Priority = 13,
                    StopOnFirstMatch = true,
                    MatchLogic = ConditionMatchLogic.All,
                    Conditions = new List<RuleCondition>
                    {
                        new RuleCondition
                        {
                            Target = RuleTarget.Extension,
                            Operator = ConditionOperator.InKeywordList,
                            KeywordListName = "PictureExts"
                        }
                    },
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            StepType = StepType.MoveFile,
                            Name = "Move to In-Place Pictures Folder",
                            DestinationTemplate = @"{directory}\Pictures",
                            ConflictResolution = ConflictResolution.AutoRenameUnique
                        }
                    }
                },
                new Rule
                {
                    Id = "rule-inplace-videos",
                    Name = "In-Place: Videos & Movies",
                    Description = "Organizes video clips and recordings into a Videos folder directly in the monitored directory",
                    Enabled = true,
                    Priority = 14,
                    StopOnFirstMatch = true,
                    MatchLogic = ConditionMatchLogic.All,
                    Conditions = new List<RuleCondition>
                    {
                        new RuleCondition
                        {
                            Target = RuleTarget.Extension,
                            Operator = ConditionOperator.InKeywordList,
                            KeywordListName = "VideoExts"
                        }
                    },
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            StepType = StepType.MoveFile,
                            Name = "Move to In-Place Videos Folder",
                            DestinationTemplate = @"{directory}\Videos",
                            ConflictResolution = ConflictResolution.AutoRenameUnique
                        }
                    }
                },
                new Rule
                {
                    Id = "rule-inplace-audio",
                    Name = "In-Place: Audio & Music",
                    Description = "Organizes songs, sound recordings, and music into an Audio folder directly in the monitored directory",
                    Enabled = true,
                    Priority = 15,
                    StopOnFirstMatch = true,
                    MatchLogic = ConditionMatchLogic.All,
                    Conditions = new List<RuleCondition>
                    {
                        new RuleCondition
                        {
                            Target = RuleTarget.Extension,
                            Operator = ConditionOperator.InKeywordList,
                            KeywordListName = "AudioExts"
                        }
                    },
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            StepType = StepType.MoveFile,
                            Name = "Move to In-Place Audio Folder",
                            DestinationTemplate = @"{directory}\Audio",
                            ConflictResolution = ConflictResolution.AutoRenameUnique
                        }
                    }
                },
                GetInPlaceOthersPreset()
            };
        }

        public static Rule GetInPlaceOthersPreset()
        {
            return new Rule
            {
                Id = "rule-inplace-others",
                Name = "In-Place: Other Files",
                Description = "Organizes any remaining unclassified files whose file type does not match any current sorting filter into an Others folder",
                Enabled = true,
                Priority = 16,
                StopOnFirstMatch = true,
                MatchLogic = ConditionMatchLogic.All,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition
                    {
                        Target = RuleTarget.Extension,
                        Operator = ConditionOperator.NotInKeywordList,
                        KeywordListName = "AllSortedExts"
                    }
                },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepType = StepType.MoveFile,
                        Name = "Move to In-Place Others Folder",
                        DestinationTemplate = @"{directory}\Others",
                        ConflictResolution = ConflictResolution.AutoRenameUnique
                    }
                }
            };
        }

        public void LoadInPlaceOrganizationPresets(bool overwriteExisting = false)
        {
            EnsureInPlaceKeywordLists(CurrentConfig);
            var presets = GetInPlaceOrganizationPresets();

            if (overwriteExisting)
            {
                CurrentConfig.Rules.RemoveAll(r => presets.Any(p => p.Id == r.Id) ||
                                                  r.Id.StartsWith("rule-inplace-", StringComparison.OrdinalIgnoreCase) ||
                                                  r.Id.StartsWith("preset-inplace-", StringComparison.OrdinalIgnoreCase));
            }

            int nextPriority = CurrentConfig.Rules.Count > 0 ? CurrentConfig.Rules.Max(r => r.Priority) + 1 : 1;
            foreach (var preset in presets)
            {
                var existing = CurrentConfig.Rules.FirstOrDefault(r => r.Id == preset.Id);
                if (existing == null)
                {
                    preset.Priority = nextPriority++;
                    CurrentConfig.Rules.Add(preset);
                }
            }

            // Cleanly reindex priorities
            for (int i = 0; i < CurrentConfig.Rules.Count; i++)
            {
                CurrentConfig.Rules[i].Priority = i + 1;
            }

            SaveConfig(CurrentConfig);
        }

        public static List<Rule> GetInPlacePresetRules() => GetInPlaceOrganizationPresets();
    }
}

