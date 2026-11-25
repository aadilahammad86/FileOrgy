# FileOrgy Startup Fix & Complete UI/UX Workflow Rebuild Plan

> **For agentic workers:** Follow `executing-plans` / `subagent-driven-development`. Implement tasks cleanly, verify `dotnet build` and `dotnet test` after each major phase, and verify live WPF UI + local Windows startup state before completion.

**Goal:** Fix FileOrgy's Windows Startup / Task Manager visibility & cross-user Task Scheduler bug, add non-destructive pipeline simulation and full Core feature parity, and completely rebuild the WPF UI/UX workflow using modern design system tokens, vector iconography, inline inspectors, dual Dark/Light theming, and micro-interactions.

**Architecture:**
- Keep `FileOrgy.Core` clean and cross-testable on `net10.0`, adding a non-destructive `PipelineSimulator` / `WorkflowEngine` simulation API and `ConfigService` helpers without breaking existing JSON persistence or the 49 existing tests.
- Rebuild `FileOrgy.App`'s startup & elevation service (`StartupHelper.cs`) to expose full diagnostic state (`StartupStatusInfo`), support `/ru` interactive user binding, provide UAC-elevated repair/mode-switching (Standard Task-Manager-visible `HKCU\...\Run` vs. Elevated Task Scheduler `FileOrgy_ElevatedAutoStart`), and fix `FileOrgy.iss` + `tools/setup_elevated_autostart.ps1`.
- Replace all generic Tailwind-Slate/Indigo inline XAML and emoji chrome with a 3-layer WPF `ResourceDictionary` token architecture (`Themes/Tokens.xaml`, `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`, `Themes/Icons.xaml`, `Themes/Controls.xaml`), custom `WindowChrome` shell, and purpose-built views (`Views/OverviewView.xaml`, `Views/WatchFoldersView.xaml`, `Views/RulesView.xaml`, `Views/VariablesView.xaml`, `Views/LogsView.xaml`, `Views/SettingsView.xaml`, plus redesigned `RuleEditDialog.xaml`, `RuleGuideDialog.xaml`, `UpdateDialog.xaml`, and in-app toast/confirm overlays).

**Tech Stack:** .NET 10 (`net10.0-windows` WPF + `net10.0` Core + xUnit 2.9.3), Inno Setup 6 (`FileOrgy.iss`), PowerShell 7.

---

## Global Constraints & Design Contract (Hydrogen)
- **Mode:** Operate (Desktop utility). High scanability, earned Windows 11 familiarity, dense where useful, zero decorative clutter.
- **Anti-Slop Rules:**
  - NO `#0F172A`/`#6366F1` Indigo/Purple AI gradient palette.
  - NO Unicode emojis (`📊`, `📁`, `⚡`, `🗑`, `🚀`, etc.) as UI icons in XAML, Tray menu, or `EnumFriendlyNameConverter`. Use drawn vector `Geometry` icons in `Themes/Icons.xaml`.
  - NO fake hardcoded telemetry (`CPU: 0.0% • RAM: ~20MB`). Display real `WorkingSet64`, watcher counts, and rule counts.
  - NO `MessageBox.Show(...)` for routine feedback, confirmations, or empty-state notices. Use non-blocking in-app Snackbar toasts + themed inline/modal confirmation dialog.
  - NO unstyled WPF defaults (`ListBox` bright blue selection box, Aero checkboxes/scrollbars/textboxes).
- **Palette ("Graphite Carbon" Dark & "Warm Alabaster" Light with Singular Signal Teal Accent):**
  - Dark: Shell `#111317`, Canvas `#16191F`, Surface `#1D212A`, Well `#12151B`, Border `#2A303C`, Accent `#14B8A6` (`#0D9488` hover, `#133A37` active tint, `#5EEAD4` accent fg), Text `#F3F4F6` / `#A1A7B3` / `#6E7583`.
  - Light: Shell `#EFECE6`, Canvas `#F7F5F0`, Surface `#FFFFFF`, Well `#F1EFE9`, Border `#E2DFD7`, Accent `#0F766E` (`#0D645D` hover, `#CCFBF1` active tint, `#115E59` accent fg), Text `#181A1F` / `#525866` / `#737986`.
- **Signature Feature:** Interactive **Pipeline Dry-Run Simulator** (drop or browse any file on Overview or Rules Inspector to preview exact condition evaluations, rule match priority, transformed filename, and destination folder with zero disk mutations, plus optional 1-click live execution).
- **Keyboard Shortcuts:** `Ctrl+1`..`Ctrl+6` navigation, `Ctrl+N` new rule (on Rules), `Ctrl+F` search focus (on Rules/Logs), `F5` scan monitored folders, `Esc` dismiss modal/toast/inspector.

