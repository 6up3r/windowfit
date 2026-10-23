<#
Reads windowfit's installed state back and prints one OK/FAIL line per check. Changes nothing.
Exits 1 if any check fails. Run elevated; the setup installs it beside windowfit.exe.
Usage: powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\windowfit\status.ps1" [-InstallDir <path>]
#>
param([string]$InstallDir)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'windowfit-common.ps1')
if (-not $InstallDir) { $InstallDir = $DefaultInstallDir }
$exe = Join-Path $InstallDir $ExeName
$failed = 0

function Check([string]$Name, [bool]$Ok, [string]$Detail) {
    $mark = if ($Ok) { 'OK  ' } else { 'FAIL' }
    "{0} {1}: {2}" -f $mark, $Name, $Detail
    if (-not $Ok) { $script:failed++ }
}

Add-Type -Namespace WindowFitStatus -Name Native -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
[DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
[DllImport("advapi32.dll", SetLastError = true)] public static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int length, out int returned);
[DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
'@

# TokenElevation of the process: $true or $false, or $null when this caller may not open the token.
function Get-TokenElevation([int]$ProcessId) {
    $handle = [WindowFitStatus.Native]::OpenProcess(0x1000, $false, $ProcessId)
    if ($handle -eq [IntPtr]::Zero) { return $null }
    try {
        $token = [IntPtr]::Zero
        if (-not [WindowFitStatus.Native]::OpenProcessToken($handle, 0x0008, [ref]$token)) { return $null }
        try {
            $elevation = 0
            $returned = 0
            if (-not [WindowFitStatus.Native]::GetTokenInformation($token, 20, [ref]$elevation, 4, [ref]$returned)) { return $null }
            return $elevation -ne 0
        } finally {
            [void][WindowFitStatus.Native]::CloseHandle($token)
        }
    } finally {
        [void][WindowFitStatus.Native]::CloseHandle($handle)
    }
}

# True when the descriptor's DACL lets $Sid read and run the task (generic or mapped rights).
function Test-TaskRunnable([string]$Sddl, [string]$Sid) {
    $target = New-Object Security.Principal.SecurityIdentifier($Sid)
    $genericReadExecute = 0x80000000 -bor 0x20000000
    $fileReadExecute = 0x001200A9
    foreach ($ace in (New-Object Security.AccessControl.RawSecurityDescriptor($Sddl)).DiscretionaryAcl) {
        if ($ace.AceType -ne 'AccessAllowed' -or $ace.SecurityIdentifier -ne $target) { continue }
        $mask = $ace.AccessMask
        if (($mask -band $genericReadExecute) -eq $genericReadExecute -or ($mask -band $fileReadExecute) -eq $fileReadExecute) { return $true }
    }
    return $false
}

Check 'installed exe' (Test-Path $exe) $exe

$task = Get-WindowFitTask
if (-not $task) {
    Check 'logon task' $false "$TaskPath$TaskName not found (or not readable without elevation)"
} else {
    Check 'task enabled' ($task.State -ne 'Disabled') "state $($task.State)"
    Check 'task action' ($task.Actions[0].Execute -eq $exe) $task.Actions[0].Execute
    Check 'task elevated' ($task.Principal.RunLevel -eq 'Highest') "run level $($task.Principal.RunLevel)"
    # "Only for me" names the user; "For all users" names the Users group.
    $runner = if ($task.Principal.GroupId) { $task.Principal.GroupId } else { $task.Principal.UserId }
    Check 'task logon trigger' (@($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }).Count -eq 1) $runner
    Check 'task time limit' ($task.Settings.ExecutionTimeLimit -eq 'PT0S') "ExecutionTimeLimit $($task.Settings.ExecutionTimeLimit)"
    Check 'task priority' ($task.Settings.Priority -eq 4) "priority $($task.Settings.Priority)"
    # The Start-menu entry runs the task as the signed-in user, who needs read and execute on it.
    $scheduler = New-Object -ComObject Schedule.Service
    $scheduler.Connect()
    $sddl = $scheduler.GetFolder($TaskPath.TrimEnd('\')).GetTask($TaskName).GetSecurityDescriptor(4)
    $runnerSid = if ($task.Principal.GroupId) { 'S-1-5-11' }
        elseif ($task.Principal.UserId -match '^S-1-') { $task.Principal.UserId }
        else { (New-Object Security.Principal.NTAccount($task.Principal.UserId)).Translate([Security.Principal.SecurityIdentifier]).Value }
    Check 'task runnable by its users' (Test-TaskRunnable $sddl $runnerSid) $sddl
}

$process = @(Get-Process -Name ($ExeName -replace '\.exe$') -ErrorAction SilentlyContinue)
Check 'process running' ($process.Count -eq 1) "$($process.Count) instance(s)"

if ($process.Count -eq 1) {
    $elevated = Get-TokenElevation $process[0].Id
    $detail = if ($null -eq $elevated) { "pid $($process[0].Id): token not readable (run elevated)" } else { "pid $($process[0].Id) elevated=$elevated" }
    Check 'process elevated' ($elevated -eq $true) $detail
}

# FancyZones' own Win+arrow override would compete with windowfit's hook.
$fz = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\FancyZones\settings.json'
if (Test-Path $fz) {
    $override = (Get-Content $fz -Raw | ConvertFrom-Json).properties.fancyzones_overrideSnapHotkeys.value
    Check 'FancyZones override Snap off' (-not $override) "fancyzones_overrideSnapHotkeys=$override"
}
exit [int]($failed -gt 0)
