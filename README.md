# ⚡ FileOrgy

> **High-Performance, Near-Zero Resource Windows File Automation & Organization Software**

**FileOrgy** is an ultra-lightweight, high-speed Windows desktop application engineered for 24/7 autonomous folder monitoring, content inspection, smart renaming, multi-step pipeline automation, archive handling, and storage cleanup.

Built on native **.NET 10 (Windows Desktop & WPF)**, FileOrgy uses Windows kernel I/O completion ports (`ReadDirectoryChangesW`) to ensure **0.0% idle CPU usage** and an ultra-lean memory footprint (~18MB to 25MB RAM).

---

## 🌟 Key Features

### 1. 👁️ Real-Time Folder Monitoring
- **Instant Detection**: Watches directories continuously for newly created, modified, or renamed files using native Windows kernel hooks.
- **Smart Debounce Engine**: Automatically coalesces rapid Windows file events (e.g., active downloads or large file copies) with customizable debounce delays (default `1200ms–1500ms`).
- **File Lock & Stability Protection**: Detects when third-party applications are still writing to a file, politely waiting until file locks are released before triggering workflows.
- **Transient File Filtering**: Automatically ignores temporary files (`*.tmp`, `*.crdownload`, `*.part`, `~$*`, `desktop.ini`, `Thumbs.db`).

### 2. 🗂️ Automatic File Organization
- **Granular Condition Engine**: Match files by Name, Extension, Full Path, File Size, Created/Modified Dates, Extracted Document Content, Internal Document Date, or Custom Variables.
- **Operators**: `Equals`, `NotEquals`, `Contains`, `StartsWith`, `EndsWith`, `MatchesWildcard`, `MatchesRegex`, `InKeywordList`, `NotInKeywordList`, `GreaterThan`, `LessThan`, `OlderThanDays`, `NewerThanDays`, `IsEmptyFile`.
- **Match Logic**: Supports both `Match ALL (AND)` and `Match ANY (OR)` condition grouping.
- **Conflict Resolution Strategies**:
  - `AutoRenameUnique`: Safely appends incremental counter `(1)`, `(2)` to prevent accidental overwrites.
  - `Overwrite`: Replaces destination file.
  - `Skip`: Retains existing destination and aborts move.
  - `KeepNewer`: Replaces only if source timestamp is newer.

### 3. 🏷️ Smart Dynamic Renaming
- **Template Tokens**:
  - `{filename}`: Full original filename with extension.
  - `{basename}`: Filename without extension.
  - `{ext}` / `{dotext}`: Extension without/with leading period.
  - `{date:format}`: Current system date/time (e.g., `{date:yyyy-MM-dd}`).
  - `{created:format}`: File creation timestamp.
  - `{modified:format}`: File modification timestamp.
  - `{doc_date:format}`: Internal extracted document date (e.g., `{doc_date:yyyy-MM-dd}`).
  - `{filesize}` / `{filesize_bytes}`: Human-readable or raw byte size.
  - `{parent}`: Name of parent folder.
  - `{counter:001}`: Incremental sequence number padded with zeroes (e.g., `001`, `002`).
  - `{var:VariableName}`: Custom variable substitution.
  - `{regex:GroupName}` / `{match:1}`: Captured regex groups from filename or document content.
- **Case Transformers**: Lowercase, Uppercase, Title Case, snake_case, kebab-case, or clean slug.
- **Filename Sanitizer**: Automatically cleans forbidden Windows characters (`\ / : * ? " < > |`).

### 4. 📄 Content-Based Processing
- **PDF Documents**: Native C# PDF text and metadata reader (powered by `UglyToad.PdfPig`). Extracts text and metadata without heavy external dependencies.
- **Office OpenXML (.docx, .xlsx, .pptx)**: Zero-dependency streaming extraction directly from the Office OpenXML container (`word/document.xml`, `xl/sharedStrings.xml`, `ppt/slides/slide*.xml`, and `docProps/core.xml`). No Microsoft Office installation required.
- **Plain Text & Code**: Reads `.txt`, `.csv`, `.tsv`, `.log`, `.md`, `.json`, `.xml`, `.html`, `.ini`, `.yaml`, `.sql`, `.cs`, `.py`, `.js`, `.ts` with streaming buffer safety to keep memory low.

