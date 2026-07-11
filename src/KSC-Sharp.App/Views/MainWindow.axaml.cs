using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KSCSharp.Core;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KSCSharp.App.Views;

public partial class MainWindow : Window
{
    private readonly FastFlagsManager _flagsManager = new();
    private Button[] _navButtons = Array.Empty<Button>();
    private Control[] _pages = Array.Empty<Control>();

    public MainWindow()
    {
        InitializeComponent();

        SubtitleText.Text = $"{KoroneConfig.ProductName} launcher — running on {DescribePlatform()}";

        var version = GetDisplayVersion();
        SidebarVersionText.Text = version;
        AboutVersionText.Text = $"Version {version}";

        BuildVersionButtons();
        WireNav();

        BtnFastFlags.Click += BtnFastFlags_Click;
        BtnDownloadBootstrapper.Click += BtnDownloadBootstrapper_Click;
        BtnLaunchBootstrapper.Click += BtnLaunchBootstrapper_Click;
        BtnRegisterUri.Click += (_, _) => LogResult(WindowsUriRegistration.Register());
        BtnUnregisterUri.Click += (_, _) => LogResult(WindowsUriRegistration.Unregister());
        BtnSetupLinuxIntegration.Click += BtnSetupLinuxIntegration_Click;
        BtnRemoveLinuxIntegration.Click += BtnRemoveLinuxIntegration_Click;
        BtnClearLog.Click += (_, _) => LogTextBox.Text = string.Empty;
        BtnCopyLog.Click += (_, _) => CopyLogToClipboard();
        BtnOpenAppData.Click += (_, _) => OpenPath(KoroneConfig.AppDataDirectory);

        WindowsIntegrationPanel.IsVisible = OperatingSystem.IsWindows();
        LinuxIntegrationPanel.IsVisible = OperatingSystem.IsLinux();

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
        _navButtons = new[] { NavLaunch, NavIntegrations, NavLog, NavAbout };
        _pages = new Control[] { LaunchPage, IntegrationsPage, LogPage, AboutPage };

        NavLaunch.Click += (_, _) => ShowPage(0);
        NavIntegrations.Click += (_, _) => ShowPage(1);
        NavLog.Click += (_, _) => ShowPage(2);
        NavAbout.Click += (_, _) => ShowPage(3);
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

    // ----- Launch page -----

    private void BuildVersionButtons()
    {
        foreach (var version in KoroneConfig.ClientVersions)
        {
            var label = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            label.Children.Add(new TextBlock
            {
                Text = version.DisplayName,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            if (version.Experimental)
            {
                label.Children.Add(new TextBlock
                {
                    Text = "EXPERIMENTAL",
                    Classes = { "badge" },
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            var button = new Button
            {
                Content = label,
                Classes = { "versiontile" },
                Margin = new Avalonia.Thickness(0, 0, 10, 10),
                Width = 120,
                IsEnabled = version.Available,
            };

            if (version.Available)
                button.Click += (_, _) => LaunchVersion(version);

            VersionButtonsPanel.Children.Add(button);
        }
    }

    private void LaunchVersion(ClientVersion version)
    {
        try
        {
            var flags = _flagsManager.Load();
            if (flags.Count > 0)
            {
                AppendLog($"[*] Applying {flags.Count} FastFlag(s)...");
                var result = _flagsManager.ApplyToInstalledClients(flags);
                AppendLog($"[*] FastFlags applied to {result.TargetsWritten} install(s).");
                foreach (var failure in result.Failures)
                    AppendLog($"[!] {failure}");
            }

            AppendLog($"Launching {version.DisplayName} ({version.FolderName})...");
            var exePath = VersionLocator.FindExecutable(version.FolderName);
            if (exePath is null)
            {
                AppendLog("[-] Executable not found. Searched:");
                foreach (var p in VersionLocator.GetExecutablePaths(version.FolderName))
                    AppendLog($"    - {p}");
                ShowPage(2);
                return;
            }

            ProcessLauncher.Launch(exePath, new[] { "--app" });
            AppendLog($"[+] Launched: {exePath}");
        }
        catch (ProcessLaunchException ex)
        {
            AppendLog($"[!] {ex.Message}");
            ShowPage(2);
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Launch failed: {ex.Message}");
            ShowPage(2);
        }
    }

    private async void BtnFastFlags_Click(object? sender, RoutedEventArgs e)
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
            var applyResult = _flagsManager.ApplyToInstalledClients(result.Flags);
            AppendLog($"[*] Applied to {applyResult.TargetsWritten} install(s).");
            foreach (var failure in applyResult.Failures)
                AppendLog($"[!] {failure}");
        }
    }

    private async void BtnDownloadBootstrapper_Click(object? sender, RoutedEventArgs e)
    {
        AppendLog("Starting bootstrapper download...");
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
        SetProgress(0);
    }

    private void BtnLaunchBootstrapper_Click(object? sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(KoroneConfig.AppDataDirectory, KoroneConfig.BootstrapperFileName);
        if (!System.IO.File.Exists(path))
        {
            AppendLog($"[!] {KoroneConfig.BootstrapperFileName} not found — download it first.");
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

    // ----- Integrations page -----

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

    // ----- About page -----

    private void OpenBloxstrap_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/bloxstraplabs/bloxstrap");

    private void OpenKoroneStrap_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/LittleBigDevs/koroneStrap");

    private void OpenKoroneBootstrapper_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/KoroneX/Korone-Bootstrapper");

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
