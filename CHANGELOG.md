# Changelog

Development history of this rewrite. For end-user documentation, see [README.md](./README.md).

## Unreleased

### Added
- Discord Rich Presence support (show game activity, status display mode, activity joining, account visibility), persisted across restarts. Needs a real Discord Application ID before it does anything — see the README's maintainer note.
- Info tooltips (ⓘ) throughout the app explaining what each section actually does — Bootstrapper, Windows/Linux/Wine integration, FastFlags, experimental client years, the About page's Data section.
- `installer.yml` CI workflow — builds a proper Windows installer (Inno Setup) with automatic uninstall cleanup, separate from the portable `ci.yml` build.
- Manual release workflow now takes a version, release type (release/pre-release/beta/alpha/experimental), and an optional changelog highlight; auto-generates a commit list since the last tag; builds and attaches both the portable zip and the installer with checksums; can be created as a draft for review before publishing.
- About page: licenses and creators for every credited project, a "Project" section linking to source and issues, and a bootstrapper status line (downloaded / last updated) instead of a permanently-visible progress bar.
- README rewritten for end users (install/usage/features) instead of reading like a development log; the development history moved to this file.
- App icon generated from the project logo (`.ico` for Windows, in-app window icon everywhere); Linux desktop integration now installs this bundled icon instead of downloading one.
- Sidebar-nav UI (Launch / Integrations / Discord / Log / About) with a color palette sampled from the project logo.
- 2017 and 2018 client support (marked Experimental — see README).
- Single-instance guard; downloaded files, the local FastFlags cache, and app settings now live in a proper per-user data directory instead of the working directory.
- `--uninstall` now also purges that per-user data directory.
- macOS Wine discovery now checks Homebrew/MacPorts/CrossOver install locations directly, not just PATH.

### Fixed
- `WinExe` output type on Windows — no more console window flashing behind the GUI.
- FastFlags dialog restyled to match the main window: input row and list now share one card instead of floating separately, and an empty state replaces what was a large blank panel with "0 flag(s) loaded" underneath it.
- The bootstrapper progress bar is now hidden except during an active download, instead of permanently visible at 0%.
- `installer/Inno/installer.iss`'s version handling used invalid Inno Setup Preprocessor syntax and referenced its own `#define` before it was set — replaced with a `/DMyAppVersion=` command-line define.
- CI Actions bumped to their Node24 releases ahead of GitHub's Node20 removal.
- `System.UriParser` (a real BCL type) collided with our own `UriParser` class — renamed to `KoroneUriParser`.
- `TextBox` XAML used `VerticalScrollBarVisibility` without the required `ScrollViewer.` prefix (attached property).

## Initial rebuild

Full restart of the project (previously three separate launcher executables — `WindowsLauncher`,
`LinuxLauncher`, `AvaloniaLauncher` — reimplementing the same logic with diverging bugs) into one
`KoroneStrap.Core` library and one cross-platform Avalonia app.

### Fixed (found by diffing against koroneStrap.py)
- FastFlags were saved to a local cache but never written into any installed client's
  `ClientSettings/ClientAppSettings.json` — they had no effect in-game.
  `FastFlagsManager.ApplyToInstalledClients()` now does that.
- Executable/version lookup was missing the `Versions/<version-hash>/<yearFolder>/` directory
  layer that real installs use, so it could never find a normal install.
- No Wine wrapping on Linux/macOS — launches called `Process.Start` directly.
- Windows URI registration lived behind `#if NET8_0_WINDOWS`, a symbol the project never
  actually defined (targets plain `net8.0`), so it was dead code even on Windows.
- The `--uri` handler on Windows only printed the parsed args instead of launching anything.
- The FastFlags editor used Avalonia's `DataGrid` without referencing the package it needs.

### Changed
- Consolidated to one Core library + one cross-platform Avalonia app.
- Added `KoroneConfig.cs` as a single source of truth for branding/URLs/paths.
- Replaced four per-OS CI workflows (three of which had dead path filters and never actually
  ran) with one matrix workflow.
