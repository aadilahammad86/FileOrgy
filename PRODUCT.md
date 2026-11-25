# Product

<!-- impeccable:product-schema 1 -->

## Platform

native-windows-wpf (Inferred from existing .NET 10 WPF desktop application `src/FileOrgy.App/FileOrgy.App.csproj` + Inno Setup 6 Windows installer `FileOrgy.iss`; Impeccable web/mobile platform schemas map closest to native desktop Operate mode.)

## Users

Windows power users, helpdesk/IT operators, and everyday PC users managing high-volume drop zones (such as `Downloads`, `Desktop`, and project intake folders) who want reliable background sorting, document date extraction, renaming, archive unpacking, and safe cleanup without manually triaging files.

## Product Purpose

FileOrgy is a lightweight, real-time Windows folder automation utility that watches local directories via OS filesystem hooks and runs deterministic multi-step rule pipelines (inspect document text/dates, regex capture, smart rename, move/copy, extract archives, safe Recycle Bin cleanup, or shell commands) in the background from the system tray.

Success means:
- Files land in the right folders automatically with zero lost or overwritten data.
- Users can tell at a glance whether background monitoring and Windows startup autostart are actually healthy (including Task Manager visibility vs. elevated Task Scheduler mode).
- Users can safely test rules with a non-destructive dry-run preview before unleashing rules on live directories.

## Positioning

Fast, deterministic, zero-cloud Windows-native file automation with content-aware PDF/DOCX/XLSX text & invoice-date extraction, reusable extension/keyword lists, and transparent step-by-step audit logs with Recycle Bin safety.

## Operating Context

- Runs primarily silently in the Windows System Tray (`--tray` at Windows sign-in) and is opened when configuring rules, inspecting activity history, running manual folder scans (`--scan <path>` from Explorer context menu), or troubleshooting startup/updates.
- Installed to `C:\Program Files\FileOrgy` via Inno Setup (`PrivilegesRequired=admin`), with per-user runtime configuration and JSONL activity logs stored in `%LOCALAPPDATA%\FileOrgy`.
- Supports both standard non-elevated startup (`HKCU\...\Run`, visible in Task Manager → Startup apps) and elevated startup (`FileOrgy_ElevatedAutoStart` Task Scheduler task when `~ RUNASADMIN` is enabled for protected system folders).

## Capabilities and Constraints

- **Core engine (`FileOrgy.Core`, .NET 10)**: `FileSystemMonitorService` (debounced `FileSystemWatcher` + lock detection), `ConditionEvaluator` (10 targets × 15 operators, AND/OR match logic), `WorkflowEngine` (11 step types), `SmartRenamer` (token & case transforms), `DocumentInspector` (PdfPig + OpenXML + text), `ArchiveExtractor` (SharpCompress + zip-slip guard), `WindowsRecycleBin`, `LogManager` (JSONL + CSV/JSON export), `ConfigService`.
- **Previously hidden Core capabilities (to expose in UI)**: `Rule.ScopedFolderIds`, `RuleCondition.TargetVariable` (`CustomVariable`), `WorkflowStep.ContinueOnError`, `CustomExtractPath`, `ArchiveMoveTargetFolder`, `StepType.InspectDocument`, `StepType.RecycleFile`, `StepType.PermanentDelete`.
- **Technical constraints**: WPF on `net10.0-windows` (`UseWPF` + `UseWindowsForms` for tray/dialogs); self-contained single-file publish via `.github/workflows/release.yml` and `FileOrgy.iss`; must preserve backward compatibility with existing `%LOCALAPPDATA%\FileOrgy\config.json` files and existing 49 core tests.
- **Autonomous decision disclosure**: User explicitly directed autonomous execution without waiting for interview responses; inferred UX priorities (inline inspectors, dry-run pipeline simulator, dual Dark/Light token theme, startup diagnostics/repair, emoji-free native vector iconography) directly from the codebase and user requests.

## Brand Commitments

- Name: **FileOrgy**
- Existing icon/logo motif: Folder + lightning bolt (`assets\app.ico`, `TrayService.cs`).
- Palette/visual replacement authorized by explicit user redesign brief ("completely rebuild its entire UI/UX workflow" using Claude design skills).

## Product Principles

1. **Zero Surprise File Operations**: Every rule shows plain-language logic and supports non-destructive dry-run simulation before touching files; destructive steps default to Windows Recycle Bin.
2. **Transparent System Health**: Never fake metrics or hide startup/elevation failures; show real process memory, real watcher counts, and real Windows startup registration state with one-click recovery.
3. **Direct Inline Workflows over Modal Jumps**: Watch folders, rules, keyword tags, and log details inspect and edit smoothly without losing context or spamming OS `MessageBox` dialogs.
4. **Native Windows Precision**: Crisp typography (`Segoe UI Variable` / `Cascadia Mono` with tabular numerals), consistent vector geometry icons (zero emoji chrome), keyboard shortcuts, and seamless Dark/Light theme switching.
