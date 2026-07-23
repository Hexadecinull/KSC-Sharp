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
- **Discord Rich Presence** – show what you're playing, with a dynamic per-game icon
  (best-effort), account detection, and activity joining, with the same granular controls
  Bloxstrap offers (see [Discord Rich Presence](#discord-rich-presence)).
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

Found under Integrations, first section. **Enable activity tracking** watches Korone's own log
file for two things the client publicly reports about itself - neither needs any credential:

- A `KORONESTRAPSDK` marker, the same channel Korone's own official Discord RPC client reads
  for live in-game status text.
- A `[FLog::GameJoinLoadTime]` line (confirmed against a real captured client log, not just
  documentation) containing the place/universe you joined and your own account id - the client
  self-reporting its own current session, the same way it already self-reports a server IP
  elsewhere in this project.

This feeds Discord's status text, resolves the game name/icon for Rich Presence, and populates
**Show Korone account** - for any launch, not just ones from a join link. It also gates **Query
server details** (see roughly where your current game server is hosted).

### Discord Rich Presence

Found under Integrations, right after Activity Tracking - it depends on activity tracking being
on, plus the Discord desktop app installed and running. Off by default:

- **Enable Discord Rich Presence** - the master on/off switch (off by default).
- **Show game activity** - whether the specific client/details are published once connected,
  separate from the connection itself (so you can stay "connected" without broadcasting details).
- **Discord status display** - "Name" shows just that you're playing Korone; "Details" adds the
  live status from Activity Tracking, or the detected game name, or the client year, in that
  order of preference. Defaults to Name.
- **Allow activity joining** - lets friends join your game directly from your Discord profile.
  Off by default. When a friend clicks Join, KSC-Sharp opens the game's page in your browser
  rather than attempting to launch the client directly - see the note below on why.
- **Show Korone account** - shows your account id once Activity Tracking has seen one, either
  from a join link's userId or the client's own log (see above). Never from reading account
  credentials.

Every toggle above takes effect immediately if Rich Presence is already showing, not just on
the next launch.

**Dynamic per-game icon and a real "Play"/"View Game" button**: once Activity Tracking has
detected a placeId (from a join link or the client's own log), KSC-Sharp attempts to resolve it
into a game name and icon via a public API call - this part is a best-effort guess at Korone's
actual endpoint (see `KoroneGameInfoClient.cs`'s doc comment for exactly what's confirmed versus
guessed), so it may not always resolve; when it doesn't, Rich Presence falls back to the static
KSC-Sharp icon and a generic "Play Korone" button, exactly as before. **If you can open your
browser's Network tab on a real pekora.zip game page and capture the request that loads the
game's name/icon, that's the one piece of information that would make this fully reliable
instead of best-effort.**

**Why activity joining opens a browser instead of launching directly**: joining a friend's
specific game server needs a negotiated authentication ticket for that server instance, the
same way a `pekora-player://` join link carries one. There's no way to get an equivalent ticket
for a friend-initiated Discord join without capturing and reusing authentication material this
project deliberately doesn't touch (see [Data & Privacy](#data--privacy) below) - and even
setting that aside, reusing the *game-launch* ticket for an unrelated purpose risks consuming a
likely single-use credential and breaking the actual join. Opening the browser sidesteps this
entirely: it already has your real logged-in session, so it can complete the join through
Korone's own normal web flow.

The Discord Application ID and small-image badge are Korone's own real, official ones - lifted
directly from Korone's own "KoroneStrap" Discord RPC client source (K-major; a different,
separate project from koroneStrap the Python bootstrapper this whole app is a rewrite of).

> **Maintainer note:** the *static* large-image art (`KoroneConfig.DiscordLargeImageKey`,
> `logo` - what shows when a per-game icon isn't available) still needs to be uploaded under
> the Discord application's Rich Presence → Art Assets page. Until it's uploaded, Rich Presence
> connects and updates fine, it just won't show a large icon when it's falling back to the
> static one.

### Server details lookup

Under Integrations → Activity tracking. Reads the client's own log file for the server you
joined, then looks up its rough location (city/region/country) via a public IP lookup - this
doesn't go through pekora.zip or need a proxy, it's a direct lookup against the server IP
itself. What's unverified: the log line this looks for is Roblox's well-documented format,
which Korone likely shares since it's Roblox-compatible, but that hasn't been confirmed
against a real Korone log. If "Check Now" never finds anything, that's the first thing to
check - see `ServerLocator.cs`.

### Window manipulation

Under Integrations. **Enable window manipulation** lets KSC-Sharp look up the running client's
window handle - groundwork for future features (always-on-top, custom title bar, etc.), not a
feature in itself yet. Windows only for now; Linux (X11/Wayland) and macOS (Cocoa) use entirely
different windowing APIs that would need separate implementations.

**Enable Borderless Fullscreen for Vulkan** builds on that: real window manipulation (strips the
title bar/border, resizes to the display), not a FastFlag. Vulkan always forces exclusive
fullscreen regardless of client settings, so getting a *borderless* window out of it has to
happen at the window level instead. Only interactive when Graphics API (under Global Settings)
is set to Vulkan.

### Join links

Opening a `pekora-player://` link now shows a small loading window with live status - "Reading
link...", "Preparing the client...", "Starting Korone..." - instead of silently launching (or
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

- **Current Graphics API** (Experimental) - switch between Direct3D, OpenGL, and Vulkan.
  This uses real, documented Roblox-engine flags, not injection - Korone being Roblox-compatible,
  these very likely work the same way, though that hasn't been confirmed against a real
  Korone install. Independent sources disagree on the exact Direct3D flag name
  (`FFlagDebugGraphicsPreferD3D11` vs. `...PreferDirect3D11`), so KSC-Sharp sets both when
  targeting Direct3D - harmless if one is wrong, since unrecognized flags are just ignored.
  Worth knowing: official Roblox added a server-side allowlist for which FastFlags actually
  take effect in September 2025; the OpenGL/Vulkan flags are confirmed to be on it, but that's
  Roblox's own policy, and Korone - especially the older client years this app supports, which
  predate that allowlist entirely - may or may not follow it. That uncertainty, not a bug in
  this app, is why it's marked Experimental. **Check Which Renderer Actually Loaded**, right
  below, gives you a way to verify empirically what actually happened, since this is genuinely
  something neither the client-engine source (not part of the bootstrapper repo this project
  is otherwise built from) nor this app can determine on its own - only running it can.
- **Framerate Limit** - unlocks the client's FPS cap (`DFIntTaskSchedulerTargetFps`, default
  60). Going above 240 FPS isn't recommended - Roblox's own engine can behave oddly above
  that, independent of KSC-Sharp.

Both are applied alongside whatever's in the FastFlags editor whenever you launch a client.
**Apply & Verify Now** applies the current presets immediately and reads them back from each
installed client's settings file to confirm they actually stuck, without needing to launch a
client and check the Log tab first.

**Check Which Renderer Actually Loaded** scans the most recent client log for lines mentioning
a graphics API. Unlike some other log-based features in this app, there's no confirmed exact
log line format for renderer selection - so rather than claim a definitive parse it doesn't
actually have, this surfaces the raw matching lines for you to read and judge yourself. Run it
after launching a client with a Graphics API preset applied. **If you can confirm which
renderer actually loaded and what (if anything) the log said about it, that turns this from a
best-effort scan into a properly verified parser** - the same process that already worked for
the Query Server Details and Activity Tracking features in this app.

### FastFlags presets

Under FastFlags → Presets → Geometry:

- **Mesh detail** - forces lower-detail meshes for performance
  (`DFFlagDebugRenderForceTechnologyVoxel` + `DFIntDebugFRMQualityLevelOverride`). Off by
  default (normal engine detail). Worth flagging: this one is less certain than the Graphics
  API / Framerate flags above - the sources describing it don't cleanly pair each flag with an
  exact description, so this is the best-supported reading of what's available, not a fully
  confirmed one.

## Data & Privacy

KSC-Sharp deliberately never reads stored account credentials - saved passwords, session
cookies, or authentication tickets/tokens on disk or in memory belonging to Korone or your
browser. Every account-related feature in this app (Show Korone account, the dynamic Discord
game icon, server details) is built entirely from information the client itself already
publishes to its own log file, or that arrives directly in a `pekora-player://` link you
opened - never anything intercepted or extracted from where your login session is actually
stored. If a feature would require that, it's built to fail gracefully and fall back rather
than reach for it (see [Discord Rich Presence](#discord-rich-presence) above for a concrete
example - the "Play"/"View Game" button opens your browser instead of attempting a client
launch that would need exactly that kind of credential).

This is a real, load-bearing limitation, not just a caveat: it means a small number of features
(a fully dynamic per-game Discord icon on every launch, joining a friend's exact server
instance directly rather than through the browser) work in a more limited way than they could
if this boundary weren't there.

What KSC-Sharp *does* store, in its own AppData directory (see the About tab → Data in-app for
the exact path): app settings, a local FastFlags cache, Korone Studio install locations, and the
last-seen account id and place/universe id the client itself reported. None of it is a
credential - it's either configuration you entered yourself or public identifiers the client
already logs about its own current session. That directory is still hardened to be readable
only by your own user account as defense-in-depth (Windows: ACLs restricted to you + built-in
Administrators; Linux/macOS: `chmod 700` on the directory, `600` on the files inside it) -
applied automatically, once, the first time the app runs. This mainly guards against unusual
configurations (a shared machine, a loose Linux umask); on a normal single-user install, your
OS already keeps this location private by default.

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