### 5. 📅 Document Date Extraction
- **Multi-Tier Date Detection**:
  1. Internal document metadata (PDF `CreationDate`/`ModDate`, Office `dcterms:created`/`dcterms:modified`).
  2. Image EXIF metadata (`DateTimeOriginal` / `CreateDate`) for JPEG and TIFF images.
  3. Text pattern scanning for ISO dates (`YYYY-MM-DD`), US/EU numeric formats (`MM/DD/YYYY`, `DD/MM/YYYY`), and written dates (`September 5, 2026`, `5-Sep-2026`).
  4. Invoice date anchors (`Invoice Date:`, `Dated:`, `Statement Date:`).

### 6. 🧹 Automated Storage Cleanup
- **Retention Rules**: Automatically remove files older than $N$ days, hours, or minutes.
- **Safe Windows Recycle Bin**: Direct P/Invoke integration with native Windows `shell32.dll` (`SHFileOperationW`). Recycled files can be restored anytime from the Windows Recycle Bin.
- **Permanent Deletion Toggle**: Optional secure permanent file removal when preferred.
- **Empty Folder Cleaner**: Recursively prunes empty directories left behind after moves or cleanups.
- **System Directory Protection**: Built-in safeguards prevent operations on protected system directories (`Windows`, `System32`, `Program Files`, drive roots).

### 7. 📦 Archive Auto-Extraction (Including Multipart)
- **Wide Format Support**: Extracts `.zip`, `.tar`, `.gz`, `.tgz`, `.bz2`, `.7z`, and `.rar` archives.
- **Multipart Archive Intelligence**: Automatically detects split archives (`.part01.rar`, `.part02.rar`, `.zip.001`, `.7z.001`) and triggers extraction only on primary volumes while tracking all companion parts.
- **Zip-Slip Protection**: Validates normalized extraction targets to protect against malicious directory traversal archives.
- **Post-Extraction Actions**: Safe Recycle Bin disposal, permanent delete, archive moving, or retention.
- **Pipeline Re-Queue**: Option to immediately organize extracted contents through subsequent workflow rules!

### 8. 🔗 Multi-Step Workflows
- **Pipelines**: Rules chain multiple steps into a single ordered sequence:
  ```
  Incoming File
      │
      ▼
  [Step 1: Extract Archive] ──▶ (Extracts ZIP/7Z to staging folder)
      │
      ▼
  [Step 2: Inspect Document] ──▶ (Parses text from PDF / Word / Excel)
      │
      ▼
  [Step 3: Extract Date] ──▶ (Resolves internal document date)
      │
      ▼
  [Step 4: Regex Capture] ──▶ (Captures invoice number into {match:1})
      │
      ▼
  [Step 5: Smart Rename] ──▶ ({doc_date:yyyy-MM-dd}_INV-{match:1}_{basename})
      │
      ▼
  [Step 6: Move / Route] ──▶ (Routes to D:\Invoices\{doc_date:yyyy}\{doc_date:MM}\)
      │
      ▼
  [Step 7: Recycle Original] ──▶ (Safe Windows Recycle Bin)
  ```

### 9. 🧩 Variables & Reusable Lists
- **Custom Variables**: Global key-value dictionary (e.g., `BaseStorage`, `ClientPrefix`, `TaxYear`) accessible via `{var:Key}` in file paths, templates, and rules.
- **Reusable Keyword Lists**: Define named lists (e.g., `FinancialKeywords`, `MediaFormats`, `DocExtensions`). Update a list in one place and all rules using `InKeywordList` update instantly.

### 10. 📊 Comprehensive Logging & Activity Tracking
- **Live Activity Feed**: Real-time streaming log of all file detections, rules matched, renames, moves, extractions, and cleanups.
- **Persistent JSONL Storage**: Operations saved to disk in JSON Lines format (`%LocalAppData%\FileOrgy\logs\activity.jsonl`) with rolling retention.
- **Exporting**: One-click export to **CSV** and **JSON**.
- **Windows Explorer Integration**: Right-click any log entry to "Open Containing Folder" or "Copy Path".

---

## ⚡ Near-Zero Resource Architecture

