using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KSCSharp.Core;
using KSCSharp.Core.Diagnostics;
using KSCSharp.Core.Discord;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KSCSharp.App.Views;

public partial class MainWindow : Window
{
    private readonly FastFlagsManager _flagsManager = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly DiscordRpcManager _discord = new();
    private Button[] _navButtons = Array.Empty<Button>();
    private Control[] _pages = Array.Empty<Control>();
    private List<(string Name, int Page)> _searchMatches = new();

    private static readonly (string Name, int Page)[] SearchIndex =
    {
        ("Launch", 0), ("2017", 0), ("2018", 0), ("2020", 0), ("2021", 0),
        ("Bootstrapper", 0), ("Download / Update", 0), ("Run Bootstrapper", 0),

        ("Integrations", 1),
        ("Activity Tracking", 1), ("Enable activity tracking", 1), ("Query server details", 1),
        ("Discord Rich Presence", 1), ("Enable Discord Rich Presence", 1), ("Show game activity", 1),
        ("Discord status display", 1), ("Allow activity joining", 1), ("Show Pekora account", 1),
        ("Window Manipulation", 1), ("Enable window manipulation", 1),
        ("Custom Integrations", 1), ("URI Scheme", 1), ("Windows", 1), ("Linux", 1), ("Wine", 1),

        ("FastFlags", 2), ("Fast Flag Editor", 2), ("Allow KSC-Sharp to manage Fast Flags", 2),
        ("Reset everything to defaults", 2),

        ("Global Settings", 3), ("Presets", 3), ("Rendering and Graphics", 3),
        ("Current Graphics API", 3), ("Framerate Limit", 3),

        ("Log", 4),

        ("About", 5), ("Source Code", 5), ("Report an Issue", 5), ("Credits", 5), ("App Data", 5),
    };

    public MainWindow()
    {
        InitializeComponent();

        SubtitleText.Text = $"{KoroneConfig.ProductName} launcher – running on {DescribePlatform()}";

        var version = GetDisplayVersion();
        SidebarVersionText.Text = version;
        AboutVersionText.Text = $"Version {version}";

        BuildVersionButtons();
        WireNav();
        WireSearch();
        WireActivityTracking();
        WireDiscord();
        WireWindowManipulation();
        WireFastFlagsPage();
        WireGlobalSettings();

        BtnFastFlags.Click += (_, _) => ShowPage(2);
        BtnDownloadBootstrapper.Click += BtnDownloadBootstrapper_Click;
        BtnLaunchBootstrapper.Click += BtnLaunchBootstrapper_Click;
        BtnRegisterUri.Click += (_, _) =>
        {
            var result = WindowsUriRegistration.Register();
            LogResult(result);
            UriSchemeStatusText.Text = result.Success ? "URI Scheme registered." : result.Message;
        };
        BtnUnregisterUri.Click += (_, _) =>
        {
            var result = WindowsUriRegistration.Unregister();
            LogResult(result);
            UriSchemeStatusText.Text = result.Success ? "URI Scheme unregistered." : result.Message;
        };
        BtnSetupLinuxIntegration.Click += BtnSetupLinuxIntegration_Click;
        BtnRemoveLinuxIntegration.Click += BtnRemoveLinuxIntegration_Click;
        BtnClearLog.Click += (_, _) => LogTextBox.Text = string.Empty;
        BtnCopyLog.Click += (_, _) => CopyLogToClipboard();
        BtnOpenAppData.Click += (_, _) => OpenPath(KoroneConfig.AppDataDirectory);

        WindowsIntegrationPanel.IsVisible = OperatingSystem.IsWindows();
        LinuxIntegrationPanel.IsVisible = OperatingSystem.IsLinux();

        RefreshBootstrapperStatus();

        if (SystemInfo.RequiresWine)
        {
            var wine = ProcessLauncher.ResolveWineCommand();
            WineStatusText.Text = wine is null
                ? "Wine was not found. Client versions won't launch until wine64/wine is installed and reachable."
                : $"Using: {wine}";
            AppendLog(wine is null
                ? "[!] Wine was not found. Client versions won't launch until it's installed."
                : $"[*] Using {wine} to run Windows clients.");
        }
        else
        {
            WineStatusText.Text = "Not needed on this platform.";
        }

        Closed += (_, _) =>
        {
            if (_discord.IsConnected)
            {
                _discord.ClearActivity();
                _discord.Disconnect();
            }
        };
    }

