# windowfit

Fits the active window with Win+arrow, on the monitor it sits on, leaving an even gap around
it. It replaces Windows Snap on these chords:

| Keys | Effect |
|---|---|
| Win+Left / Win+Right | left / right half, with a gap |
| Win+Up | half-width box, centred, with a gap |
| Win+Down | centres the window without resizing it |
| Win+Ctrl+Left / Win+Ctrl+Right | left / right half, edge to edge |
| Win+Ctrl+Up | the whole screen, edge to edge (taskbar excluded) |
| Win+Ctrl+Down | the whole screen with a wide gap, wider at the sides |
| Win+Up+Left / Win+Up+Right | top-left / top-right quarter, with a gap |
| Win+Down+Left / Win+Down+Right | bottom-left / bottom-right quarter, with a gap |
| Win+Ctrl+two arrows | the same quarter, edge to edge |

For a quarter, hold one arrow and press the perpendicular one, in either order: the window
first makes the first arrow's move and right after goes to the quarter.

Windows that cannot be resized (fixed dialogs, fullscreen games) stay where they are; Win+Down
still centres them if they can be moved. A maximized window is restored first. It also works
on windows started as administrator (Task Manager, elevated terminals).

While windowfit runs, Win+Ctrl+Left/Right fit the window instead of switching virtual
desktops.
Win+Shift+arrow (moving the window to another monitor) stays the Windows one.

## Requirements

- Windows 11 or Windows 10, in a version supported by .NET 10; x64, ARM64 or 32-bit x86.
- An administrator account, or an administrator's password, to install.
- Only to build from source: .NET SDK 10.0.402 or a later 10.0 version (`global.json` pins the
  SDK).

## Install

1. From the repository's **Releases** page download the setup for your Windows:
   `windowfit-<version>-setup-x64.exe` for most PCs, `-setup-arm64.exe` for ARM PCs (Snapdragon),
   `-setup-x86.exe` for 32-bit Windows 10. A setup started on the wrong kind of Windows says
   which one to download. The `.sha256` file next to each setup holds its hash:
   `(Get-FileHash windowfit-<version>-setup-x64.exe).Hash` must give the same value, ignoring
   case.
2. Run the setup and confirm the administrator prompt. The wizard follows the Windows display
   language (English, Italian, German, French, Spanish, Portuguese, Dutch, Polish, Russian,
   Japanese, Chinese, Korean, Turkish) and asks who uses windowfit:
   - **Only for me**: windowfit starts when you sign in;
   - **For all users of this PC**: it starts for anyone who signs in; on standard accounts it
     runs without administrator rights, so it cannot move windows started as administrator.

The setup installs windowfit in `C:\Program Files\windowfit` and creates the scheduled task
`\windowfit\windowfit`, which starts it at sign-in with administrator rights without asking,
and a **windowfit** entry in the Start menu. If PowerToys FancyZones is set to override Windows
Snap, the wizard says so: windowfit does not need PowerToys, but the two would compete for the
same keys. There is nothing to configure: keys and gaps are fixed.

Running a newer setup over an installed windowfit updates it and keeps the choice made at
install. `/VERYSILENT /SCOPE=all` installs for all users without the wizard.

The setup is not signed: Windows may show the SmartScreen warning "Windows protected your PC";
**More info**, then **Run anyway**.

## Build and try

From the repo folder:

```
dotnet build -c Release
dotnet test
```

To try it without installing, start `src\WindowFit\bin\Release\net10.0-windows\windowfit.exe`
from a normal terminal: it takes Win+arrow at once and closes from **Exit** in the icon's menu.
Started this way it has no administrator rights: on windows started as administrator Win+arrow
stays Windows Snap.

To build the setups, install [Inno Setup 6](https://jrsoftware.org/isinfo.php), publish and run
its compiler (`Arch` is `x64`, `x86` or `arm64`):

```
dotnet publish src\WindowFit\WindowFit.csproj -c Release -r win-x64 --self-contained true -o out\x64
ISCC /DArch=x64 /DAppVersion=1.0.0 /DSourceExe=%CD%\out\x64\windowfit.exe installer\windowfit.iss
```

The setup lands in `installer\Output\`.

The Release build, the installed one included, writes no log. For a diagnosis use the Debug
build: `dotnet build`, then start `src\WindowFit\bin\Debug\net10.0-windows\windowfit.exe`. It
writes to `%LOCALAPPDATA%\windowfit\windowfit.log` the ignored windows, those that did not reach
the exact position and the errors, and its menu also has **Open log**.

## Use

windowfit lives in the notification area: a blue icon with a tiled window, grey while paused.
Its menu, in the Windows display language:

- **Enabled**: unticked, windowfit pauses and Win+arrow is Windows Snap again (Win+Ctrl+Left/Right
  switch virtual desktops); ticked, it takes the keys back. A double click on the icon does the
  same. Every start begins enabled.
- **Exit**: closes windowfit; the **windowfit** entry in the Start menu starts it again, and so
  does the next sign-in.

Status check (changes nothing; run it from a terminal **run as administrator**):

```
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\windowfit\status.ps1"
```

Each line says `OK` or `FAIL`: installed file, task, running process with administrator
rights, FancyZones option.

## Uninstall

**Settings > Apps > Installed apps > windowfit > Uninstall**. It stops windowfit and removes the
task, the folder in Program Files and the Start menu entry; Win+arrow goes back to Windows Snap.

## Troubleshooting

- **Win+arrow does Windows Snap**: windowfit is paused (grey icon: double-click it) or not
  running (start **windowfit** from the Start menu). If that does not help, run `status.ps1`
  or run the setup again.
- **The Start menu opens after Win+arrow**: open an issue in the repository; if you can, attach
  the Debug build's log lines.
- **PowerToys FancyZones**: its "Override Windows Snap" option must stay off, otherwise the two
  programs fight over the same keys. The setup and `status.ps1` check it.

The technical notes for agents are in `AGENTS.md`.

## Credits

windowfit started as a fork of a Linux script developed by
[MatrixDJ96](https://github.com/MatrixDJ96).

## License

MIT: see `LICENSE`.
