using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using KSCSharp.Core;
using System;
using System.Threading.Tasks;

namespace KSCSharp.App.Views;

public partial class LoadingWindow : Window
{
    private readonly string _rawUri;

    public LoadingWindow(string rawUri)
    {
        InitializeComponent();
        _rawUri = rawUri;

        BtnClose.Click += (_, _) => CloseAndShutdown();

        Opened += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var settings = AppSettings.Load();

        var outcome = await UriLaunchRunner.RunAsync(_rawUri, settings, status =>
        {
            Dispatcher.UIThread.Post(() => StatusText.Text = status);
        });

        if (outcome.Success)
        {
            StatusText.Text = outcome.Message;
            await Task.Delay(700);
            CloseAndShutdown();
        }
        else
        {
            StatusText.Text = outcome.Message;
            LoadingProgress.IsIndeterminate = false;
            LoadingProgress.Value = 0;
            BtnClose.IsVisible = true;
        }
    }

    private void CloseAndShutdown()
    {
        Close();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
