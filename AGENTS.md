# windowfit

Win+arrow window tiling: a .NET tray app catches sixteen Win+arrow chords with a
low-level keyboard hook and fits the foreground window into a tile of the work area of the
monitor it sits on, with a gap around it. It runs elevated from a logon task so it can also
move elevated windows. An Inno Setup wizard per architecture installs it.

## Stack

- .NET 10 (SDK pinned by `global.json`, `rollForward: latestFeature`), C#, WinForms for the tray
  icon only; Win32 P/Invoke for the hook and the window moves.
- xUnit for `WindowFit.Core`. Inno Setup 6 (`ISCC`, preinstalled on the `windows-latest` runner)
  for the setups. Windows PowerShell 5.1 for `status.ps1` and the setup's session-user helper.

## Layout

| Path | Role |
|---|---|
| `src/WindowFit.Core/` | pure `net10.0` library, no Windows dependency: `Geometry` (tiles, gaps, centring), `Actions` (the sixteen actions, chord matching, window guards), `ChordTracker` (held arrows, repeats, quarter pairing, `Enabled` pause), `FrameCompensation` (visible frame to window rect), `Rect` (physical-pixel rectangle), `TrayText` (tray menu words in 13 languages) |
| `src/WindowFit/` | `windowfit.exe`: `Program` (single-instance mutex, `--start`), `KeyboardHook` (WH_KEYBOARD_LL on its own thread, feeding `ChordTracker`), `WindowMover` (foreground window, eligibility, restore, `SetWindowPos`, read-back), `TrayContext` (tray icon and menu, Enabled toggle, move worker, rehook triggers), `Native` (P/Invoke), `Log` (Debug builds only), `Assets/` (active and paused `.ico`, embedded; the active one is also the exe icon) |
| `tests/WindowFit.Tests/` | xUnit tests of `WindowFit.Core`; they open no window |
| `installer/windowfit.iss` | Inno Setup script, one per architecture through `/DArch=x64\|x86\|arm64 /DAppVersion=X.Y.Z /DSourceExe=<exe>`; output `installer/Output/windowfit-X.Y.Z-setup-<arch>.exe` |
| `installer/CustomMessages.iss` | the setup's own texts in the 13 languages (UTF-8 with BOM) |
| `installer/Languages/ChineseSimplified.isl` | Inno Setup's unofficial Chinese (Simplified) translation, not bundled with Inno Setup; copied unchanged from `jrsoftware/issrc` at `is-6_7_3` |
| `installer/session-user.ps1` | run by the setup: account and SID of the owner of `explorer.exe` in the setup's session |
| `.github/workflows/build.yml` | every push and pull request: Release build with `-warnaserror`, tests, ASCII and parse checks, the three setups and their `.sha256` (artifact `setups`), and a silent install, status check and uninstall of the x64 setup on the runner; a `vX.Y.Z` tag also publishes the six files as a GitHub release |
| `scripts/status.ps1` | read-only check of the exe, the task settings and descriptor, the running process (elevated, read from its token) and the FancyZones override; exits 1 on any failure; the setup installs it beside the exe |
| `scripts/windowfit-common.ps1` | names `status.ps1` uses |
| `README.md` | human entry point (English, like everything published) |
| `LICENSE` | MIT |

## Actions

| Chord | Action | Result |
|---|---|---|
| Win+Left / Win+Right | `FitHalfLeft` / `FitHalfRight` | left / right half, gap `GapHalf` |
| Win+Up | `FitHalfCenter` | half-width box centred on the work area, gap `GapHalf` |
| Win+Down | `CenterOnly` | centred, size unchanged |
| Win+Ctrl+Left / Win+Ctrl+Right | `FillHalfLeft` / `FillHalfRight` | left / right half, no gap |
| Win+Ctrl+Up | `FillScreen` | the whole work area, no gap |
| Win+Ctrl+Down | `FitScreen` | the whole work area, gap `GapScreen`, `2 * GapScreen` left and right |
| Win+Up+Left / Win+Up+Right | `FitQuarterTopLeft` / `FitQuarterTopRight` | top-left / top-right quarter, gap `GapHalf` |
| Win+Down+Left / Win+Down+Right | `FitQuarterBottomLeft` / `FitQuarterBottomRight` | bottom-left / bottom-right quarter, gap `GapHalf` |
| Win+Ctrl+Up+Left / Win+Ctrl+Up+Right | `FillQuarterTopLeft` / `FillQuarterTopRight` | top-left / top-right quarter, no gap |
| Win+Ctrl+Down+Left / Win+Ctrl+Down+Right | `FillQuarterBottomLeft` / `FillQuarterBottomRight` | bottom-left / bottom-right quarter, no gap |