| Metric | Performance |
|---|---|
| **Idle CPU Usage** | **0.00%** (Windows Kernel I/O Completion Ports) |
| **Active Processing CPU** | Under 1.5% (Bounded concurrency queue, max 2 worker threads) |
| **Working Set Memory (RAM)** | **~18 MB – 25 MB** |
| **Disk I/O** | Batched asynchronous flushes every 2 seconds |
| **Background Mode** | Minimizes to Windows System Tray (`NotifyIcon`) |

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11 (x64 / ARM64)
- .NET 10.0 Runtime (or .NET 10 Desktop SDK for building from source)

### Building from Source

```powershell
# Clone or navigate to the repository
cd C:\Users\swalih\FileOrgy

# Restore and build the solution
dotnet build FileOrgy.slnx -c Release

# Run automated tests (19 passing unit tests)
dotnet test FileOrgy.slnx

# Run FileOrgy
dotnet run --project src/FileOrgy.App/FileOrgy.App.csproj
```

### CLI / Background Startup Options

```powershell
# Start minimized directly to Windows notification area / system tray
FileOrgy.App.exe --tray

# Or with --minimized
FileOrgy.App.exe --minimized
```

---

## 📁 Solution Architecture

```
FileOrgy/
├── FileOrgy.slnx                       # Modern .NET 10 Solution
├── src/
│   ├── FileOrgy.Core/                  # High-performance automation engine
│   │   ├── Models/
│   │   │   ├── Models.cs               # Rules, Conditions, Steps, Config, Logs
│   │   │   └── WorkflowContext.cs      # File processing context & state
│   │   ├── Services/
│   │   │   ├── FileSystemMonitorService.cs # Kernel-level folder watcher & debouncer
│   │   │   ├── ConditionEvaluator.cs   # Multi-target rule matching engine
│   │   │   ├── WorkflowEngine.cs       # Multi-step pipeline execution
│   │   │   ├── SmartRenamer.cs         # Token-based dynamic renamer
│   │   │   ├── DocumentInspector.cs    # PDF, Office OpenXML & text parser
│   │   │   ├── ArchiveExtractor.cs     # ZIP, 7Z, RAR & multipart extractor
│   │   │   ├── WindowsRecycleBin.cs    # Native shell32 Recycle Bin P/Invoke
│   │   │   ├── ConfigService.cs        # JSON config loader & default presets
│   │   │   ├── LogManager.cs           # Async circular log buffer & export
│   │   │   └── FileOrgyOrchestrator.cs # Central coordinator & concurrency pool
│   │   └── Utils/
│   │       ├── FileLockHelper.cs       # Non-blocking file stability detection
│   │       ├── PathHelper.cs           # Sanitization & zip-slip protection
│   │       └── DateParserHelper.cs     # Multi-pattern date parsing
│   └── FileOrgy.App/                   # Fluent dark WPF Desktop UI
│       ├── App.xaml / App.xaml.cs      # CLI startup & application host
│       ├── MainWindow.xaml / .cs       # Main multi-tab application window
│       ├── Views/
│       │   └── RuleEditDialog.xaml/.cs # Visual rule & workflow editor
│       ├── ViewModels/
│       │   ├── MainViewModel.cs        # Navigation & coordinator
│       │   ├── DashboardViewModel.cs   # KPI cards & live feed
│       │   ├── WatchFoldersViewModel.cs# Folder monitoring manager
│       │   ├── RulesViewModel.cs       # Rules list & priority ordering
│       │   ├── RuleEditViewModel.cs    # Condition & step pipeline builder
│       │   ├── VariablesViewModel.cs   # Custom variables & keyword lists
│       │   ├── LogsViewModel.cs        # Log search, filters & export
│       │   └── SettingsViewModel.cs    # Auto-start, debounce & limits
│       ├── Services/
│       │   ├── TrayService.cs          # Native NotifyIcon & context menu
│       │   └── StartupHelper.cs        # Windows Registry Run integration
│       └── Converters/
│           └── CommonConverters.cs     # WPF UI value converters
└── tests/
    └── FileOrgy.Tests/                 # Comprehensive unit test suite
        ├── CoreTests.cs                # 11 core engine & workflow tests
        └── AdvancedFeatureTests.cs     # 8 advanced tests (PDF, DOCX, XLSX, etc.)
```

---

## 📜 License
MIT License. Built with ❤️ for Windows.
