<#
Prints the account and the SID of the user who owns the desktop of this process's session, one
per line; exits 1 when the session has no desktop shell. The setup runs it elevated, possibly
with another administrator's credentials, so its own identity is not the signed-in user's.
#>
$ErrorActionPreference = 'Stop'
# Inno decodes captured output as UTF-8; the console default is the OEM code page.
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$session = (Get-Process -Id $PID).SessionId
$shell = Get-CimInstance Win32_Process -Filter "Name = 'explorer.exe' AND SessionId = $session" | Select-Object -First 1
if (-not $shell) { exit 1 }
$owner = Invoke-CimMethod -InputObject $shell -MethodName GetOwner
$sid = (Invoke-CimMethod -InputObject $shell -MethodName GetOwnerSid).Sid
if (-not $owner.User -or -not $sid) { exit 1 }
"$($owner.Domain)\$($owner.User)"
$sid
