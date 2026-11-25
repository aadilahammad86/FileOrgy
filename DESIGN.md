# FileOrgy Design System (`DESIGN.md`)

A 3-layer WPF `ResourceDictionary` design system for a utility-first desktop automation workbench with live Dark & Light theme switching.

## 1. Architecture & Resource Layering

Merged in order inside `src/FileOrgy.App/App.xaml`:

1. **Palette Layer** (`Themes/DarkTheme.xaml` or `Themes/LightTheme.xaml`): 40 semantic `SolidColorBrush` resources swapped dynamically at runtime by `Services/ThemeService.cs` alongside Win11 DWM caption/border sync (`DwmSetWindowAttribute`).
2. **Tokens Layer** (`Themes/Tokens.xaml`): Font families (`Font.UI`, `Font.Display`, `Font.Mono`), corner radii (`Radius.Sm=4`, `Radius.Md=6`, `Radius.Lg=8`, `Radius.Xl=12`, `Radius.Pill=12`), and 9 typographic `TextBlock` styles (`Text.PageTitle`, `Text.SectionTitle`, `Text.CardTitle`, `Text.Body`, `Text.Secondary`, `Text.Muted`, `Text.Caption`, `Text.Mono`, `Text.KpiValue`). Note `Radius.Pill` uses `12` rather than `999` because WPF clamps `CornerRadius` per axis and stretches `999` into pointed ellipses on wide badges.
3. **Iconography Layer** (`Themes/Icons.xaml`): 41 closed geometric `F0`/`F1` `<Geometry>` resources drawn on a `24x24` grid for crisp uniform rendering without glyph/emoji dependencies.
4. **Control Styles Layer** (`Themes/Controls.xaml`): Custom templates for cards, wells, badges, primary/secondary/ghost/danger/icon/token buttons, titlebar caption buttons, navigation rail tabs, filter pills, animated toggle switches, placeholder-capable text boxes (`Tag` placeholder), combo boxes, list boxes, scrollbars, sliders, data grids, tooltips, and context menus.

## 2. Color Tokens (40 Key Parity)

| Token | Dark (Carbon Graphite) | Light (Warm Alabaster) | Role |
| :--- | :--- | :--- | :--- |
| `Brush.Shell` | `#111317` | `#EFECE6` | Window chrome & left navigation rail |
| `Brush.Canvas` | `#16191F` | `#F7F5F0` | Primary workspace scroll canvas |
| `Brush.Surface` | `#1D212A` | `#FFFFFF` | Cards & panels |
| `Brush.SurfaceElevated` | `#232834` | `#FFFFFF` | Popovers, toasts, confirmation sheets |
| `Brush.SurfaceHover` | `#262B38` | `#F4F2EC` | Hover overlays |
| `Brush.Well` | `#12151B` | `#F1EFE9` | Nested inputs, inspectors, mono diffs, disabled primary bg |
| `Brush.Border` / `BorderStrong` | `#2A303C` / `#3B4354` | `#E2DFD7` / `#C9C3B8` | Crisp 1px structural framing |
| `Brush.Divider` | `#232833` | `#EAE7DF` | Section dividers |
| `Brush.TextPrimary` | `#F3F4F6` | `#181A1F` | Headings & primary values |
| `Brush.TextSecondary` | `#A1A7B3` | `#525866` | Descriptions & subtitles |
| `Brush.TextMuted` | `#6E7583` | `#737986` | Labels & metadata |
| `Brush.Accent` / `AccentText` | `#14B8A6` / `#5EEAD4` | `#0F766E` / `#0F766E` | Primary actions & active indicators |
| `Brush.Success` / `SuccessText` | `#22C55E` / `#4ADE80` | `#15803D` / `#166534` | Active watchers, valid matches, paths found |
| `Brush.Warning` / `WarningText` | `#F59E0B` / `#FBBF24` | `#B45309` / `#92400E` | Startup/Task Scheduler warnings & paused states |
| `Brush.Danger` / `DangerText` | `#EF4444` / `#F87171` | `#DC2626` / `#991B1B` | Destructive steps (`RecycleFile`, `PermanentDelete`) & errors |
| `Brush.Info` / `InfoText` | `#38BDF8` / `#7DD3FC` | `#0369A1` / `#075985` | Informational logs & system callouts |