A quarter chord is one arrow held while the perpendicular one goes down, in either order: the
first arrow runs its own action, the second moves the window on to the quarter. An opposite
arrow (Up with Down, Left with Right) runs its own action. Only arrows pressed during the current
Win press pair: an arrow still held from an earlier Win press, or one whose key-up the hook never
saw, does not.

Geometry: `reference = min(area.Width, area.Height)`, `gap = floor(reference * Ratio(r))` with
`Ratio` flooring to two decimals; `mid` rounds half up, so an odd width gives the left half the
extra pixel, and `midY` likewise gives an odd height's extra row to the top quarters; centring floors towards negative infinity. A gap that leaves no width or height
leaves the window alone. Shift or Alt held: no match (Win+Shift+arrow stays the Windows move to
another monitor).

Eligibility: the fit actions need a normal (not tool), movable, resizable (`WS_THICKFRAME`)
window; `CenterOnly` needs a normal, movable one. Not movable: minimized, or fullscreen (frame
covering the monitor, no caption, not maximized). Ignored: no foreground window, invisible,
cloaked, child windows, desktop and taskbar classes. A maximized window is restored first.

## How a move works

1. The hook callback passes every key event to `ChordTracker`, which matches the chord against
   the arrows already held (they pair into a quarter) and tells it to swallow the arrow (down,
   repeats and up). The callback then sends one
   unassigned key (`0xE8`) so releasing Win does not open Start, queues the action and returns.
   It ignores only events tagged with its own `dwExtraInfo`; other injected input (Parsec) is
   matched like typed keys.
2. The worker thread plans on the visible frame (`DWMWA_EXTENDED_FRAME_BOUNDS`) and the monitor
   work area (`rcWork`), all in physical pixels (`PerMonitorV2`).
3. `FrameCompensation` adds back the per-edge invisible borders measured on that window at that
   moment (`GetWindowRect` minus frame), and `SetWindowPos` places it.
4. The frame is read back; a difference from the target is logged in Debug builds and never
   retried (apps may round or clamp their size).

The hook is reinstalled every 5 minutes, on session unlock and on resume, because Windows
removes a low-level hook that exceeds `LowLevelHooksTimeout` without telling the process.

## Install

The setup always runs elevated (`PrivilegesRequired=admin`) and installs to
`C:\Program Files\windowfit` with no directory page. It refuses another architecture's Windows,
naming the right setup. Its scope page picks the task's principal:

- **Only for me**: the session user (by SID) as logon trigger and principal, `InteractiveToken`;
  greyed out when the session has no `explorer.exe`;
- **For all users**: logon trigger for anyone, principal the group `S-1-5-32-545`;
  `/SCOPE=all` selects it on the command line. The previous install's choice is preselected.

Task `\windowfit\windowfit`, written as XML and registered with `schtasks /Create /XML`: run level
`HighestAvailable`, no execution time limit, priority 4, `IgnoreNew`, restart 3 times at 1
minute, runs on batteries. Its descriptor grants read and execute to the session user, or to
Authenticated Users for all-users installs; Task Scheduler drops its `P` flag, so before
registering the setup creates the `\windowfit` folder, or rewrites it when it already exists,
with `D:P(A;;FA;;;BA)(A;;FA;;;SY)(A;;FR;;;AU)`. The CI pre-creates that folder with full control
for Authenticated Users and fails if any non-admin can write the folder or the task after
install. A failed registration shows an error, skips "Launch windowfit now" and makes
the setup exit 1. The Start menu entry `windowfit` and "Launch windowfit
now" run `windowfit.exe --start`, which runs `schtasks /Run` and exits.

