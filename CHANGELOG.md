# Changelog

Development history of this rewrite. For end-user documentation, see [README.md](./README.md).

## 1.1.0

### Added
- Discord Rich Presence, server details lookup, and window manipulation all moved into (or added directly under) the Integrations tab instead of Discord having its own nav item, with Integrations getting its own description text to match the other tabs.
- New Activity tracking section: **Query server details** looks up the rough location of the server you're connected to, by reading it out of the client's own log file and geolocating the IP. The log format this depends on is Roblox's well-documented pattern; Pekora likely shares it since it's Roblox-compatible, but that's not been confirmed against a real Pekora log.
- New Window Manipulation section: **Enable window manipulation** looks up the running client's window handle (Windows only for now) - groundwork for future features, not a feature by itself yet.
- FastFlags is now its own tab instead of only a dialog: **Allow KSC-Sharp to manage Fast Flags** (on by default) gates whether anything configured here actually reaches the client, **Reset everything to defaults** clears both the local cache and whatever's currently applied, and the **Fast Flag Editor** button opens the same dialog as before, now styled as a Bloxstrap-style settings row.
- About page: Source Code and Report an Issue are now real buttons with icons, not text links.

### Changed
- Discord Rich Presence defaults revisited: "Allow activity joining" now defaults off, "Discord status display" now defaults to Name (was Details).
- 2017/2018 client years are no longer marked Experimental - there was no code-level distinction from 2020/2021 to base that on (the launch path is fully generic), so the badge was removed rather than kept as an unfounded caution label.
- "KoroneX" (the GitHub username) replaced with "Korone" (the actual project name) throughout credits.
- Em dashes replaced with en dashes throughout the project.
- Version bumped to 1.1.0.

### Fixed
- Settings-row toggles (ToggleSwitch/ComboBox) weren't consistently pinned to the card's right edge - a shared "Auto" grid column sized to the widest control (the status-display ComboBox) let narrower toggles drift left within that column. Fixed with explicit `HorizontalAlignment="Right"` on every one of them.
- FastFlags dialog could overflow its own content when resized smaller than its opening size. `MinWidth`/`MinHeight` now match `Width`/`Height`, so it can't shrink past the size it opens at.

## 1.0.0

### Added
- Discord Rich Presence support (show game activity, status display mode, activity joining, account visibility), persisted across restarts. Needs a real Discord Application ID before it does anything – see the README's maintainer note.
- Info tooltips (ⓘ) throughout the app explaining what each section actually does – Bootstrapper, Windows/Linux/Wine integration, FastFlags, experimental client years, the About page's Data section.
- `installer.yml` CI workflow – builds a proper Windows installer (Inno Setup) with automatic uninstall cleanup, separate from the portable `ci.yml` build.
- Manual release workflow now takes a version, release type (release/pre-release/beta/alpha/experimental), and an optional changelog highlight; auto-generates a commit list since the last tag; builds and attaches both the portable zip and the installer with checksums; can be created as a draft for review before publishing.
- About page: licenses and creators for every credited project, a "Project" section linking to source and issues, and a bootstrapper status line (downloaded / last updated) instead of a permanently-visible progress bar.
- README rewritten for end users (install/usage/features) instead of reading like a development log; the development history moved to this file.
- App icon generated from the project logo (`.ico` for Windows, in-app window icon everywhere); Linux desktop integration now installs this bundled icon instead of downloading one.
- Sidebar-nav UI (Launch / Integrations / Discord / Log / About) with a color palette sampled from the project logo.
- 2017 and 2018 client support (marked Experimental – see README).
- Single-instance guard; downloaded files, the local FastFlags cache, and app settings now live in a proper per-user data directory instead of the working directory.
- `--uninstall` now also purges that per-user data directory.
- macOS Wine discovery now checks Homebrew/MacPorts/CrossOver install locations directly, not just PATH.

### Fixed
- `WinExe` output type on Windows – no more console window flashing behind the GUI.
- FastFlags dialog restyled to match the main window: input row and list now share one card instead of floating separately, and an empty state replaces what was a large blank panel with "0 flag(s) loaded" underneath it.
- The bootstrapper progress bar is now hidden except during an active download, instead of permanently visible at 0%.
- `installer/Inno/installer.iss`'s version handling used invalid Inno Setup Preprocessor syntax and referenced its own `#define` before it was set – replaced with a `/DMyAppVersion=` command-line define.
- CI Actions bumped to their Node24 releases ahead of GitHub's Node20 removal.
- `System.UriParser` (a real BCL type) collided with our own `UriParser` class – renamed to `KoroneUriParser`.
- `TextBox` XAML used `VerticalScrollBarVisibility` without the required `ScrollViewer.` prefix (attached property).

## Initial rebuild

Full restart of the project (previously three separate launcher executables – `WindowsLauncher`,
`LinuxLauncher`, `AvaloniaLauncher` – reimplementing the same logic with diverging bugs) into one
`KoroneStrap.Core` library and one cross-platform Avalonia app.

### Fixed (found by diffing against koroneStrap.py)
- FastFlags were saved to a local cache but never written into any installed client's
  `ClientSettings/ClientAppSettings.json` – they had no effect in-game.
  `FastFlagsManager.ApplyToInstalledClients()` now does that.
- Executable/version lookup was missing the `Versions/<version-hash>/<yearFolder>/` directory
  layer that real installs use, so it could never find a normal install.
- No Wine wrapping on Linux/macOS – launches called `Process.Start` directly.
- Windows URI registration lived behind `#if NET8_0_WINDOWS`, a symbol the project never
  actually defined (targets plain `net8.0`), so it was dead code even on Windows.
- The `--uri` handler on Windows only printed the parsed args instead of launching anything.
- The FastFlags editor used Avalonia's `DataGrid` without referencing the package it needs.

### Changed
- Consolidated to one Core library + one cross-platform Avalonia app.
- Added `KoroneConfig.cs` as a single source of truth for branding/URLs/paths.
- Replaced four per-OS CI workflows (three of which had dead path filters and never actually
  ran) with one matrix workflow.