---

## Review Focus (5 Edge Cases to Pin With Tests)
1. **Startup cross-user / elevated detection:** `StartupHelper` parsing `schtasks /query` `Access is denied` vs `cannot find the file specified`, and `IsElevatedOrFlagged` detecting `~ RUNASADMIN` across HKCU/HKLM without throwing when keys are missing.
2. **Dry-run simulation safety:** Simulating `MoveFile`, `SmartRename`, `ExtractArchive`, `RecycleFile`, `PermanentDelete`, and `RunCommand` MUST NOT create directories, move/delete/rename files, extract archives, or spawn processes on disk.
3. **Config backward compatibility:** Loading existing user `%LOCALAPPDATA%\FileOrgy\config.json` (which lacks new UI fields like `HasCompletedOnboarding` or `DarkMode` toggle changes) deserializes cleanly and preserves all rules, variables, keyword lists, and watch folders.
4. **Full Core exposure in `RuleEditViewModel`:** `ScopedFolderIds`, `TargetVariable`, `ContinueOnError`, `CustomExtractPath`, `ArchiveMoveTargetFolder`, `InspectDocument`, `RecycleFile`, `PermanentDelete` round-trip accurately without losing values on Save or Duplicate.
5. **Empty / extreme UI data:** 0 watch folders, 0 rules, very long file paths (`> 200 chars`), invalid regex patterns in Rule Editor or Simulator, and large log feeds (`1000` entries) render without layout overflow, crashes, or UI thread freezes.

---

## Task Breakdown

### Task 1: Core Simulation Engine & Startup/Installer Fixes
**Files:**
- Create: `src/FileOrgy.Core/Services/PipelineSimulator.cs`
- Modify: `src/FileOrgy.Core/Models/Models.cs` (add `HasCompletedOnboarding` optional setting defaulting false; ensure clean `AppSettings`)
- Modify: `src/FileOrgy.App/Services/StartupHelper.cs` (full `StartupStatusInfo`, `StartupMode`, `StartupOperationResult`, `/ru` target user support, `SetRunAsAdminFlag`, elevated repair helper, `GetStartupStatus`)
- Modify: `FileOrgy.iss` (resolve interactive logged-on user for `schtasks /create /ru` when `runasadmin` is selected; keep default non-elevated `HKCU\Run` clean)
- Modify: `tools/setup_elevated_autostart.ps1` (remove hardcoded `C:\Users\swalih\FileOrgy`, use repo root + interactive user lookup)
- Test: `tests/FileOrgy.Tests/PipelineSimulatorTests.cs`, `tests/FileOrgy.Tests/StartupAndConfigTests.cs`

### Task 2: Three-Layer Design Token System, Vector Icon Library & Control Templates
**Files:**
- Create: `src/FileOrgy.App/Themes/Tokens.xaml`
- Create: `src/FileOrgy.App/Themes/DarkTheme.xaml`
- Create: `src/FileOrgy.App/Themes/LightTheme.xaml`
- Create: `src/FileOrgy.App/Themes/Icons.xaml`
- Create: `src/FileOrgy.App/Themes/Controls.xaml`
- Create: `src/FileOrgy.App/Services/ThemeService.cs` (live Dark/Light dictionary swap + Win11 DWM immersive dark titlebar P/Invoke)
- Modify: `src/FileOrgy.App/App.xaml`, `src/FileOrgy.App/App.xaml.cs`
- Modify: `src/FileOrgy.App/Converters/CommonConverters.cs` (remove all emojis from `EnumFriendlyNameConverter`; add converters for `StartupMode`, `StepType` icon/color, `SimulationResult`, relative timestamps, `InverseBool`, etc.)
- Modify: `src/FileOrgy.App/Services/TrayService.cs` (replace emoji in tray menu item `"⚡ Scan Monitored Folders"` with clean text `"Scan Monitored Folders"`, update tray icon palette to Signal Teal `#14B8A6` / `#0F766E` matching rebuilt brand)

