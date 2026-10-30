# Changelog

All notable changes to windowfit are listed here, newest first. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Check for updates** clicked while an automatic check runs now shows that check's answer.
- A newer release without a setup for your system's architecture is reported as a failed check, not as "up to date".
- Without administrator rights, windowfit opens the release page at an address it builds from the version.
- The setup of an update gets the system's own `Path`, never folders that other programs add to the machine's `Path`.
- When `windowfit-launcher.exe` itself fails, the task's "Last Run Result" names the Windows error.

## [1.0.3] - 2026-10-30

### Added

- Window groups: windows tiled together on a monitor come back together when one of them is activated. The tray menu's **Groups** item turns them off and on, and the choice is kept.
- Automatic updates: windowfit checks for a new release, shows its notes and installs it on your click, keeping the install scope. Only setups signed with windowfit's release key are run.
- **What's new** in the tray menu, shown once after each update.
- **License** in the tray menu: windowfit's license and the third-party notices.

### Changed

- The logon task starts windowfit through `windowfit-launcher.exe`, which leaves out the environment variables a program of the signed-in user could set to load code into the elevated windowfit.
- The setups are named `windowfit-setup-<arch>.exe`, without the version, so the README's download links always reach the latest release.
- The README has a banner and an animated tour.

## [1.0.0] - 2026-10-23

### Added

- Sixteen Win+arrow chords that fit the foreground window into a half, a quarter, the centre or the whole work area of its monitor, with or without a gap.
- An elevated logon task, so elevated windows can be moved too.
- A setup for x64, x86 and arm64, installing for the signed-in user or for all users.

[1.0.3]: https://github.com/6up3r/windowfit/releases/tag/v1.0.3
[1.0.0]: https://github.com/6up3r/windowfit/releases/tag/v1.0.0
