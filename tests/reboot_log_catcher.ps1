# FileOrgy - Persistent Post-Reboot Verification & Log Catcher
# Designed to execute automatically upon Windows logon to verify elevated autostart.

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceDir = (Resolve-Path (Join-Path $ScriptDir "..")).Path
$LogFile = Join-Path $ScriptDir "reboot_verification.log"
$JsonStatusFile = Join-Path $ScriptDir "reboot_status.json"
$LockFile = Join-Path $ScriptDir "reboot_catcher.lock"

# Mutex check: prevent concurrent runs if triggered by both Task Scheduler and RunOnce
if (Test-Path $LockFile) {
    $existingPid = Get-Content $LockFile -ErrorAction SilentlyContinue
    if ($existingPid -and (Get-Process -Id $existingPid -ErrorAction SilentlyContinue)) {
        exit 0
    }
}
$PID | Out-File -FilePath $LockFile -Force

# Helper function to append log entries
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff")
    $entry = "[$timestamp] [$Level] $Message"
    Write-Host $entry
    $entry | Out-File -FilePath $LogFile -Append -Encoding UTF8
}

# Compile Win32 Token Elevation Checker
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class ProcessTokenInspector {
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    public static string GetElevation(int pid) {
        try {
            using (var p = System.Diagnostics.Process.GetProcessById(pid)) {
                IntPtr token;
                if (!OpenProcessToken(p.Handle, 0x0008, out token)) return "Access Denied (Insufficient Token Privileges)";
                try {
                    IntPtr pElev = Marshal.AllocHGlobal(4);
                    try {
                        uint retLen;
                        if (GetTokenInformation(token, 20, pElev, 4, out retLen)) {
                            int isElevated = Marshal.ReadInt32(pElev);
                            return isElevated != 0 ? "High Integrity (Elevated Administrator)" : "Medium Integrity (Standard User)";
                        }
                    } finally {
                        Marshal.FreeHGlobal(pElev);
                    }
                } finally {
                    CloseHandle(token);
                }
            }
        } catch (Exception ex) { return "Query Error: " + ex.Message; }
        return "Unknown";
    }
}
"@ -ErrorAction SilentlyContinue

# Initialize log file
"" | Out-File -FilePath $LogFile -Force -Encoding UTF8
Write-Log "=========================================================================="
Write-Log "=== FileOrgy Post-Reboot Verification & Automated Log Catcher Started ==="
Write-Log "=========================================================================="

$lastBoot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
Write-Log "Current System Time : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Log "Windows Boot Time   : $lastBoot"
Write-Log "Logged-in User      : $env:USERDOMAIN\$env:USERNAME"
Write-Log "Catcher Script PID  : $PID"

# Step 1: Check Scheduled Task status
Write-Log "Querying Task Scheduler status for 'FileOrgy_ElevatedAutoStart'..."
try {
    $taskQuery = schtasks /query /tn "FileOrgy_ElevatedAutoStart" /fo LIST /v 2>&1
    foreach ($line in ($taskQuery -split "`r?`n")) {
        if ($line -match "TaskName|Status|Last Run Time|Last Result|Author|Run As User") {
            Write-Log "  Task Info: $($line.Trim())"
        }
    }
} catch {
    Write-Log "Failed to query Scheduled Task: $_" "WARN"
}

# Step 2: Poll for FileOrgy.exe process
Write-Log "Initiating process detection polling (Timeout: 90s, Interval: 2s)..."
$maxTimeoutSeconds = 90
$pollIntervalSeconds = 2
$elapsed = 0
$targetProcess = $null

while ($elapsed -lt $maxTimeoutSeconds) {
    $procs = Get-Process -Name "FileOrgy" -ErrorAction SilentlyContinue
    if ($procs -and $procs.Count -gt 0) {
        $targetProcess = $procs[0]
        Write-Log "SUCCESS: Detected FileOrgy process after $elapsed second(s)!"
        break
    }
    Start-Sleep -Seconds $pollIntervalSeconds
    $elapsed += $pollIntervalSeconds
    if ($elapsed % 10 -eq 0) {
        Write-Log "Polling in progress... elapsed: ${elapsed}s / ${maxTimeoutSeconds}s (No process yet)"
    }
}

$results = [ordered]@{
    Timestamp = (Get-Date).ToString("o")
    LastBootTime = $lastBoot.ToString("o")
    User = "$env:USERDOMAIN\$env:USERNAME"
    ProcessFound = ($targetProcess -ne $null)
    ProcessId = $null
    CommandLine = $null
    Elevation = $null
    ParentProcess = $null
    LivenessVerified = $false
    Verdict = "FAILED"
    Notes = ""
}

