<p align="center">
  <img width="110" height="110" alt="KSC-Sharp logo" src="KSCSL.png" />
</p>
<h1 align="center">KSC-Sharp</h1>
<p align="center"><i>A fast, modern bootstrapper for Korone (Pekora), built for Windows, Linux, and macOS.</i></p>

<p align="center">
  <a href="https://github.com/Hexadecinull/KSC-Sharp/releases"><b>Download</b></a> ·
  <a href="#building-from-source">Build from source</a> ·
  <a href="#credits--licenses">Credits</a>
</p>

---

## What is this?

[Korone](https://pekora.zip) (also called Pekora) is a Roblox-revival platform that lets you
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
- **Bootstrapper management** – download/update the official Pekora bootstrapper and run it
  directly from the app.
- **Link handling** – registers `pekora-player://` so join links from the browser open straight
  into KSC-Sharp (Windows registry on Windows, a desktop entry + MIME handler on Linux).
- **Discord Rich Presence** – show what you're playing on your Discord profile, with the same
  granular controls Bloxstrap offers (see [Discord Rich Presence](#discord-rich-presence)).
- **Server details lookup** – see the rough location of the game server you're currently
  connected to.
- **Runs on Linux and macOS**, not just Windows, launching the Windows client through Wine.
- **A real uninstaller** on Windows – removing KSC-Sharp also unregisters the link handler and
  clears everything it downloaded, rather than leaving files behind.

## Getting started

Grab the latest build from the [Releases page](https://github.com/Hexadecinull/KSC-Sharp/releases).
Two kinds of builds are published:

| | Best for |
|---|---|
| **Installer** (`KSC-Sharp-Setup-*.exe`, Windows only) | Everyday use. Installs properly, adds Start Menu shortcuts, registers the link handler, and uninstalls cleanly. |
| **Portable** (`KSC-Sharp-*.zip`) | Testing a specific build, or running on Linux/macOS. Just unzip and run – nothing is installed or registered until you ask it to be, from the Integrations page. |

Requires the [.NET 8 runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if it isn't
already on your system (the installer doesn't currently bundle it).

### Linux & macOS

The Pekora client is a Windows program, so on Linux and macOS it runs under
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

**Integrations** covers everything else: Discord Rich Presence, the `pekora-player://` link
handler (Windows) and desktop integration (Linux), server details lookup, and window
manipulation.

### Discord Rich Presence

Found under Integrations. Off by default. When enabled, KSC-Sharp shows your current activity
on your Discord profile while a client is running, the same way Bloxstrap does for Roblox:

- **Enable Discord Rich Presence** – the master on/off switch (off by default).
- **Discord status display** – "Name" shows just that you're playing Korone; "Details" adds
  which client year. Defaults to Name.
- **Allow activity joining** – lets friends join your game directly from your Discord profile.
  Off by default, and outbound-only for now (see the ⓘ next to it in-app).
- **Show Pekora account** – not yet functional; KSC-Sharp doesn't currently detect which
  account is logged in, and this deliberately isn't faked.

This requires the Discord desktop app to be installed and running.

> **Maintainer note:** Rich Presence needs a real Discord Application ID before it'll do
> anything – `KoroneConfig.DiscordClientId` currently holds a placeholder. Create an app at
> the [Discord Developer Portal](https://discord.com/developers/applications), copy its
> Application ID in, and upload art matching `KoroneConfig.DiscordLargeImageKey` under that
> app's Rich Presence → Art Assets page. Until that's done, the toggle works but Discord just
> won't connect (this fails quietly – it's logged, not a crash).

### Server details lookup

Under Integrations → Activity tracking. Reads the client's own log file for the server you
joined, then looks up its rough location (city/region/country) via a public IP lookup – this
doesn't go through pekora.zip or need a proxy, it's a direct lookup against the server IP
itself. What's unverified: the log line this looks for is Roblox's well-documented format,
which Pekora likely shares since it's Roblox-compatible, but that hasn't been confirmed
against a real Pekora log. If "Check Now" never finds anything, that's the first thing to
check – see `ServerLocator.cs`.

### Window manipulation

Under Integrations. Lets KSC-Sharp look up the running client's window handle – groundwork for
future features (always-on-top, custom title bar, etc.), not a feature in itself yet. Windows
only for now; Linux (X11/Wayland) and macOS (Cocoa) use entirely different windowing APIs that
would need separate implementations.

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
