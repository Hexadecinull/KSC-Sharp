<p align="center">
  <img width="110" height="110" alt="KSC-Sharp logo" src="KSCSL.png" />
</p>
<h1 align="center">KSC-Sharp</h1>
<p align="center"><i>A fast, modern bootstrapper for Korone, built for Windows, Linux, and macOS.</i></p>

<p align="center">
  <a href="https://github.com/Hexadecinull/KSC-Sharp/releases"><b>Download</b></a> ·
  <a href="#building-from-source">Build from source</a> ·
  <a href="#credits--licenses">Credits</a>
</p>

---

## What is this?

[Korone](https://pekora.zip) (formerly known as Pekora) is a Roblox-revival platform that lets you
play both current and legacy client builds. Like any Roblox-style client, it needs a small
launcher program – a **bootstrapper** – that downloads and keeps the game client up to date,
handles `pekora-player://` join links from the browser, and lets you tweak client-side settings.

**KSC-Sharp** is that bootstrapper. It's a from-scratch C# rewrite of the original
[koroneStrap](https://github.com/LittleBigDevs/koroneStrap) Python script, styled after
[Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) (the equivalent tool for actual Roblox),
and – unlike either of those – runs natively on Windows, Linux, and macOS from a single app.

## Features

- **Launch any client year** – 2017, 2018, 2020, and 2021 builds, all from one window.
- **FastFlags editor** – add, edit, and apply client-side FastFlags without hand-editing JSON.
- **Global Settings presets** – switch graphics API (Direct3D/OpenGL/Vulkan, experimental) and
  unlock the framerate limit, without touching raw FastFlags yourself.
- **Bootstrapper management** – download/update the official Korone bootstrapper and run it
  directly from the app.
- **Link handling** – registers `pekora-player://` so join links from the browser open straight
  into KSC-Sharp (Windows registry on Windows, a desktop entry + MIME handler on Linux).
- **Discord Rich Presence** – show what you're playing on your Discord profile, with the same
  granular controls Bloxstrap offers (see [Discord Rich Presence](#discord-rich-presence)).
- **Server details lookup** – see the rough location of the game server you're currently
  connected to.
- **Korone Studio support** (Experimental) – locate, install, update, and launch Studio
  2017/2018/2020/2021 independently.
- **A loading window for join links** instead of launching silently in the background.
- **Sidebar search** – jump straight to any feature by name instead of hunting through tabs.
- **Runs on Linux and macOS**, not just Windows, launching the Windows client through Wine.
- **A real uninstaller** on Windows – removing KSC-Sharp also unregisters the link handler and
  clears everything it downloaded, rather than leaving files behind.

## Getting started

Grab the latest build from the [Releases page](https://github.com/Hexadecinull/KSC-Sharp/releases).
Two kinds of builds are published:

| | Best for |
|---|---|
| **Installer** (`KSC-Sharp-Setup-*.exe`, Windows only – x64 and x86 both built) | Everyday use. Installs properly, adds Start Menu shortcuts, registers the link handler, and uninstalls cleanly. |
| **Portable** (`KSC-Sharp-*.zip`) | Testing a specific build, or running on Linux/macOS. Just unzip and run – nothing is installed or registered until you ask it to be, from the Integrations page. |

Requires the [.NET 8 runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if it isn't
already on your system (the installer doesn't currently bundle it).

### Linux & macOS

The Korone client is a Windows program, so on Linux and macOS it runs under
[Wine](https://www.winehq.org/). Install Wine first (`wine64` or `wine` needs to be reachable –
on macOS via [Homebrew](https://brew.sh) (`brew install --cask wine-stable`), MacPorts, or
[CrossOver](https://www.codeweavers.com/crossover); on Linux via your distro's package manager
or Flatpak). KSC-Sharp looks in the usual install locations for each of these automatically.

macOS support is newer and rougher around the edges than Windows/Linux – if something doesn't
work, that's a known area we're actively improving, not necessarily something you're doing wrong.

## Using KSC-Sharp

**Launch** – pick a client year and click it. If nothing happens, check the Log page; the most
common cause is Wine not being found (Linux/macOS) or the client not being installed yet (run
the bootstrapper first).

**FastFlags** has its own tab: toggle "Allow KSC-Sharp to manage Fast Flags" (on by default –
turning it off stops anything here from being applied, without losing what you've configured),
open the **Fast Flag Editor** to add key/value pairs, and either *Save* (keeps them for next
launch) or *Save & Apply to Installed Clients* (writes them into every installed client
immediately). *Reset everything to defaults* clears both the local cache and whatever's
currently applied to your installs.

**Integrations** is organized into sub-sections: Activity Tracking, Discord Rich Presence,
Window Manipulation, and Custom Integrations (the `pekora-player://` link handler on Windows,
desktop integration on Linux, and Wine status) – roughly in order of "most people will touch
this" to "advanced/platform-specific." Use the sidebar search if you'd rather jump straight to
a feature by name than scroll through sections.

**Global Settings** holds engine-level presets – currently Graphics API and Framerate Limit,
under Presets → Rendering and Graphics.

### Activity Tracking

Found under Integrations, first section. **Enable activity tracking** watches for a
`KORONESTRAPSDK` marker in Korone's own log file – the same public, non-credential channel
Korone's own official Discord RPC client reads from – to pick up live status updates the game
pushes. This feeds Discord's status text when Rich Presence is on, and gates **Query server
details** (see roughly where your current game server is hosted).

### Discord Rich Presence

Found under Integrations, right after Activity Tracking – it depends on activity tracking being
on, plus the Discord desktop app installed and running. Off by default:

- **Enable Discord Rich Presence** – the master on/off switch (off by default).
- **Show game activity** – whether the specific client/details are published once connected,
  separate from the connection itself (so you can stay "connected" without broadcasting details).
- **Discord status display** – "Name" shows just that you're playing Korone; "Details" adds
  the live status from Activity Tracking if one's available, falling back to the client year.
  Defaults to Name.
- **Allow activity joining** – lets friends join your game directly from your Discord profile.
  Off by default, and outbound-only for now (see the ⓘ next to it in-app).
- **Show Korone account** – only ever populated from the userId in a join link you opened
  through KSC-Sharp, never by reading account credentials. Only works after joining a game via
  a `pekora-player://` link at least once; direct in-app launches don't carry an account id.

Every toggle above takes effect immediately if Rich Presence is already showing, not just on
the next launch.

This requires the Discord desktop app to be installed and running.

The Discord Application ID and small-image badge are Korone's own real, official ones – lifted
directly from Korone's own "KoroneStrap" Discord RPC client source (K-major; a different,
separate project from koroneStrap the Python bootstrapper this whole app is a rewrite of). What
that official client also does, and this project deliberately doesn't: it authenticates to
Korone's API using a login ticket to look up exactly which game and icon to show. Capturing or
using an authentication ticket to make API calls as the user is treated the same as reading
account credentials elsewhere in this project – a line not crossed here without it being an
explicit, separate decision. In practice this means the large image is a static KSC-Sharp icon
rather than a live per-game one, and the "Play Korone" button links to Korone generally rather
than the specific game.

> **Maintainer note:** the large-image art itself (`KoroneConfig.DiscordLargeImageKey`, `logo`)
> still needs to be uploaded under the Discord application's Rich Presence → Art Assets page –
> unlike the Application ID and small-image key, that specific key name isn't something the
> official client's own source provided, since it uses a dynamic per-game icon URL there instead.
> Until it's uploaded, Rich Presence connects and updates fine, it just won't show a large icon.

### Server details lookup

Under Integrations → Activity tracking. Reads the client's own log file for the server you
joined, then looks up its rough location (city/region/country) via a public IP lookup – this
doesn't go through pekora.zip or need a proxy, it's a direct lookup against the server IP
itself. What's unverified: the log line this looks for is Roblox's well-documented format,
which Korone likely shares since it's Roblox-compatible, but that hasn't been confirmed
against a real Korone log. If "Check Now" never finds anything, that's the first thing to
check – see `ServerLocator.cs`.

### Window manipulation

Under Integrations. **Enable window manipulation** lets KSC-Sharp look up the running client's
window handle – groundwork for future features (always-on-top, custom title bar, etc.), not a
feature in itself yet. Windows only for now; Linux (X11/Wayland) and macOS (Cocoa) use entirely
different windowing APIs that would need separate implementations.

**Enable Borderless Fullscreen for Vulkan** builds on that: real window manipulation (strips the
title bar/border, resizes to the display), not a FastFlag. Vulkan always forces exclusive
fullscreen regardless of client settings, so getting a *borderless* window out of it has to
happen at the window level instead. Only interactive when Graphics API (under Global Settings)
is set to Vulkan.

### Join links

Opening a `pekora-player://` link now shows a small loading window with live status – "Reading
link...", "Preparing the client...", "Starting Korone..." – instead of silently launching (or
silently failing) in the background. If something's wrong (client not found, launch failed), it
shows the error there instead of just doing nothing.

### Korone Studio

Under the Launch tab (Experimental). Locate, install, update-check, and launch Studio
2017/2018/2020/2021 independently – they're separate portable installs, not one shared thing.

Because a portable install can be anywhere, locating one requires an explicit **Scan drives for
Korone Studio** click under Manage Korone Studio – this never happens automatically. The scan
runs in parallel across directories (not one at a time) and skips known-slow, known-irrelevant
locations (cloud-sync placeholder folders like OneDrive are the standout case – each file in
one can trigger a network round-trip just to check if it exists, which is the single biggest
known cause of a scan taking many minutes), with live "currently scanning: ..." progress and a
Cancel button so a long scan is never indistinguishable from a hung one. It still checks a
short list of plausible executable names rather than one fixed guess, since the real one isn't
confirmed (the download ZIPs are too large to fetch and inspect directly) – if a scan finds
nothing on a drive you know has Studio installed, that name list is the first thing to check.
Update checks compare the server's file signature against what was recorded at install time
rather than downloading the whole archive just to check.

### Global Settings presets

Under Global Settings → Presets → Rendering and Graphics:

- **Current Graphics API** (Experimental) – switch between Direct3D, OpenGL, and Vulkan.
  This uses real, documented Roblox-engine flags (`FFlagDebugGraphicsPreferD3D11` /
  `...PreferOpenGL` / `...PreferVulkan`), not injection – Korone being Roblox-compatible,
  these very likely work the same way, though that hasn't been confirmed against a real
  Korone install. Worth knowing: official Roblox added a server-side allowlist for which
  FastFlags actually take effect in September 2025; these particular flags are confirmed to
  be on it, but that's Roblox's own policy, and Korone – especially the older client years
  this app supports, which predate that allowlist entirely – may or may not follow it. That
  uncertainty, not a bug in this app, is why it's marked Experimental.
- **Framerate Limit** – unlocks the client's FPS cap (`DFIntTaskSchedulerTargetFps`, default
  60). Going above 240 FPS isn't recommended – Roblox's own engine can behave oddly above
  that, independent of KSC-Sharp.

Both are applied alongside whatever's in the FastFlags editor whenever you launch a client.
**Apply & Verify Now** applies the current presets immediately and reads them back from each
installed client's settings file to confirm they actually stuck, without needing to launch a
client and check the Log tab first.

### FastFlags presets

Under FastFlags → Presets → Geometry:

- **Mesh detail** – forces lower-detail meshes for performance
  (`DFFlagDebugRenderForceTechnologyVoxel` + `DFIntDebugFRMQualityLevelOverride`). Off by
  default (normal engine detail). Worth flagging: this one is less certain than the Graphics
  API / Framerate flags above – the sources describing it don't cleanly pair each flag with an
  exact description, so this is the best-supported reading of what's available, not a fully
  confirmed one.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet restore KSC-Sharp.sln
dotnet build KSC-Sharp.sln --configuration Release
dotnet test tests/KoroneStrap.Core.Tests/KoroneStrap.Core.Tests.csproj
dotnet run --project src/KSC-Sharp.App/KSC-Sharp.App.csproj
```

Project layout:

```
KSC-Sharp/
  src/
    KoroneStrap.Core/     # platform logic - version detection, FastFlags, launching, Wine,
                           # URI parsing, Windows/Linux integration, Discord RPC. No UI code.
    KSC-Sharp.App/         # the Avalonia app (Windows/Linux/macOS) - Views/, Program.cs
  tests/
    KoroneStrap.Core.Tests/
  installer/               # Windows installer scaffolds (Inno Setup, WiX, PowerShell)
```

See [CHANGELOG.md](./CHANGELOG.md) for what's changed recently, and open an issue if something's
broken or missing.

## Contributing

Issues and pull requests are welcome. A few things that'll make a PR easier to review:

- Keep platform-specific logic in `KoroneStrap.Core/Platform/`, not in the UI layer.
- If you're touching branding, URLs, or file/folder names, they should live in `KoroneConfig.cs`
  – that's the one place upstream koroneStrap changes get reflected.
- Run the test suite (`dotnet test`) before opening a PR.

## Credits & licenses

KSC-Sharp exists because of the projects it builds on:

| Project | What it contributed | License |
|---|---|---|
| [**koroneStrap**](https://github.com/LittleBigDevs/koroneStrap) (LittleBigDevs) | The original Python bootstrapper this is a rewrite of – version detection, FastFlags application, and Linux integration all follow its lead. | GPL-3.0 |
| [**Bloxstrap**](https://github.com/bloxstraplabs/bloxstrap) (pizzaboxer / Bloxstrap Labs) | UI/UX reference and the model for features like Discord Rich Presence. | MIT |
| [**Korone Bootstrapper**](https://github.com/KoroneX/Korone-Bootstrapper) (Korone) | The original native Windows bootstrapper this whole lineage descends from. | Not published by upstream |

KSC-Sharp itself is licensed under [GPL-3.0](./LICENSE), the same as koroneStrap. Full license
texts for the projects above are included as [LICENSE.KoroneStrap](./LICENSE.KoroneStrap) and
[LICENSE.Bloxstrap](./LICENSE.Bloxstrap).