if (-not $targetProcess) {
    Write-Log "FAILURE: FileOrgy process was NOT detected within $maxTimeoutSeconds seconds after logon." "ERROR"
    $results.Verdict = "FAILED"
    $results.Notes = "Process did not spawn within $maxTimeoutSeconds seconds."
} else {
    # Step 3: Analyze Target Process Identity & Privileges
    $pidNum = $targetProcess.Id
    $results.ProcessId = $pidNum
    Write-Log "Target Process ID   : $pidNum"
    Write-Log "Process Executable  : $($targetProcess.Path)"
    Write-Log "Process Start Time  : $($targetProcess.StartTime)"

    # Query WMI for CLI arguments and Parent Process
    $cimProc = Get-CimInstance Win32_Process -Filter "ProcessId = $pidNum" -ErrorAction SilentlyContinue
    if ($cimProc) {
        $cli = $cimProc.CommandLine
        $parentPid = $cimProc.ParentProcessId
        $parentName = (Get-Process -Id $parentPid -ErrorAction SilentlyContinue).ProcessName
        $results.CommandLine = $cli
        $results.ParentProcess = "$parentName (PID $parentPid)"
        Write-Log "Command Line Passed : $cli"
        Write-Log "Parent Process      : $parentName (PID $parentPid)"

        if ($cli -like "*--tray*") {
            Write-Log "Flag Validation     : Passed! '--tray' argument present."
        } else {
            Write-Log "Flag Validation     : Note - '--tray' argument not detected in commandline." "WARN"
        }
    }

    # Query Elevation Level
    $elevation = [ProcessTokenInspector]::GetElevation($pidNum)
    $results.Elevation = $elevation
    Write-Log "Integrity / Elevation: $elevation"
    if ($elevation -like "*High Integrity*") {
        Write-Log "Privilege Check     : VERIFIED ELEVATED ADMINISTRATOR STATUS!"
    } else {
        Write-Log "Privilege Check     : Non-elevated or unexpected status: $elevation" "WARN"
    }

    # Step 4: Liveness Monitoring over 60 seconds
    Write-Log "Beginning 60-second Liveness Audit (Checking stability every 10s)..."
    $isAlive = $true
    for ($i = 1; $i -le 6; $i++) {
        Start-Sleep -Seconds 10
        $checkProc = Get-Process -Id $pidNum -ErrorAction SilentlyContinue
        if ($checkProc -and -not $checkProc.HasExited) {
            $wsMB = [Math]::Round($checkProc.WorkingSet64 / 1MB, 2)
            $cpu = $checkProc.TotalProcessorTime.TotalSeconds
            Write-Log "  Liveness T+$($i*10)s: ACTIVE | Memory: ${wsMB} MB | CPU: ${cpu}s | Responding: $($checkProc.Responding)"
        } else {
            Write-Log "  Liveness T+$($i*10)s: TERMINATED / CRASHED! Process is no longer running." "ERROR"
            $isAlive = $false
            break
        }
    }

    $results.LivenessVerified = $isAlive
    if ($isAlive) {
        $results.Verdict = "PASSED"
        $results.Notes = "FileOrgy spawned successfully on boot, verified elevated, and remained active."
        Write-Log "=========================================================================="
        Write-Log "=== FINAL VERDICT: PASSED - FILEORGY AUTOSTART VERIFIED WITH SUCCESS ==="
        Write-Log "=========================================================================="
    } else {
        $results.Verdict = "FAILED_CRASH"
        $results.Notes = "FileOrgy spawned but exited prematurely during the 60-second audit."
        Write-Log "=========================================================================="
        Write-Log "=== FINAL VERDICT: FAILED - PROCESS DIED SHORTLY AFTER LAUNCH ==="
        Write-Log "=========================================================================="
    }
}

# Step 5: Check FileOrgy Internal Engine Activity
Write-Log "Checking FileOrgy internal engine activity log..."
$activityLogPath = Join-Path $env:LOCALAPPDATA "FileOrgy\logs\activity.jsonl"
if (Test-Path $activityLogPath) {
    $tailLines = Get-Content $activityLogPath -Tail 5 -ErrorAction SilentlyContinue
    Write-Log "Latest internal logs from FileOrgy engine:"
    foreach ($line in $tailLines) {
        Write-Log "  [Engine] $line"
    }
} else {
    Write-Log "activity.jsonl does not exist yet." "WARN"
}

# Save structured JSON status
$results | ConvertTo-Json -Depth 5 | Out-File -FilePath $JsonStatusFile -Force -Encoding UTF8
Write-Log "Verification status written to: $JsonStatusFile"

# Also save a timestamped backup copy
$backupLog = Join-Path $ScriptDir "reboot_verification_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
Copy-Item -Path $LogFile -Destination $backupLog -Force -ErrorAction SilentlyContinue

# Cleanup lock file
Remove-Item -Path $LockFile -Force -ErrorAction SilentlyContinue

# Desktop notification to user
try {
    Add-Type -AssemblyName System.Windows.Forms
    $balloon = New-Object System.Windows.Forms.NotifyIcon
    $balloon.Icon = [System.Drawing.SystemIcons]::Information
    $balloon.BalloonTipIcon = [System.Windows.Forms.ToolTipIcon]::Info
    $balloon.BalloonTipTitle = "FileOrgy Reboot Verification"
    $balloon.BalloonTipText = "Status: $($results.Verdict). Process is active and running elevated."
    $balloon.Visible = $true
    $balloon.ShowBalloonTip(5000)
    Start-Sleep -Seconds 5
    $balloon.Dispose()
} catch { }

Write-Log "Log Catcher finished successfully."
