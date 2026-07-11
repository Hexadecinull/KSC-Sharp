<p align="center">
  <img width="96" height="96" alt="KoroneStrap Logo" src="KSCSL.png" />
</p>
<h1 align="center">KSC-Sharp</h1>

<p align="center">
  <i>A C#/.NET 8 port and expansion of <a href="https://github.com/LittleBigDevs/koroneStrap">koroneStrap</a>,
  with Bloxstrap's visual polish as a reference point, built on Avalonia for real cross-platform support.</i>
</p>

---

## What changed this round

Reviewed against the real `Korone-Bootstrapper` source (the native C++ bootstrapper the whole
lineage descends from) in addition to koroneStrap.py and Bloxstrap.

- **No more console window on Windows.** `KSC-Sharp.App.csproj` now builds `WinExe` instead of
  `Exe`. This has no effect on Linux/macOS — `OutputType` only changes anything for the Windows
  PE subsystem bit — so it was safe to set unconditionally rather than needing a per-platform
  build.
- **Real app icon**, generated from `KSCSL.png` (the existing project logo) rather than left
  blank: a padded, square-cropped multi-resolution `.ico` for the Windows exe/taskbar
  (`ApplicationIcon` in the csproj), and the same artwork as the in-app window icon on every
  platform. The Linux desktop entry now installs this bundled icon directly instead of
  downloading one from an external GitHub URL at integration-setup time — one less runtime
  network dependency, and it's guaranteed to match what's actually in the repo.
- **Actual UI styling**, not just the default Fluent theme. The palette (`#292551` ink,
  `#6459A6` accent, `#DDD2F9` accent-light) was sampled directly from the logo's own pixels
  rather than picked arbitrarily, so it reads as the same brand as the icon. Layout moved from
  one long stack of bordered sections to a sidebar-nav shell (Launch / Integrations / Log /
  About) closer to how Bloxstrap itself is organized, with primary/secondary/ghost button
  styles and card containers defined once in `App.axaml` and reused throughout.
- **2017 and 2018 are enabled**, on every platform, not just Windows/macOS specifically —
  there was no separate technical path to build for these. `VersionLocator`,
  `ProcessLauncher`, and `FastFlagsManager` were already generic across whatever's in
  `KoroneConfig.ClientVersions`; koroneStrap.py's own menu just hardcoded a "Work in Progress"
  stub for these two entries without ever attempting a launch. They're marked `Experimental`
  in the UI (a small badge, not a hidden/disabled button) rather than presented identically to
  2020/2021, since upstream has never actually verified them and they're much older
  DirectX 9-era builds — more likely to hit their own rendering quirks, especially under Wine.
  That's a real, worth-flagging caveat, not a gap in this codebase.