Before copying, the setup ends the task and kills every `windowfit.exe`. The uninstaller (Installed
apps) does the same, deletes the task and its folder `\windowfit\` when it is empty, and removes
the files and the Start menu entry. Release builds, the installed one included, write no log;
Debug builds log to `%LOCALAPPDATA%\windowfit\windowfit.log`, which the setup never touches.

One branch, `master`: work lands there and releases are tags on it.

A release is a pushed tag `vX.Y.Z` on a `master` commit: the build job publishes with
`-p:Version=X.Y.Z` (otherwise `0.0.0`) and builds the setups, and the `release` job creates the
GitHub release from its artifact with `gh release create`, using the job's `GITHUB_TOKEN`
(`contents: write` on that job only): three setups and three `.sha256`.

## Gotchas

- `RegisterHotKey` cannot take Win+Left/Right/Up/Down or Win+Ctrl+Left/Right: the shell holds
  them (error 1409). Only a low-level hook overrides them; while windowfit runs,
  Win+Ctrl+Left/Right tile the window instead of switching virtual desktops.
- `ChordTracker` assumes that only the most recently pressed key autorepeats (the Win32 keyboard
  docs do not state it): a key-down for a held arrow that is not the latest key-down is a new
  press whose key-up went to the secure desktop or, for a non-elevated instance, to an elevated
  window.
- A non-elevated hook neither sees keys sent to an elevated foreground window nor may move it
  (UIPI); hence the elevated task.
- An elevated task runs with the user's environment (`HKCU\Environment`, writable without
  elevation). The publish is self-contained, so no `DOTNET_ROOT` runtime lookup, and
  `StartupHookSupport=false` ignores `DOTNET_STARTUP_HOOKS`.
- The installed folder must stay writable only by Administrators, SYSTEM and TrustedInstaller:
  a user-writable exe behind an elevated task is an elevation path. Hence the fixed
  `Program Files` folder, no directory page and no Inno "install for me only" mode
  (`PrivilegesRequiredOverridesAllowed`), which would install into the user's profile.
- Setup's "user" constants (`{username}`, `{localappdata}`) name whoever runs it, which under
  over-the-shoulder elevation is another administrator: the signed-in user comes from
  `session-user.ps1`, and its profile from `HKLM\...\ProfileList\<SID>` (the FancyZones check).
- A line of the `.iss` that starts with `[`, even inside `[Code]`, is read as a section tag.
- `windowfit.exe --start` is handled before the mutex: it only runs `schtasks /Run` and must
  never become a non-elevated instance.
- The pause (`Enabled`) lives in `ChordTracker`, so a rehook keeps it; an arrow swallowed before
  the pause stays swallowed until its key-up. It is not persisted: every start is enabled.
- `Log.Write` is `[Conditional("DEBUG")]`: an elevated process writing into the user-writable
  `%LOCALAPPDATA%` can be redirected by a junction, so Release builds compile every log call out.
  A Debug build runs elevated only for a diagnosis.
- Invisible borders differ per edge and state, typically 7/0/7/7 (L/T/R/B) restored, 8/8/8/8
  maximized, 0 for custom-chrome windows: they are measured per window at move time.
- Without `global.json`, `dotnet` picks the newest installed SDK, previews included.
- A publish is one compressed single-file exe (`PublishSingleFile`, `EnableCompressionInSingleFile`
  in the csproj). `IncludeNativeLibrariesForSelfExtract` stays off: the elevated process would
  extract native code into a user-writable temp folder.
- The workflow pins each action to a commit SHA, with its tag in a comment; an update moves both.
- If PowerToys FancyZones is installed, its "Override Windows Snap" must stay off (the setup
  warns, `status.ps1` checks it).
- A non-elevated instance started first owns the `Local\windowfit` mutex, and the task's
  elevated instance then exits; the setup kills every instance before installing.
  `status.ps1` reads the running process's token and fails unless it is elevated; a token it
  cannot open is a FAIL "token not readable (run elevated)".
- Inno Setup 6.7.3 bundles no Chinese (Simplified) `.isl`; the runner has 6.7.1, so the `.iss`
  uses nothing newer.

## Conventions

- `.ps1` files and `installer/windowfit.iss` are pure ASCII; every script sets
  `$ErrorActionPreference = 'Stop'`. Translations live only in `TrayText.cs` and
  `installer/CustomMessages.iss`.
- Native calls live in `Native.cs`; `WindowFit.Core` stays free of Windows APIs so tests run
  headless.
- Docs are affirmative: they describe what the code IS, in the present tense.

## Do not

- Run a built setup, `windowfit.exe`, `windowfit.exe --start` or anything that registers the
  task without the user's explicit go: each takes over the Win+arrow keys or changes the
  machine.

## Verify your work

- `dotnet build -c Release` with 0 warnings and `dotnet test` all green.
- The scripts are ASCII: in PowerShell, `Get-ChildItem scripts\*.ps1, installer\*.ps1, installer\windowfit.iss | Select-String -Pattern '[^\x00-\x7F]'`
  prints nothing.
- Every script parses with no errors, in PowerShell:
  `Get-ChildItem scripts\*.ps1, installer\*.ps1 | ForEach-Object { $e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$e); "$($_.Name): $($e.Count) errors" }`.
- With Inno Setup installed, each architecture compiles: `ISCC /DArch=<arch> /DAppVersion=0.0.0 /DSourceExe=<published exe> installer\windowfit.iss`
  exits 0. Installing it is the CI's job (a throwaway runner) or the user's.
