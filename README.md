<p align="center">
  <img width="96" height="96" alt="KoroneStrap Logo" src="KSCSL.png" />
</p>
<h1 align="center">KSC-Sharp</h1>

<p align="center">
  <i>A C#/.NET 8 port and expansion of <a href="https://github.com/LittleBigDevs/koroneStrap">koroneStrap</a>,
  with Bloxstrap's visual polish as a reference point, built on Avalonia for real cross-platform support.</i>
</p>

---

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
  `RequestedThemeVariant`, `Window.Clipboard`, etc.) is correct — I'm confident in these based
  on Avalonia 11's documented API, but haven't compiled against the real assemblies.

**First thing to do when you pick this up:** run `dotnet restore && dotnet build` from the
repo root somewhere with NuGet access. If the Avalonia layer has an issue, that's where it'll
show up, and it should be a small, mechanical fix rather than a structural one.

## Project layout

```
KSC-Sharp/
  KSC-Sharp.sln
  src/
    KoroneStrap.Core/       # platform-agnostic logic, zero external dependencies
      KoroneConfig.cs       # branding/URLs/paths - the file to touch when upstream changes
      FastFlagsManager.cs
      BootstrapperDownloader.cs
      KoroneUriParser.cs
      Models/
      Platform/
        VersionLocator.cs
        ProcessLauncher.cs
        WindowsUriRegistration.cs
        LinuxIntegration.cs
    KSC-Sharp.App/           # single Avalonia app - Windows, Linux, macOS
      Program.cs             # handles --uri / --install / --uninstall, then starts the GUI
      Views/
  tests/
    KoroneStrap.Core.Tests/
  installer/                 # Windows installer scaffolds (WiX/Inno/PowerShell) - unchanged
                              # in spirit from before, just repointed at the new exe name
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

- **macOS** only has version-root discovery (Wine prefixes + CrossOver bottles). There's no
  macOS desktop integration (icon/URI handling) — upstream koroneStrap itself warns macOS
  support is "very buggy," so this wasn't a regression to chase down yet.
- **2017/2018 client support** is still marked unavailable in `KoroneConfig.ClientVersions`,
  same as upstream — koroneStrap.py itself treats these as WIP in this snapshot.
- **No code signing** in the release workflow — needs a real certificate before shipping installers.
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
