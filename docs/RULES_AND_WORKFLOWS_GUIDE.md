# ⚡ FileOrgy: Automation Rules & Workflows Guide

Welcome to the comprehensive guide for **FileOrgy Automation Rules & Multi-Step Workflows**. This document explains how rules match files, how sequential workflow pipelines execute, how to use dynamic tokens, and how keyword lists work.

---

## 📑 Table of Contents
1. [Core Concepts: How Rules Work](#1-core-concepts-how-rules-work)
2. [Matching Conditions & Operators](#2-matching-conditions--operators)
3. [Sequential Multi-Step Workflows](#3-sequential-multi-step-workflows)
4. [Dynamic Path & Rename Tokens Cheatsheet](#4-dynamic-path--rename-tokens-cheatsheet)
5. [Reusable Keyword Lists & In-Place Organization](#5-reusable-keyword-lists--in-place-organization)
6. [Step-by-Step Recipes & Real-World Examples](#6-step-by-step-recipes--real-world-examples)

---

## 1. Core Concepts: How Rules Work

When a file is detected in a monitored folder (or when you click **⚡ Scan Monitored Folders**):
1. **Priority Ordering**: FileOrgy evaluates rules from top to bottom based on **Priority (1, 2, 3...)**.
2. **Match Evaluation**:
   - **Match ALL conditions (AND)**: The file must satisfy *every* condition listed in the rule.
   - **Match ANY condition (OR)**: The file triggers if *at least one* condition matches.
3. **Stop on Match**:
   - If checked, once this rule matches and executes its steps on the file, **no lower priority rules will run on that file**.
   - Keep specific rules (e.g. *Smart Invoices*) at higher priority and generic fallback rules (e.g. *In-Place: Others*) at lower priority.

---

## 2. Matching Conditions & Operators

Each condition checks a specific property (**Target**) of the file against an **Operator** and a **Value / Keyword List**.

### Targets
| Target | What it checks | Example |
| :--- | :--- | :--- |
| **File Extension** | Extension without dot | `pdf`, `zip`, `docx` |
| **File Name with Extension** | Full filename | `Invoice_May_2026.pdf` |
| **File Name without Extension** | Base name only | `Invoice_May_2026` |
| **Full File Path** | Complete absolute path on disk | `C:\Users\Downloads\app.exe` |
| **File Size (in bytes)** | Numeric size in bytes | `1048576` (1 MB) |
| **Creation Date** | Date file was created | `2026-05-12` |
| **Modification Date** | Date file was last modified | `2026-05-12` |
| **Document Text Content** | Deep content scanned from PDFs, DOCX, TXT, OCR | Matches words inside documents |

### Operators
| Operator | Description | Use Case |
| :--- | :--- | :--- |
| **Matches Keyword List** | Checks if extension or content matches an entire list | `InKeywordList` ➔ `DocumentExts` |
| **Does NOT Match Keyword List** | Checks if extension is absent from a list | `NotInKeywordList` ➔ `AllSortedExts` (*Others* catch-all) |
| **Equals (exact match)** | Exact string or number comparison | `Extension Equals pdf` |
| **Does Not Equal** | Inverted exact match | `Extension NotEquals tmp` |
| **Contains Text** | Substring match anywhere in name/content | `FileName Contains Invoice` |
| **Does Not Contain Text** | Inverted substring match | `FileName NotContains draft` |
| **Starts With** / **Ends With** | Prefix or suffix matching | `FileName StartsWith IMG_` |
| **Matches Wildcard** | Standard shell wildcards `*` and `?` | `FileName MatchesWildcard report_202?.*` |
| **Matches Regular Expression** | Full regex pattern matching | `^INV-\d{4}-[A-Z]+$` |
| **Greater Than (>)** / **Less Than (<)** | Numeric comparisons | `FileSize GreaterThan 10485760` (>10MB) |
| **Older Than (Days)** | Age check for cleanup routines | `ModifiedDate OlderThanDays 30` |
| **Is Empty File (0 bytes)** | Detects corrupted or empty downloads | Size is 0 bytes |

---

## 3. Sequential Multi-Step Workflows

When a rule triggers, its action steps execute sequentially from top to bottom:
$$\text{Step 1} \longrightarrow \text{Step 2} \longrightarrow \text{Step 3}$$

You can reorder steps anytime using the **▲ Up** and **▼ Down** buttons.

### Available Action Steps
1. **📁 Move File to Folder**:
   - Moves the file to a target destination folder.
   - Example: `{directory}\Documents`
   - Supports conflict resolution: *Auto-rename unique* (`file (1).pdf`), *Overwrite*, *Skip*, or *Keep newer*.
2. **📋 Copy File to Folder**:
   - Duplicates the file into the destination folder while keeping original.
3. **✏️ Smart Rename File**:
   - Renames the file dynamically using formatting tokens.
   - Supports case transforms: *lowercase*, *UPPERCASE*, *Title Case*.
4. **📦 Extract Archive**:
   - Automatically unzips `.zip`, `.rar`, `.7z`, `.tar`, `.gz`, `.xz`, `.bz2`.
   - Output destination: subfolder named after archive or same folder.
   - Post action: keep original archive, delete it, or send to Recycle Bin.
   - *Optionally feeds extracted files back into the organization pipeline!*
5. **📅 Extract Document Date**:
   - Deep-scans text in PDFs, invoices, receipts, and contracts to detect the document billing date.
   - Sets `{doc_date}` for downstream rename and folder move steps.
6. **🔍 Capture Variables via Regex**:
   - Extracts regex capture groups (e.g. invoice number `#?([A-Z0-9-]+)`) into variables accessible via `{var:Name}` or `{match:1}`.
7. **🧹 Clean Up Old Files**:
   - Purges or recycles files older than $N$ days with optional empty folder removal.
8. **⚙️ Run External Script / Command**:
   - Launches external executables, batch files, or PowerShell scripts, passing `{filepath}`.

---

## 4. Dynamic Path & Rename Tokens Cheatsheet

Whenever configuring a **Destination Path** or **Smart Rename Pattern**, click any token chip or type it directly:

| Token | Meaning | Example Value |
| :--- | :--- | :--- |
| `{directory}` or `{dir}` | Source folder where file was detected | `C:\Users\Downloads` |
| `{basename}` | File name without extension | `Annual_Report_2026` |
| `{ext}` | File extension without dot | `pdf` |
| `{dotext}` | File extension with leading dot | `.pdf` |
| `{doc_date:yyyy-MM-dd}` | Date extracted from inside invoice/document | `2026-05-12` |
| `{doc_date:yyyy}` | Year extracted from invoice | `2026` |
| `{doc_date:MM}` | Month extracted from invoice | `05` |
| `{year}` | Current system year | `2026` |
| `{month}` | Current system month (01-12) | `09` |
| `{day}` | Current system day (01-31) | `24` |
| `{var:VariableName}` | Value of custom variable or regex capture | `AcmeCorp` |
| `{counter:001}` | Sequential incrementing counter | `001`, `002`, `003` |

### Path Examples
- **In-Place Subfolder**:
  `{directory}\Documents` ➔ `C:\Users\Downloads\Documents\file.docx`
- **Yearly & Monthly Archive**:
  `{directory}\Archives\{year}\{month}` ➔ `C:\Users\Downloads\Archives\2026\09\file.zip`
- **Dated Invoices**:
  `{directory}\Invoices\{doc_date:yyyy}\{doc_date:yyyy-MM-dd}_{basename}.{ext}`

---

## 5. Reusable Keyword Lists & In-Place Organization

FileOrgy includes over 250+ pre-configured file types grouped into universal keyword lists:

- **`ArchiveExts` (43 formats)**: `.zip`, `.rar`, `.7z`, `.tar`, `.gz`, `.bz2`, `.xz`, `.zst`, `.iso`, `.img`, `.dmg`, `.wim`, `.cab`, etc.
- **`ProgramExts` (34 formats)**: `.exe`, `.msi`, `.msix`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.apk`, `.appimage`, `.sh`, `.jar`, etc.
- **`DocumentExts` (75 formats)**: `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.csv`, `.tsv`, `.txt`, `.md`, `.json`, `.xml`, `.yaml`, `.epub`, etc.
- **`PictureExts` (41 formats)**: `.jpg`, `.png`, `.webp`, `.avif`, `.gif`, `.svg`, `.psd`, `.ai`, `.raw`, `.cr2`, etc.
- **`VideoExts` (29 formats)**: `.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.webm`, `.m4v`, `.flv`, `.ts`, etc.
- **`AudioExts` (29 formats)**: `.mp3`, `.wav`, `.flac`, `.aac`, `.ogg`, `.m4a`, `.opus`, `.alac`, etc.
- **`AllSortedExts`**: Dynamic aggregate list of all sorted extensions above.
- **`FinancialKeywords`**: Document keywords: `invoice`, `receipt`, `bill`, `statement`, `payment`, `tax`, etc.

### The "Others" Catch-All Rule
To organize unknown file formats without losing them:
- **Condition**: `Target: File Extension`, `Operator: Does NOT Match Keyword List`, `Keyword List: AllSortedExts`
- **Action**: Move to `{directory}\Others`
- Any file type that doesn't match active categories (including extensionless files) is automatically sorted cleanly into `Others`.

---

## 6. Step-by-Step Recipes & Real-World Examples

### Recipe 1: Organize Downloads Folder In-Place
1. Go to the **Rules & Workflows** tab in FileOrgy.
2. Click **📥 In-Place Presets** to load all standard category rules.
3. Every file downloaded to your Downloads folder will automatically organize into:
   - `Downloads\Programs\`
   - `Downloads\Documents\`
   - `Downloads\Compressed\`
   - `Downloads\Pictures\`
   - `Downloads\Videos\`
   - `Downloads\Audio\`
   - `Downloads\Others\`

### Recipe 2: Automatic Invoice Renaming & Filing
1. Click **+ New Rule**.
2. **Name**: `Smart Invoice Renamer`.
3. **Conditions** (Match ALL):
   - Condition 1: `File Extension` ➔ `Matches Keyword List` ➔ `DocumentExts`
   - Condition 2: `Document Text Content` ➔ `Matches Keyword List` ➔ `FinancialKeywords`
4. **Workflow Steps**:
   - Step 1: `📅 Extract Document Date`
   - Step 2: `✏️ Smart Rename` ➔ Pattern: `{doc_date:yyyy-MM-dd}_{basename}.{ext}`
   - Step 3: `📁 Move File` ➔ Destination: `{directory}\Invoices\{doc_date:yyyy}`
5. Click **Save & Apply**.

---
*Generated for FileOrgy v1.0.4+ by Antigravity Orchestrator.*