- **macOS Wine discovery now checks known install locations directly**, not just PATH.
  GUI apps on macOS don't inherit the full interactive-shell PATH the way Terminal does, so
  Homebrew's `/opt/homebrew/bin`, MacPorts' `/opt/local/bin`, and CrossOver's bundled Wine are
  often invisible to a double-clicked `.app` even though `which wine` works fine in Terminal.
  This is one of the more common concrete causes behind "Wine isn't found" on macOS. (I looked
  into also supporting Whisky, a popular Wine wrapper for macOS gaming, but its upstream app is
  now archived/unmaintained and its bottle layout needs either its own CLI tool - a separate,
  optional install most users won't have - or parsing a bottle-UUID plist. Given that, wiring
  it in didn't seem like a good trade for a prototype; CrossOver and plain Wine are covered.)
- **Downloaded files no longer live wherever the app happens to run from.** The bootstrapper
  download and local FastFlags cache now default to a proper per-user data directory
  (`KoroneConfig.AppDataDirectory` — `%LocalAppData%\KSC-Sharp` / `~/.local/share/KSC-Sharp` /
  `~/Library/Application Support/KSC-Sharp`) instead of the current working directory. This
  was a real correctness gap, not just cleanup: for a per-machine Program Files install, writing
  there at runtime as a non-admin user would just fail.
- **`--uninstall` now actually cleans up**, not just the URI registration / Linux desktop
  entry: it also deletes that AppData directory, so nothing is left behind.
- **Single-instance guard** for the GUI: double-clicking the icon while it's already open no
  longer opens a second window. Implemented as a simple exclusive file lock in AppData, held
  for the process lifetime — no named-mutex platform quirks to worry about. Headless flows
  (`--uri`/`--install`/`--uninstall`) intentionally skip this, since they're meant to run and
  exit regardless of whether the GUI is open.
- **Two CI workflows now, not one**: `ci.yml` (portable — publish + upload per OS, unchanged
  from last round) and a new `installer.yml` (Windows-only for now — builds and uploads an
  actual Inno Setup installer). Along the way, `installer/Inno/installer.iss` got a real fix:
  its version handling (`GetString(FileNameExpand(...))`) wasn't valid Inno Setup Preprocessor
  syntax and referenced `{#MyAppVersion}` before the `#define` that set it — it would have
  failed the first time anyone actually ran ISCC against it. It's now passed in via
  `/DMyAppVersion=...` on the command line, and the installer runs `--install`/`--uninstall`
  automatically as part of setup/removal ([UninstallRun], with the required `RunOnceId`), so
  the URI registration and AppData cleanup above are wired into the installer flow itself, not
  just the in-app buttons. One environment-specific detail worth flagging: Inno Setup used to
  ship on GitHub's `windows-latest` runner image but was dropped when the image moved to
  Windows Server 2025, so `installer.yml` installs it explicitly via Chocolatey rather than
  assuming `ISCC.exe` is already on PATH.
- **A couple of small QOL additions**: an About page (version, credits, a button straight to
  the AppData folder) and links out to Bloxstrap/koroneStrap/Korone-Bootstrapper. I kept this
  list short rather than trying to port more of Bloxstrap's feature set (Discord Rich Presence,
  FastFlag presets, etc.) in the same pass as a full styling rewrite — happy to take a specific
  direction on what's actually useful for Korone/Pekora players rather than guessing further.

## What changed in this rebuild

This is a restart of the project, not an incremental patch. The previous layout (separate
`WindowsLauncher` / `LinuxLauncher` console apps plus an `AvaloniaLauncher` GUI, all
reimplementing the same menu logic) had drifted from what koroneStrap.py actually does, and
had accumulated some real, functional bugs. Rather than patch three divergent copies of the
same logic, this rebuild consolidates to **one Core library + one cross-platform Avalonia app**.

### Bugs fixed (found by diffing against koroneStrap.py)

- **FastFlags never reached the game.** The old `FastFlagsManager` only wrote a local
  `fastFlags.json` cache next to the app — it never touched the installed client's
  `ClientSettings/ClientAppSettings.json`, which is what Pekora/Roblox actually reads at
  startup. `FastFlagsManager.ApplyToInstalledClients()` now does that, matching
  `apply_fastflags()` in the Python original.
- **Executable/version lookup skipped a directory level.** Real installs look like
  `Versions/<version-hash>/<yearFolder>/ProjectXPlayerBeta.exe`. The old
  `LauncherHelper.GetExecutablePaths` appended the year folder directly under the Versions
  root, so it could never find a normal install. `Platform/VersionLocator.cs` now iterates
  the version-hash directories first, matching `iter_version_dirs()` / `get_executable_paths()`.
- **No Wine wrapping on Linux/macOS.** Client and bootstrapper launches called `Process.Start`
  directly, which only works on Windows. `Platform/ProcessLauncher.cs` now wraps with
  `wine64`/`wine` (with the same NVIDIA PRIME env vars koroneStrap sets) on other platforms,
  and reports clearly if Wine isn't installed instead of failing silently.
