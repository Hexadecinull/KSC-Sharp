# Changelog

Development history of this rewrite. For end-user documentation, see [README.md](./README.md).

## 1.5.0

This round implements everything left open at the end of 1.4.0's Discord/Graphics API status check, with explicit permission covering the credential-adjacent parts - see the Data & Privacy section below and in the README for exactly where the line ended up.

### Added
- **`[FLog::GameJoinLoadTime]` log parsing** - confirmed against a real captured Roblox client log (not just documentation) to contain placeId, universeId, and userId together, self-reported by the client about its own session. This single addition is what makes several other things below possible without touching any credential.
- **Dynamic per-game Discord icon and "Play"/"View Game" button**, resolved via a best-effort public API call (`KoroneGameInfoClient`) once a placeId is known. The exact Korone endpoint isn't confirmed - only that `www.pekora.zip/api/...` is a real, used prefix (found in the bootstrapper source's own Discord OAuth callback URL). Fails gracefully to the static icon/generic button on any error.
- **"Show Korone account" now works for any launch**, not just join links, sourced from the GameJoinLoadTime line above.
- **Real activity joining**: the Discord IPC client now runs a continuous background read loop (previously it only ever wrote commands and did one blocking read during the handshake) and subscribes to `ACTIVITY_JOIN`. When a friend clicks Join, KSC-Sharp opens the game's page in your browser rather than attempting a direct client launch - see the README for why a direct launch isn't the reliable choice here (it would need a negotiated auth ticket for that specific server instance, which risks consuming a likely single-use credential even if one were available).
- **"Check Which Renderer Actually Loaded"** in Global Settings: scans the most recent client log for graphics-API-related lines. No confirmed exact log format exists for this (unlike GameJoinLoadTime above), so it surfaces raw matching lines for manual verification rather than claiming a definitive parse.
- **AppData directory hardening**: settings.json, the FastFlags cache, and studio.json are now restricted to the current user only (Windows: ACLs; Linux/macOS: `chmod 700`/`600`), applied automatically once per run. Defense-in-depth, not the primary protection - see the new Data & Privacy README section.
- **Windows x86 (32-bit) support** across CI, the installer, and manual releases - Korone ships both architectures natively.
- Defensive dual Direct3D flag naming (`FFlagDebugGraphicsPreferD3D11` and `...PreferDirect3D11`) after finding independent sources disagree on the exact name; setting both costs nothing since unrecognized flags are ignored.

