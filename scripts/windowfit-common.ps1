# Names used by status.ps1; dot-source it. The setup installs both beside windowfit.exe.
$TaskName = 'windowfit'
$TaskPath = '\windowfit\'
$ExeName = 'windowfit.exe'
$DefaultInstallDir = Join-Path $env:ProgramFiles 'windowfit'

function Get-WindowFitTask {
    Get-ScheduledTask -TaskPath $TaskPath -TaskName $TaskName -ErrorAction SilentlyContinue
}