- **Windows URI registration was dead code.** It lived behind `#if NET8_0_WINDOWS`, a symbol
  that's only defined when `TargetFramework` is literally `net8.0-windows` — this project
  targets plain `net8.0`, so that branch never compiled in on any platform, including Windows.
  `Platform/WindowsUriRegistration.cs` uses a runtime `OperatingSystem.IsWindows()` guard
  instead, which is both correct and sufficient (Microsoft.Win32.Registry types are part of
  the base class library reference assemblies regardless of TFM; they just throw off-Windows
  at runtime, which the guard prevents).
- **The `--uri` handler on Windows only printed the parsed args** instead of launching
  anything. The consolidated `Program.cs` now actually resolves and launches the client for
  every platform, not just Linux.
- **`Windows/Installer` scaffolds and the Avalonia FastFlags editor referenced
  `WindowsLauncher.exe`** and used `DataGrid` without the `Avalonia.Controls.DataGrid`
  package being referenced. Installer scripts now point at `KSC-Sharp.App.exe`; the FastFlags
  editor uses a plain `ListBox` instead, so there's no missing-package risk.

### Structural changes

- `Windows/`, `Linux/`, `UI/AvaloniaLauncher/` → **`src/KoroneStrap.Core/` + `src/KSC-Sharp.App/`**.
  One Avalonia app runs on Windows, Linux, and macOS — that's the whole point of Avalonia,
  so three separate launcher executables were working against that. The single app's
  `Program.cs` handles headless flows (`--uri`, `--install`, `--uninstall`) before starting
  the GUI, the same way koroneStrap.py's `__main__` block does.
- **`KoroneConfig.cs`** is a new single source of truth for branding — product name, URLs,
  file/folder names, the client version list. koroneStrap upstream changes these fairly
  often; previously they were scattered as string literals across every file. Now there's
  one place to update when upstream moves.
- **Dropped `Avalonia.ReactiveUI`** from the app's dependencies — it was referenced but never
  actually used anywhere in the old code.
- Four separate per-OS CI workflows → **one matrix workflow** (`ci.yml`, ubuntu/windows/macos).
  Three of the old ones (`macos-ci.yml`, `android-ci.yml`, `ios-ci.yml`) filtered on paths
  (`macOS/**`, `Android/**`, `iOS/**`) that don't exist in the repo, so they never actually
  ran — dead CI. Mobile is intentionally out of scope for this pass; re-add those workflows
  when you actually start that work. Each matrix run now also publishes the app and uploads
  it via `actions/upload-artifact`, so you can grab a runnable build straight from the Actions
  run summary instead of building locally every time.
- **Actions bumped to their Node24 releases** (`actions/checkout@v6`, `actions/setup-dotnet@v5`,
  `actions/upload-artifact@v7`, `softprops/action-gh-release@v3`) ahead of GitHub's Node20
  removal. `auto-lock.yml`/`blatant-duplicates.yml` only shell out to the `gh` CLI directly —
  no JS-runtime actions — so they weren't affected and didn't need changes.

## What's verified vs. what isn't

