using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KSCSharp.App.Views;

namespace KSCSharp.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.PendingUri is { } uri
                ? new LoadingWindow(uri)
                : new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
