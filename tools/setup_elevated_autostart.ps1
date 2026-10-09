# FileOrgy - Elevated Autostart & Verification Harness Setup
# This script must be executed with Administrator privileges to configure Task Scheduler with Highest Privileges.

param(
    [string]$TargetExe = "C:\Program Files\FileOrgy\FileOrgy.exe",
    [string]$WorkspaceDir = "C:\Users\swalih\FileOrgy"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "=== Configuring FileOrgy Elevated Autostart & Tracker ===" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Check Administrator Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script requires Administrator privileges to register elevated tasks in Task Scheduler."
    exit 1
}

# 2. Clean up blocked / conflicting Run registry keys
Write-Host "`n[1/4] Removing blocked Run registry keys..." -ForegroundColor Yellow
try {
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "FileOrgy" -Force -ErrorAction SilentlyContinue
    Write-Host "  Cleaned HKCU Run key." -ForegroundColor Green
} catch {
    Write-Host "  HKCU Run key already clean." -ForegroundColor Gray
}

try {
    Remove-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "FileOrgy" -Force -ErrorAction SilentlyContinue
    Write-Host "  Cleaned HKLM Run key." -ForegroundColor Green
} catch {
    Write-Host "  HKLM Run key already clean." -ForegroundColor Gray
}

# 3. Create Elevated Scheduled Task for FileOrgy
Write-Host "`n[2/4] Registering Task Scheduler entry: 'FileOrgy_ElevatedAutoStart'..." -ForegroundColor Yellow

$taskName = "FileOrgy_ElevatedAutoStart"
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
Write-Host "  Target User : $currentUser" -ForegroundColor Gray
Write-Host "  Executable  : $TargetExe --tray" -ForegroundColor Gray

# Define Action
$action = New-ScheduledTaskAction -Execute $TargetExe -Argument "--tray" -WorkingDirectory (Split-Path $TargetExe -Parent)

# Define Trigger (At user logon)
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser

# Define Principal (Highest Privileges)
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -RunLevel Highest -LogonType Interactive

# Define Settings (Run on battery, never stop, restart if failed)
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew

# Unregister old task if exists
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

# Register the elevated task
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "FileOrgy Elevated Autostart Pipeline (Runs as Administrator at user logon)" -Force

Write-Host "  Task '$taskName' registered successfully with Highest Privileges!" -ForegroundColor Green

# 4. Register the Background Log Catcher in RunOnce and Task Scheduler
Write-Host "`n[3/4] Registering Post-Reboot Log Catcher..." -ForegroundColor Yellow
$logCatcherScript = Join-Path $WorkspaceDir "tests\reboot_log_catcher.ps1"
$logCatcherCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$logCatcherScript`""

# Register in HKCU RunOnce
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce" -Name "FileOrgyBootLogCatcher" -Value $logCatcherCmd -Force
Write-Host "  Registered in HKCU RunOnce." -ForegroundColor Green

# Also register as a Scheduled Task for redundancy across any fast-startup scenarios
$catcherTaskName = "FileOrgy_RebootLogCatcher"
$catcherAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$logCatcherScript`"" -WorkingDirectory (Split-Path $logCatcherScript -Parent)
$catcherTrigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$catcherPrincipal = New-ScheduledTaskPrincipal -UserId $currentUser -RunLevel Highest -LogonType Interactive
$catcherSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 10)

Unregister-ScheduledTask -TaskName $catcherTaskName -Confirm:$false -ErrorAction SilentlyContinue
Register-ScheduledTask -TaskName $catcherTaskName -Action $catcherAction -Trigger $catcherTrigger -Principal $catcherPrincipal -Settings $catcherSettings -Description "FileOrgy Automated Post-Reboot Verification & Telemetry" -Force
Write-Host "  Registered in Task Scheduler: '$catcherTaskName'." -ForegroundColor Green

# 5. Summary and Verification
Write-Host "`n[4/4] Verifying Setup..." -ForegroundColor Yellow
$verifyTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($verifyTask) {
    Write-Host "  Verification SUCCESS: '$taskName' is $($verifyTask.State) and ready for reboot testing." -ForegroundColor Green
} else {
    Write-Error "Failed to verify '$taskName'."
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "=== Setup Complete! Armed for Windows Restart Testing ===" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