I had a real .NET 8 SDK available while building this, but **no NuGet access** (this
environment can't reach nuget.org). That split what I could actually check:

- ✅ **`KoroneStrap.Core` compiles clean** — zero errors, zero warnings, via a real `dotnet build`.
  It has no external package dependencies, so this was fully verifiable.
- ✅ **`src/KSC-Sharp.App` and `tests/`** were syntax-checked with the Roslyn compiler bundled
  in the SDK (referencing only the base class library, with expected Avalonia/xunit
  "type not found" errors filtered out). This confirms there are no syntax errors and that
  the test files' calls into Core match Core's real, compiled API. It does **not** confirm
  the Avalonia-specific API usage (named-field codegen from `x:Name`, `ItemsSource`,
  `RequestedThemeVariant`, `Window.Clipboard`, style selectors like `Button.primary:pointerover
  /template/ ContentPresenter`, etc.) is correct — I'm confident in these based on Avalonia 11's
  documented API, but haven't compiled against the real assemblies. The styling pass this round
  is the largest chunk of XAML added so far, so it's the most likely place for a real Avalonia
  compiler (not C#) error to show up — same category as the `AVLN2000` one from last round.
- ✅ **The generated `.ico`** was verified with `file` to actually be a valid multi-resolution
  Windows icon resource (7 sizes, 16×16 up to 256×256), not just a renamed PNG.
- Every `x:Name` referenced from code-behind was cross-checked against what's actually
  declared in each `.axaml` file (and vice versa) — a mismatch there is exactly the kind of
  thing a C#-only syntax check can't catch, since the field is generated from the XAML at
  Avalonia-compile time.

**First thing to do when you pick this up:** run `dotnet restore && dotnet build` from the
repo root somewhere with NuGet access. If the Avalonia layer has an issue, that's where it'll
show up, and it should be a small, mechanical fix rather than a structural one.

## Project layout

```
KSC-Sharp/
  KSC-Sharp.sln
  src/
    KoroneStrap.Core/       # platform-agnostic logic, zero external dependencies
      KoroneConfig.cs       # branding/URLs/paths/AppDataDirectory - touch this when upstream changes
      FastFlagsManager.cs
      BootstrapperDownloader.cs
      KoroneUriParser.cs
      Assets/icon.png        # embedded resource - Linux desktop icon installs from this, no network
      Models/
      Platform/
        VersionLocator.cs
        ProcessLauncher.cs    # Wine wrapping + Homebrew/MacPorts/CrossOver discovery
        WindowsUriRegistration.cs
        LinuxIntegration.cs
    KSC-Sharp.App/           # single Avalonia app - Windows, Linux, macOS
      Program.cs             # --uri / --install / --uninstall, single-instance guard, then GUI
      Assets/icon.ico, icon.png
      Views/
  tests/
    KoroneStrap.Core.Tests/
  installer/
    Inno/installer.iss       # built by .github/workflows/installer.yml
    WiX/, install.ps1         # scaffolds, not wired into CI
```

## Building

```
dotnet restore KSC-Sharp.sln
dotnet build KSC-Sharp.sln --configuration Release
dotnet test tests/KoroneStrap.Core.Tests/KoroneStrap.Core.Tests.csproj
dotnet run --project src/KSC-Sharp.App/KSC-Sharp.App.csproj
```

## Known gaps / what's still not done

This is a prototype, not a finished product:

- **macOS** has version-root discovery (Wine prefixes + CrossOver bottles) and improved Wine
  binary discovery (see above), but still no macOS desktop/URI integration (the Linux desktop
  entry mechanism doesn't translate directly to macOS's `.app`/Info.plist/LaunchServices
  world) — upstream koroneStrap itself warns macOS support is "very buggy," so this remains
  the biggest real gap on that platform.
- **No code signing** in either release workflow — needs a real certificate before shipping
  installers or macOS builds (unsigned macOS apps get Gatekeeper-blocked by default).
- **Android/iOS** are out of scope for this pass (see above).
- The test suite covers `KoroneUriParser`, `FastFlagsManager`'s local cache, and `VersionLocator`'s
  path shapes. It does **not** cover `ApplyToInstalledClients` end-to-end (would need a fake
  filesystem / dependency injection for `VersionLocator`, which felt like over-engineering for
  a prototype) or any of the Avalonia UI.

## Licenses

- [LICENSE](./LICENSE) — GPL-3.0 (KSC-Sharp)
- [LICENSE.KoroneStrap](./LICENSE.KoroneStrap) — GPL-3.0 (original koroneStrap project)
- [LICENSE.Bloxstrap](./LICENSE.Bloxstrap) — MIT (Bloxstrap, referenced for visual/UX inspiration)

## Credits

- [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) — UI/UX reference
- [LittleBigDevs / koroneStrap](https://github.com/LittleBigDevs/koroneStrap) — upstream bootstrapper this ports
- [KoroneX / Korone-Bootstrapper](https://github.com/KoroneX/Korone-Bootstrapper) — original Windows bootstrapper
- SSMG4 — project owner / integrator