### Task 3: ViewModels & Navigation / Toast / Confirmation Architecture
**Files:**
- Create: `src/FileOrgy.App/Services/NotificationService.cs` (in-app Snackbar toast queue + async confirmation prompt service, eliminating `MessageBox.Show`)
- Create: `src/FileOrgy.App/ViewModels/PipelinePreviewViewModel.cs` (shared interactive file drop/pick + dry-run simulation ViewModel used in Overview and Rules Inspector)
- Modify: `src/FileOrgy.App/ViewModels/MainViewModel.cs` (keyboard shortcut navigation, live real RAM telemetry, startup health banner state, toast/confirm host bindings, theme toggle support)
- Modify: `src/FileOrgy.App/ViewModels/DashboardViewModel.cs` (richer session + today telemetry, active rules quick list, simulator integration, onboarding quick-setup command)
- Modify: `src/FileOrgy.App/ViewModels/WatchFoldersViewModel.cs` (quick-add Downloads/Desktop/Documents, directory existence status, inline editing of all properties + add/remove ignore pattern chips, inline confirm remove)
- Modify: `src/FileOrgy.App/ViewModels/RulesViewModel.cs` (search filter, folder scope summary, inline pipeline preview steps, toast/confirm integration instead of `MessageBox`, fix `DuplicateRule` copying missing `CaseTransform`/`EnqueueExtractedFiles`/`CustomExtractPath`/`ArchiveMoveTargetFolder`/`CommandExecutable`/`CommandArguments`!)
- Modify: `src/FileOrgy.App/ViewModels/RuleEditViewModel.cs` (expose `ScopedFolderIds`, `TargetVariable`, `ContinueOnError`, `CustomExtractPath`, `ArchiveMoveTargetFolder`, `IsInspectVisible`, `IsRecycleOrDeleteVisible`, `IsCustomVariableVisible`, `IsValueInputVisible`, plus live step output preview!)
- Modify: `src/FileOrgy.App/ViewModels/VariablesViewModel.cs` (inline add/edit variable with validation + copy token, keyword list tag chip add/remove/bulk-import)
- Modify: `src/FileOrgy.App/ViewModels/LogsViewModel.cs` (event type filter, date range quick filter, toast feedback instead of `MessageBox`)
- Modify: `src/FileOrgy.App/ViewModels/SettingsViewModel.cs` (full `StartupStatusInfo` bindings, `RunAsAdmin` toggle, `RepairStartupCommand`, `DarkMode` live theme switch, validation of numeric settings)

### Task 4: Rebuilt WPF Views & Dialogs
**Files:**
- Create: `src/FileOrgy.App/Views/OverviewView.xaml` (+ `.cs` for drag-and-drop file handling)
- Create: `src/FileOrgy.App/Views/WatchFoldersView.xaml` (+ `.cs`)
- Create: `src/FileOrgy.App/Views/RulesView.xaml` (+ `.cs`)
- Create: `src/FileOrgy.App/Views/VariablesView.xaml` (+ `.cs`)
- Create: `src/FileOrgy.App/Views/LogsView.xaml` (+ `.cs`)
- Create: `src/FileOrgy.App/Views/SettingsView.xaml` (+ `.cs`)
- Modify: `src/FileOrgy.App/MainWindow.xaml`, `src/FileOrgy.App/MainWindow.xaml.cs` (custom `WindowChrome` shell, clean left navigation rail with vector icons, dynamic top bar, Startup/Update InfoBar, Toast overlay, In-App Confirmation modal sheet, keyboard shortcuts)
- Modify: `src/FileOrgy.App/Views/RuleEditDialog.xaml`, `RuleEditDialog.xaml.cs` (3-zone pipeline builder with Folder Scope, TargetVariable, ContinueOnError, all 11 step types, live token preview)
- Modify: `src/FileOrgy.App/Views/RuleGuideDialog.xaml`, `RuleGuideDialog.xaml.cs` (themed Read-mode visual guide, remove `C:\Users\swalih` example path)
- Modify: `src/FileOrgy.App/Views/UpdateDialog.xaml`, `UpdateDialog.xaml.cs` (aligned with theme tokens)
- Modify: `GEMINI.md` (fix hardcoded `C:\Users\swalih` link if appropriate or leave minimal)

### Task 5: End-to-End Build, Test, Packaging, Local Startup Repair & Live Verification
- Run `dotnet build FileOrgy.slnx -c Release` and `dotnet test FileOrgy.slnx` (ensure 0 warnings, 0 errors, all old + new tests pass).
- Publish self-contained `dist\publish\FileOrgy.exe` and compile `FileOrgy.iss` via `ISCC.exe` to verify installer builds cleanly!
- Fix the user's local machine startup issue cleanly (with clear user-level/admin verification: e.g., verify whether the user's installed `C:\Program Files\FileOrgy\` should be updated or startup registration fixed — wait: let's check permissions for `Program Files` and `schtasks` from our shell, update/repair what we can or create an elevated prompt/script if UAC is required, and verify Task Manager Startup Apps visibility!).
- Launch the rebuilt `FileOrgy.exe` (test instance / live window) and visually/programmatically inspect all 6 views + `RuleEditDialog` + `RuleGuideDialog` + Dark/Light toggle + Dry-Run Simulator using Windows UI Automation / screenshot capture!
