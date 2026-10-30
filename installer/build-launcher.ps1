<#
Builds windowfit-launcher.exe for one architecture with MSVC (Visual Studio or its Build Tools, with the C++ x64/x86
and ARM64 tools).
Usage: powershell -NoProfile -ExecutionPolicy Bypass -File installer\build-launcher.ps1 -Arch x64|x86|arm64 -OutDir <dir>
#>
param(
    [Parameter(Mandatory)][ValidateSet('x64', 'x86', 'arm64')][string]$Arch,
    [Parameter(Mandatory)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw "vswhere not found at ${vswhere}: install Visual Studio Build Tools with the C++ tools" }
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'no Visual Studio with the MSVC x64/x86 tools' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvarsall.bat'
# vcvarsall looks vswhere up on the Path.
$env:Path = (Split-Path $vswhere) + ';' + $env:Path
# The host is x64; arm64 is a cross build.
$target = @{ x64 = 'x64'; x86 = 'x64_x86'; arm64 = 'x64_arm64' }[$Arch]
New-Item -ItemType Directory -Force $OutDir | Out-Null
$source = Join-Path $PSScriptRoot '..\src\WindowFit.Launcher\launcher.c'
$out = Join-Path (Resolve-Path $OutDir) 'windowfit-launcher.exe'
$obj = Join-Path (Resolve-Path $OutDir) 'launcher.obj'
# /MT links the C runtime in: the launcher loads nothing but system DLLs.
$cl = "cl /nologo /W4 /WX /O2 /MT /GS /guard:cf /DUNICODE /D_UNICODE /Fo`"$obj`" `"$source`" /link /SUBSYSTEM:WINDOWS /DYNAMICBASE /NXCOMPAT /GUARD:CF /OUT:`"$out`" kernel32.lib"
& cmd.exe /d /c "call `"$vcvars`" $target >nul && $cl"
if ($LASTEXITCODE -ne 0) { throw "cl $Arch failed ($LASTEXITCODE)" }
Remove-Item $obj -ErrorAction SilentlyContinue
"windowfit-launcher.exe ($Arch): $out"