    private static string DescribePlatform()
    {
        if (SystemInfo.IsWindows) return "Windows";
        if (SystemInfo.IsLinux) return "Linux";
        if (SystemInfo.IsMacOS) return "macOS";
        return SystemInfo.SystemName;
    }

    private static string GetDisplayVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "dev" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    // ----- Navigation -----

    private void WireNav()
    {
        _navButtons = new[] { NavLaunch, NavIntegrations, NavFastFlags, NavGlobalSettings, NavLog, NavAbout };
        _pages = new Control[] { LaunchPage, IntegrationsPage, FastFlagsPage, GlobalSettingsPage, LogPage, AboutPage };

        NavLaunch.Click += (_, _) => ShowPage(0);
        NavIntegrations.Click += (_, _) => ShowPage(1);
        NavFastFlags.Click += (_, _) => ShowPage(2);
        NavGlobalSettings.Click += (_, _) => ShowPage(3);
        NavLog.Click += (_, _) => ShowPage(4);
        NavAbout.Click += (_, _) => ShowPage(5);
    }

    private void ShowPage(int index)
    {
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].IsVisible = i == index;

            if (i == index)
                _navButtons[i].Classes.Add("selected");
            else
                _navButtons[i].Classes.Remove("selected");
        }
    }

    // ----- Sidebar search -----

    private void WireSearch()
    {
        SearchBox.TextChanged += (_, _) => UpdateSearchResults(SearchBox.Text ?? "");

        SearchResultsList.SelectionChanged += (_, _) =>
        {
            var index = SearchResultsList.SelectedIndex;
            if (index < 0 || index >= _searchMatches.Count)
                return;

            ShowPage(_searchMatches[index].Page);
            SearchBox.Text = string.Empty;
            SearchResultsPanel.IsVisible = false;
        };
    }

    private void UpdateSearchResults(string query)
    {
        query = query.Trim();

        if (query.Length == 0)
        {
            SearchResultsPanel.IsVisible = false;
            _searchMatches = new();
            return;
        }

        _searchMatches = SearchIndex
            .Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        SearchResultsList.ItemsSource = _searchMatches.Select(m => m.Name).ToList();
        SearchResultsPanel.IsVisible = _searchMatches.Count > 0;
    }

    // ----- Launch page -----

    private void BuildVersionButtons()
    {
        foreach (var version in KoroneConfig.ClientVersions)
        {
            var button = new Button
            {
                Content = version.DisplayName,
                Classes = { "versiontile" },
                Margin = new Avalonia.Thickness(0, 0, 10, 10),
                Width = 120,
                IsEnabled = version.Available,
            };

            ToolTip.SetTip(button, $"Launch the {version.DisplayName} client.");

            if (version.Available)
                button.Click += (_, _) => LaunchVersion(version);

            VersionButtonsPanel.Children.Add(button);
        }
    }

    private void LaunchVersion(ClientVersion version)
    {
        try
        {
            var flags = _flagsManager.BuildEffectiveFlags(_settings);
            AppendLog($"[*] Applying {flags.Count} FastFlag(s) (incl. Graphics API / Framerate presets)...");
            var result = _flagsManager.ApplyToInstalledClients(flags);
            AppendLog($"[*] FastFlags applied to {result.TargetsWritten} install(s).");
            foreach (var failure in result.Failures)
                AppendLog($"[!] {failure}");

            AppendLog($"Launching {version.DisplayName} ({version.FolderName})...");
            var exePath = VersionLocator.FindExecutable(version.FolderName);
            if (exePath is null)
            {
                AppendLog("[-] Executable not found. Searched:");
                foreach (var p in VersionLocator.GetExecutablePaths(version.FolderName))
                    AppendLog($"    - {p}");
                ShowPage(4);
                return;
            }

            var process = ProcessLauncher.Launch(exePath, new[] { "--app" });
            AppendLog($"[+] Launched: {exePath}");

            UpdateDiscordActivity(version);
            HookProcessExitForDiscord(process);

            if (_settings.WindowManipulationEnabled && WindowManipulator.IsSupported)
            {
                _ = Task.Run(() =>
                {
                    var handle = WindowManipulator.FindMainWindowHandle(process.Id);
                    AppendLog(handle is null
                        ? "[!] Window manipulation: couldn't find the client's window handle."
                        : $"[*] Window manipulation: found window handle for {version.DisplayName}.");
                });
            }
        }
        catch (ProcessLaunchException ex)
        {
            AppendLog($"[!] {ex.Message}");
            ShowPage(4);
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Launch failed: {ex.Message}");
            ShowPage(4);
        }
    }

    private void HookProcessExitForDiscord(Process process)
    {
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                if (_discord.IsConnected)
                    _discord.ClearActivity();
            };
        }
        catch (Exception)
        {
            // Wine-wrapped processes can behave oddly here on some distros - not fatal,
            // presence just won't auto-clear when the game closes.
        }
    }

    private async void BtnDownloadBootstrapper_Click(object? sender, RoutedEventArgs e)
    {
        AppendLog("Starting bootstrapper download...");
        DownloadProgressRow.IsVisible = true;
        SetProgress(0);

        var downloader = new BootstrapperDownloader();
        var cts = new CancellationTokenSource();
        var progress = new Progress<(long downloaded, long? total)>(p =>
        {
            if (p.total is > 0)
            {
                var percent = (int)Math.Min(100, p.downloaded * 100 / p.total.Value);
                SetProgress(percent);
            }
        });

        var ok = false;
        try
        {
            ok = await downloader.DownloadAsync(progress, cts.Token);
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Download error: {ex.Message}");
        }

        AppendLog(ok ? $"[*] Download finished ({downloader.OutputFile})." : "[!] Download failed.");
        DownloadProgressRow.IsVisible = false;
        RefreshBootstrapperStatus();
    }

    private void BtnLaunchBootstrapper_Click(object? sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(KoroneConfig.AppDataDirectory, KoroneConfig.BootstrapperFileName);
        if (!System.IO.File.Exists(path))
        {
            AppendLog($"[!] {KoroneConfig.BootstrapperFileName} not found – download it first.");
            return;
        }

        try
        {
            ProcessLauncher.Launch(path);
            AppendLog("[*] Bootstrapper launched.");
        }
        catch (ProcessLaunchException ex)
        {
            AppendLog($"[!] {ex.Message}");
        }
    }

    private void RefreshBootstrapperStatus()
    {
        var path = System.IO.Path.Combine(KoroneConfig.AppDataDirectory, KoroneConfig.BootstrapperFileName);
        BootstrapperStatusText.Text = System.IO.File.Exists(path)
            ? $"Downloaded – last updated {System.IO.File.GetLastWriteTime(path):yyyy-MM-dd HH:mm}."
            : "Not downloaded yet.";
    }

    // ----- Integrations: Activity tracking -----

    private void WireActivityTracking()
    {
        ToggleActivityTracking.IsChecked = _settings.ActivityTrackingEnabled;
        ToggleQueryServerDetails.IsChecked = _settings.QueryServerDetailsEnabled;
        ApplyActivityTrackingGate();

        ToggleActivityTracking.Click += (_, _) =>
        {
            _settings.ActivityTrackingEnabled = ToggleActivityTracking.IsChecked == true;
            _settings.Save();
            ApplyActivityTrackingGate();
        };

        ToggleQueryServerDetails.Click += (_, _) =>
        {
            _settings.QueryServerDetailsEnabled = ToggleQueryServerDetails.IsChecked == true;
            _settings.Save();
        };

        BtnCheckServer.Click += BtnCheckServer_Click;
    }

    private void ApplyActivityTrackingGate()
    {
        var enabled = _settings.ActivityTrackingEnabled;
        ToggleQueryServerDetails.IsEnabled = enabled;
        BtnCheckServer.IsEnabled = enabled;
        if (!enabled)
            ServerDetailsText.Text = "Enable activity tracking first.";
    }

    private async void BtnCheckServer_Click(object? sender, RoutedEventArgs e)
    {
        if (!_settings.QueryServerDetailsEnabled)
        {
            ServerDetailsText.Text = "Turn on \"Query server details\" first.";
            return;
        }

        ServerDetailsText.Text = "Checking...";
        var location = await ServerLocator.QueryCurrentServerAsync();

        ServerDetailsText.Text = location is null
            ? "Couldn't determine your server (no client log found, or it's not in a game yet)."
            : $"{location.Ip} – {location.City}, {location.Region}, {location.Country} ({location.Isp})";
    }

    // ----- Integrations: Discord Rich Presence -----

    private void WireDiscord()
    {
        ToggleDiscordEnabled.IsChecked = _settings.DiscordEnabled;
        ToggleDiscordShowActivity.IsChecked = _settings.DiscordShowActivity;
        ToggleDiscordJoining.IsChecked = _settings.DiscordAllowJoining;
        ToggleDiscordAccount.IsChecked = _settings.DiscordShowAccount;
        ComboDiscordDisplay.SelectedIndex = _settings.DiscordShowDetails ? 1 : 0;

        if (_settings.DiscordEnabled)
            TryConnectDiscord();
        else
            DiscordStatusText.Text = "Discord Rich Presence is off.";

        ToggleDiscordEnabled.Click += (_, _) =>
        {
            _settings.DiscordEnabled = ToggleDiscordEnabled.IsChecked == true;
            _settings.Save();

            if (_settings.DiscordEnabled)
                TryConnectDiscord();
            else
            {
                _discord.ClearActivity();
                _discord.Disconnect();
                DiscordStatusText.Text = "Discord Rich Presence is off.";
            }
        };

        ToggleDiscordShowActivity.Click += (_, _) =>
        {
            _settings.DiscordShowActivity = ToggleDiscordShowActivity.IsChecked == true;
            _settings.Save();
        };

        ComboDiscordDisplay.SelectionChanged += (_, _) =>
        {
            _settings.DiscordShowDetails = ComboDiscordDisplay.SelectedIndex == 1;
            _settings.Save();
        };

        ToggleDiscordJoining.Click += (_, _) =>
        {
            _settings.DiscordAllowJoining = ToggleDiscordJoining.IsChecked == true;
            _settings.Save();
        };

        ToggleDiscordAccount.Click += (_, _) =>
        {
            _settings.DiscordShowAccount = ToggleDiscordAccount.IsChecked == true;
            _settings.Save();
        };
    }

    private void TryConnectDiscord()
    {
        var ok = _discord.Connect();
        DiscordStatusText.Text = ok
            ? "Connected to Discord."
            : "Couldn't reach Discord (is it running?). Will show activity next time a client launches and it's found.";
        AppendLog(ok ? "[*] Discord Rich Presence connected." : "[!] Discord Rich Presence: couldn't connect.");
    }

    private void UpdateDiscordActivity(ClientVersion version)
    {
        if (!_settings.DiscordEnabled)
            return;

        if (!_discord.IsConnected)
            TryConnectDiscord();

        if (!_discord.IsConnected)
            return;

        if (!_settings.DiscordShowActivity)
        {
            // Stay connected, but don't publish what we're playing.
            _discord.ClearActivity();
            return;
        }

        var activity = new DiscordActivity
        {
            Details = $"Playing {KoroneConfig.ProductName}",
            State = _settings.DiscordShowDetails ? $"{version.DisplayName} client" : null,
            StartTimestamp = DateTimeOffset.UtcNow,
            LargeImageKey = KoroneConfig.DiscordLargeImageKey,
            LargeImageText = KoroneConfig.DiscordLargeImageText,
            PartyId = _settings.DiscordAllowJoining ? Guid.NewGuid().ToString() : null,
            PartySize = _settings.DiscordAllowJoining ? 1 : null,
            PartyMax = _settings.DiscordAllowJoining ? 4 : null,
            JoinSecret = _settings.DiscordAllowJoining ? Guid.NewGuid().ToString("N") : null,
        };

        var ok = _discord.SetActivity(activity);
        AppendLog(ok ? "[*] Discord activity updated." : "[!] Discord activity update failed.");
    }

    // ----- Integrations: Window manipulation -----

    private void WireWindowManipulation()
    {
        ToggleWindowManipulation.IsChecked = _settings.WindowManipulationEnabled;
        ToggleWindowManipulation.Click += (_, _) =>
        {
            _settings.WindowManipulationEnabled = ToggleWindowManipulation.IsChecked == true;
            _settings.Save();

            if (_settings.WindowManipulationEnabled && !WindowManipulator.IsSupported)
                AppendLog("[!] Window manipulation is only implemented on Windows so far.");
        };
    }

    // ----- Integrations: Windows / Linux -----

    private async void BtnSetupLinuxIntegration_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "KSC-Sharp.App";
            LinuxIntegration.CreateDesktopEntry(exePath);
            AppendLog("[*] Desktop entry created.");

            LinuxIntegration.InstallIcon();
            AppendLog("[*] Icon installed.");

            await Task.Run(LinuxIntegration.RegisterMimeHandler);
            AppendLog("[*] MIME handler registered.");
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Linux integration failed: {ex.Message}");
        }
    }

    private void BtnRemoveLinuxIntegration_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            LinuxIntegration.UninstallIntegration();
            AppendLog("[*] Linux integration removed.");
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Failed to remove integration: {ex.Message}");
        }
    }

    // ----- FastFlags page -----

    private void WireFastFlagsPage()
    {
        ToggleFastFlagsManagement.IsChecked = _settings.FastFlagsManagementEnabled;
        ToggleFastFlagsManagement.Click += (_, _) =>
        {
            _settings.FastFlagsManagementEnabled = ToggleFastFlagsManagement.IsChecked == true;
            _settings.Save();
        };

        BtnResetFastFlags.Click += (_, _) =>
        {
            var result = _flagsManager.ResetAll();
            AppendLog($"[*] FastFlags reset. Applied to {result.TargetsWritten} install(s).");
            foreach (var failure in result.Failures)
                AppendLog($"[!] {failure}");
        };
    }

    private async void OpenFastFlagEditor_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new FastFlagsWindow(_flagsManager.Load());
        var result = await dialog.ShowDialog<FastFlagsWindow.Result?>(this);

        if (result is null)
        {
            AppendLog("[*] FastFlags dialog closed without saving.");
            return;
        }

        _flagsManager.Save(result.Flags);
        AppendLog($"[*] FastFlags saved locally ({result.Flags.Count}).");

        if (result.ApplyNow)
        {
            var applyResult = _flagsManager.ApplyToInstalledClients(_flagsManager.BuildEffectiveFlags(_settings));
            AppendLog($"[*] Applied to {applyResult.TargetsWritten} install(s).");
            foreach (var failure in applyResult.Failures)
                AppendLog($"[!] {failure}");
        }
    }

    // ----- Global Settings page -----

    private void WireGlobalSettings()
    {
        ComboGraphicsApi.SelectedIndex = (int)_settings.GraphicsApi;
        NumFramerateLimit.Value = _settings.FramerateLimit;

        ComboGraphicsApi.SelectionChanged += (_, _) =>
        {
            _settings.GraphicsApi = (GraphicsApi)ComboGraphicsApi.SelectedIndex;
            _settings.Save();
        };

        NumFramerateLimit.ValueChanged += (_, _) =>
        {
            _settings.FramerateLimit = (int)(NumFramerateLimit.Value ?? 60);
            _settings.Save();
        };
    }

    // ----- About page -----

    private void OpenBloxstrap_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/bloxstraplabs/bloxstrap");

    private void OpenKoroneStrap_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/LittleBigDevs/koroneStrap");

    private void OpenKoroneBootstrapper_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/KoroneX/Korone-Bootstrapper");

    private void OpenSource_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/Hexadecinull/KSC-Sharp");

    private void OpenIssues_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/Hexadecinull/KSC-Sharp/issues");

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Couldn't open {url}: {ex.Message}");
        }
    }

    private void OpenPath(string path)
    {
        try
        {
            System.IO.Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Couldn't open {path}: {ex.Message}");
        }
    }

    // ----- Shared helpers -----

    private void LogResult((bool Success, string Message) result) =>
        AppendLog(result.Success ? $"[*] {result.Message}" : $"[!] {result.Message}");

    private void SetProgress(int percent)
    {
        DownloadProgress.Value = percent;
        ProgressText.Text = $"{percent}%";
    }

    private void AppendLog(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {text}\n";
            LogTextBox.CaretIndex = LogTextBox.Text.Length;
        });
    }

    private async void CopyLogToClipboard()
    {
        var clipboard = Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(LogTextBox.Text ?? string.Empty);
        AppendLog("[*] Log copied to clipboard.");
    }
}