### Changed
- Reviewed the Korone-Bootstrapper C++ source specifically for renderer-selection code - confirmed it contains none (it's genuinely just the old installer/updater, not engine source), which is why Graphics API verification has to happen empirically via the log-scanning tool above rather than by reading the source.
- Version bumped to 1.5.0.

### Explicitly not done, with reasoning
- **No authenticated ticket-based API polling**, even though this was explicitly permitted this round. Two separate reasons, not just caution: (1) the only ticket KSC-Sharp ever sees is the one already flowing through to the client for its own launch/join, likely single-use - capturing it for an unrelated API call risks breaking the actual game launch, which was explicitly something not to do; (2) the GameJoinLoadTime log line already provides placeId/universeId/userId without needing one at all, so building the ticket path wouldn't have added capability, only risk.
- Injection/memory patching was not built. There's no confirmed target for it yet - the bootstrapper source review above ruled out one place it might have been justified (no renderer gating exists there to bypass), and the Graphics API question is still open pending real testing with the verification tool above. Revisit only if that testing shows a concrete gap injection could actually address.

## 1.4.0

### Fixed
- **Real Discord Application ID and small-image key**, sourced directly from Korone's own official Discord RPC client source (also confusingly also called "KoroneStrap", K-major - a separate project from koroneStrap the Python bootstrapper this app is a rewrite of). The placeholder ID from 1.2.0 is gone.
- **Client log directory corrected**: confirmed via the same official source to be `%LocalAppData%\Roblox\logs\`, not `ProjectX`/`Pekora` as previously guessed - this directly affects Query Server Details and the new Activity Tracking mechanism below.
- **CA1416 platform-compatibility warnings** on every CI platform: the `OperatingSystem.IsWindows()` guard lived in the outer method, but the actual restricted calls were inside a `Task.Run` lambda - a separate method body as far as the analyzer is concerned, so the guard didn't count. Moved the check inside the lambda itself.
- **Double ampersand on Studio buttons** ("Download && Install"): this was a plain C# string assigned to `Button.Content`, not XAML markup - it never needed escaping, so writing `&&` just displayed two literal ampersands. Fixed to a single `&`.
- **Discord toggles didn't visibly do anything until the next external trigger**: every toggle under Discord Rich Presence (Show game activity, status display, joining, Show Korone account) only saved the setting - none of them refreshed an activity that was already showing. Now every one re-publishes immediately if Rich Presence is live.
- **Search box background** went white on click: FluentTheme's `:pointerover`/`:focus` states set `Background` on an inner template part directly, silently overriding the plain `Background="..."` attribute set on the `TextBox` itself. Fixed with an explicit style override on both the control and its template part.
- **Manual Release CI**: the pasted failure was GitHub's own infrastructure returning a transient 500 ("Unicorn" error page) during release creation. Replaced `softprops/action-gh-release@v3` with a direct `gh release create` call (the first-party CLI, preinstalled on every runner) wrapped in a 3-attempt retry loop, so a transient failure like this one no longer fails the whole run.
- **Drive scan taking 10+ minutes and finding nothing**: two separate problems. Speed: the scan was strictly single-threaded depth-first recursion with only light pruning - rewritten to scan directories in parallel (bounded concurrency) with much more aggressive pruning, especially of cloud-sync placeholder folders (OneDrive/Dropbox/etc.), where every file check can trigger a slow network round-trip and was very likely the dominant cost on a real-world drive. Visibility: added live "currently scanning: ..." progress text and a Cancel button, since a scan that's still running looked identical to one that had hung. Correctness: the scan only ever checked one guessed executable name; it now checks a short list of plausible candidates instead, and "not found" is at least no longer caused by chasing a single wrong guess.

### Added
- **Activity Tracking now does something real**: watches Korone's own `[KORONESTRAPSDK]` log marker and decodes `presenceStateChange` events from it - the exact mechanism Korone's official Discord RPC client uses, minus the half that needs an authentication ticket (see below). Feeds Discord's status text live.
- **Discord Buttons support** in the IPC client, with a generic "Play Korone" link - matching the official client's use of Buttons, though without a per-game URL (see below for why).
- **Windows x86 (32-bit) support**: Korone natively ships both architectures, so `win-x86` was added alongside `win-x64` across `ci.yml`, `installer.yml`, and `manual-release.yml`. The Inno Setup script is now parameterized by architecture (`/DMyAppArch=`, `/DMySourceDir=`) instead of hardcoded to one build, and produces a correctly-named installer for each.
- **FastFlags → Presets → Geometry → Mesh detail** toggle, forcing reduced mesh/LOD detail for performance. Off by default.
- **Apply & Verify Now** button in Global Settings: applies the current Graphics API / Framerate / Mesh Detail presets immediately and confirms they stuck, without needing to launch a client and check the Log tab.

### Changed
- Sub-category headers/descriptions in the FastFlags page now sit tight against their own content (matching the same fix already applied to Integrations/Global Settings last round).
- Version bumped to 1.4.0.

### Known limitations (unchanged from what they were, restated for clarity)
- Korone's official Discord RPC client also authenticates to its API using a login ticket to look up exactly which game and icon to show. This project deliberately doesn't do that - capturing or using an auth ticket to make API calls as the user is treated the same as reading account credentials elsewhere in this project. In practice: the large Discord image is a static KSC-Sharp icon, not a live per-game one, and the button links to Korone generally rather than a specific game.
- Mesh detail's underlying flags are a best-supported reading of ambiguous sources, not a fully confirmed one - see the README.
- Korone Studio's executable name is still not confirmed against a real install, just narrowed to a short candidate list.

## 1.3.0

### Added
- **Korone Studio support** (Experimental): locate, download/install, update-check, and launch Studio 2017/2018/2020/2021 independently. Since these are portable installs that can live anywhere, locating them requires an explicit drive scan (opt-in, never automatic) that searches bounded-depth for the Studio executable. Update checks compare the server's file signature (ETag/Last-Modified/Content-Length) against what was recorded at install time - a true content hash would mean downloading the whole archive just to check for updates. The executable name this all depends on (`ProjectXStudioBeta.exe`) is inferred from this project's own Player naming convention and Bloxstrap's equivalent split, not verified against a real extracted install - the download URLs are confirmed real and live (fetched and got responses too large to inspect further), but their contents weren't.
- **Loading window for join links**: opening a `pekora-player://` link now shows a small Roblox-bootstrapper-style loading window with live status instead of silently launching (or silently failing) in the background. Fixed a real bug in the process: join-link launches were using the raw FastFlags cache instead of `BuildEffectiveFlags`, so the Graphics API / Framerate presets never applied to anything launched via a join link, only in-app launches.
- **Enable Borderless Fullscreen for Vulkan**, under Window Manipulation: real Win32 window manipulation (strip chrome, resize to the display), not a FastFlag - Vulkan always forces exclusive fullscreen regardless of client settings (confirmed via Bloxstrap's own FastFlags guide), so a *borderless* fullscreen needs to happen at the window level instead. Only interactive when Graphics API is set to Vulkan.
- **"Show Korone account" now does something real**, within an honest limit: the userId from a join link you open through KSC-Sharp gets captured and shown, never anything from reading account credentials/cookies. This only covers clients launched via join link, not direct in-app launches, which don't carry an account id at all.
- Real vector icons for all six sidebar tabs, plus a search icon and a clear button in the sidebar search box - Launch (play triangle), Integrations (plus), FastFlags (hollow flag, solid pole), Global Settings (hollow ring - see note below), Log (list), About (circled i).
- Graphics API apply verification: after applying, KSC-Sharp reads back each installed client's ClientAppSettings.json and confirms the Graphics API flags actually match what was meant to be written, logging any mismatch instead of assuming a clean write always stuck.
- Windows URI registration now reads back what it wrote and reports a specific failure if it doesn't verify, and also sets the conventional protocol description value it was missing before.

### Changed
- **"Pekora" replaced with "Korone"** throughout display text, UI labels, tooltips, and comments - Korone is the platform's current name, Pekora its old one. What did NOT change: the registered URI scheme (`pekora-player://`), the download domain (`pekora.zip`), and the bootstrapper's real filename - those reflect the live platform's actual technical identity today, and renaming them would silently break real interop rather than just being cosmetic.
- Sidebar search rebuilt: results now live in the normal layout flow instead of floating as an overlay, so they push the nav down instead of covering (and blocking clicks on) it. Clearing the search, picking a result, or navigating to any tab now reliably dismisses it - previously it could linger visible after clicking a nav item underneath it.
- Sub-category headers/descriptions in Integrations and Global Settings now sit tight against their own content, with breathing room only between different sub-categories, not within one.
- Version bump policy is now sized to scope per change list rather than a fixed increment (this one: a new feature subsystem plus several substantial fixes → another minor bump).
- Version bumped to 1.3.0.

### Known limitations
- The Global Settings gear icon is a plain hollow ring, not literal gear teeth - a deliberate safety trade-off. A malformed complex icon path can throw a runtime parse exception and take down the page it's on; a plain ring carries none of that risk. Happy to revisit with a verified path if one turns up.
- Korone Studio's executable name and per-version folder layout are inferred, not confirmed - see the Added section above.

## 1.2.0

### Added
- **Global Settings** tab (between FastFlags and Log): Presets → Rendering and Graphics, with **Current Graphics API** (Direct3D/OpenGL/Vulkan, marked Experimental) and **Framerate Limit** (default 60). Both are implemented as real, externally-verified Roblox-engine FastFlags (`FFlagDebugGraphicsPreferD3D11`/`OpenGL`/`Vulkan`, `DFIntTaskSchedulerTargetFps`) merged into whatever's applied on launch - no injection needed.
- **Activity Tracking** is now its own Integrations sub-category (first, ahead of Discord Rich Presence): a new **Enable activity tracking** master toggle gates **Query server details**, which moved under it.
- **Discord Rich Presence** is now its own labeled sub-category with a description, and gets back a **Show game activity** toggle (between the master switch and status display) - separate from the master toggle, so you can stay connected to Discord without publishing what you're playing.
- **Custom Integrations** sub-category at the bottom of Integrations, holding the Windows/Linux/Wine link-handler features that used to be spread across the page.
- **URI Scheme confirmation text** - "URI Scheme registered."/"URI Scheme unregistered." now shows inline next to the buttons, not just in the Log tab.
- **Sidebar search** - a search box the same width as the sidebar, with live results that jump straight to the relevant tab.
- Real vector icons (GitHub mark, alert triangle) for the About page's Source Code / Report an Issue buttons, replacing the emoji placeholders from last round - sourced from the public octicon path data, cross-checked against multiple independent sources.
- `manual-release.yml` rebuilt as four small jobs (validate → build → installer → release) instead of one long script, so a failure partway through doesn't force re-running everything; now also builds and attaches portable zips for all three platforms, not just the Windows installer.

### Changed
- FastFlags tab reordered: Fast Flag Editor first, "Allow KSC-Sharp to manage Fast Flags" second.
- FastFlags editor window resized (750×538) to match a screenshot of real overflow - matching `MinWidth`/`MinHeight` to `Width`/`Height` last round wasn't enough on its own if the window needed to open larger in the first place.
- Version bump policy: from here on, version bumps are sized to the scope of each change list rather than waiting to be told a number.
- Version bumped to 1.2.0.

### Fixed
- Toggle/dropdown right-alignment bug: a shared "Auto" grid column sized to the widest control let some toggles drift left of the card's true right edge. Fixed everywhere with explicit `HorizontalAlignment="Right"`.

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
