using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KSCSharp.Core;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KSCSharp.App.Views;

public partial class MainWindow : Window
{
    private readonly FastFlagsManager _flagsManager = new();

    public MainWindow()
    {
        InitializeComponent();

        SubtitleText.Text = $"{KoroneConfig.ProductName} launcher — running on {DescribePlatform()}";

        BuildVersionButtons();

        BtnFastFlags.Click += BtnFastFlags_Click;
        BtnDownloadBootstrapper.Click += BtnDownloadBootstrapper_Click;
        BtnLaunchBootstrapper.Click += BtnLaunchBootstrapper_Click;
        BtnRegisterUri.Click += (_, _) => LogResult(WindowsUriRegistration.Register());
        BtnUnregisterUri.Click += (_, _) => LogResult(WindowsUriRegistration.Unregister());
        BtnSetupLinuxIntegration.Click += BtnSetupLinuxIntegration_Click;
        BtnRemoveLinuxIntegration.Click += BtnRemoveLinuxIntegration_Click;
        BtnClearLog.Click += (_, _) => LogTextBox.Text = string.Empty;
        BtnCopyLog.Click += (_, _) => CopyLogToClipboard();

        WindowsIntegrationPanel.IsVisible = OperatingSystem.IsWindows();
        LinuxIntegrationPanel.IsVisible = OperatingSystem.IsLinux();

        if (SystemInfo.RequiresWine)
        {
            var wine = ProcessLauncher.ResolveWineCommand();
            AppendLog(wine is null
                ? "[!] Wine was not found on PATH. Client versions won't launch until it's installed."
                : $"[*] Using {wine} to run Windows clients.");
        }
    }

    private static string DescribePlatform()
    {
        if (SystemInfo.IsWindows) return "Windows";
        if (SystemInfo.IsLinux) return "Linux";
        if (SystemInfo.IsMacOS) return "macOS";
        return SystemInfo.SystemName;
    }

    private void BuildVersionButtons()
    {
        foreach (var version in KoroneConfig.ClientVersions)
        {
            var button = new Button
            {
                Content = version.Available ? version.DisplayName : $"{version.DisplayName} (WIP)",
                Margin = new Avalonia.Thickness(0, 0, 8, 0),
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
                return;
            }

            ProcessLauncher.Launch(exePath, new[] { "--app" });
            AppendLog($"[+] Launched: {exePath}");
        }
        catch (ProcessLaunchException ex)
        {
            AppendLog($"[!] {ex.Message}");
        }
        catch (Exception ex)
        {
            AppendLog($"[!] Launch failed: {ex.Message}");
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

        AppendLog(ok ? "[*] Download finished." : "[!] Download failed.");
        SetProgress(0);
    }

    private void BtnLaunchBootstrapper_Click(object? sender, RoutedEventArgs e)
    {
        var path = KoroneConfig.BootstrapperFileName;
        if (!System.IO.File.Exists(path))
        {
            AppendLog($"[!] {path} not found — download it first.");
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

    private async void BtnSetupLinuxIntegration_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "KSC-Sharp.App";
            LinuxIntegration.CreateDesktopEntry(exePath);
            AppendLog("[*] Desktop entry created.");

            await LinuxIntegration.DownloadIconAsync();
            AppendLog("[*] Icon installed.");

            LinuxIntegration.RegisterMimeHandler();
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