All theme-sensitive bindings use `{DynamicResource Brush.*}` so live toggling between Dark (`DarkTheme.xaml`) and Light (`LightTheme.xaml`) updates every open view and modal without restart.

## 3. Window Chrome, Navigation & View Architecture

- **Custom `WindowChrome`** (`MainWindow.xaml`): `WindowStyle="None"`, `CaptionHeight="40"`, `ResizeBorderThickness="6"`, paired with `WM_GETMINMAXINFO` in `MainWindow.xaml.cs` so maximized bounds honor monitor `rcWork` (never overlapping the Windows taskbar).
- **Left Navigation Rail (`220px`)**: Six primary workspaces (`Overview`, `Watch Folders`, `Rules`, `Variables & Lists`, `Logs`, `Settings`) plus pinned status footer displaying active summary (`ActiveWatchSummaryText`), live memory working set (`MemoryUsageText`), and app version.
- **Global Notification Architecture (`Services/NotificationService.cs`)**: Zero `MessageBox.Show` prompts in views/dialogs; interactive confirmations render via an in-app dimmed modal sheet (`Notifications.HasActiveConfirmation`), and non-blocking feedback uses the bottom-right snackbar stack (`Notifications.Toasts`).
- **Keyboard Shortcuts**: `Ctrl+1..6` tab navigation, `Ctrl+N` new rule, `F5` scan monitored folders, `Esc` dismiss confirmation modal.

## 4. Screen Workflow Summary

- **Overview** (`Views/OverviewView.xaml`): KPI telemetry strip, real-time feed, Monitoring Engine & Priority Rules cards, and the non-destructive **Dry-Run Pipeline Simulator** dropzone backed by `PipelineSimulator` + `PipelinePreviewViewModel`.
- **Watch Folders** (`Views/WatchFoldersView.xaml`): Master-detail editor with quick-add known folders (`Downloads`, `Desktop`, `Documents`, `Pictures`), live disk validation, debounce slider (`100..15000` slider / `100..60000` input), and interactive ignore-pattern chip editor.
- **Rules & Workflows** (`Views/RulesView.xaml`): Live filterable priority order list paired with an inline rule inspector (`ScopedFolderIds` summary, conditions, visual sequential pipeline step trace) and an inline rule dry-run bar.
- **Rule Builder Modal** (`Views/RuleEditDialog.xaml`): 3-zone visual pipeline editor supporting folder scope pills, `TargetVariable`, all 10 `RuleTarget`s, all 15 `ConditionOperator`s, all 11 `StepType`s, `ContinueOnError`, clickable token chips with live sample filename preview, and pre-save **Test Draft Rule** simulation.
- **Variables & Keyword Lists** (`Views/VariablesView.xaml`): Inline custom `{var:Key}` editor with token copy plus interactive tag-chip keyword list management and bulk-paste sync.
- **Activity History** (`Views/LogsView.xaml`): Level + Event filter pills, virtualized themed `DataGrid`, bottom detail drawer with path copy/Explorer launch, and CSV/JSON export.
- **Settings & Startup Diagnostics** (`Views/SettingsView.xaml`): Exposes `StartupHelper.GetStartupStatus()` diagnostics (Standard `HKCU\...\Run` Task Manager visibility vs. Elevated Task Scheduler principal conflicts), one-click **Repair Startup Registration**, live Carbon Graphite / Warm Alabaster theme switch, engine concurrency tuning, and GitHub release checks (`Views/UpdateDialog.xaml`).
