using KSCSharp.Core;
using KSCSharp.Core.Platform;

namespace KSCSharp.App;

public record UriLaunchOutcome(bool Success, string Message);

public static class UriLaunchRunner
{
    public static async Task<UriLaunchOutcome> RunAsync(string rawUri, AppSettings settings, Action<string> onStatus)
    {
        onStatus("Reading link...");
        var cleaned = KoroneUriParser.StripScheme(rawUri);
        var parsed = KoroneUriParser.Parse(cleaned);

        if (parsed.UserId is not null)
        {
            settings.LastKnownUserId = parsed.UserId;
            settings.Save();
        }

        onStatus($"Preparing the {parsed.Year} client...");
        var flagsManager = new FastFlagsManager();
        var flags = flagsManager.BuildEffectiveFlags(settings);
        var applyResult = await Task.Run(() => flagsManager.ApplyToInstalledClients(flags));

        onStatus("Looking for the client...");
        var exePath = VersionLocator.FindExecutable(parsed.Year);
        if (exePath is null)
        {
            return new UriLaunchOutcome(false,
                $"Couldn't find an installed {parsed.Year} client. Try running the bootstrapper first.");
        }

        onStatus("Starting Korone...");
        try
        {
            await Task.Run(() => ProcessLauncher.Launch(exePath, parsed.Args));
            return new UriLaunchOutcome(true, "Launched!");
        }
        catch (ProcessLaunchException ex)
        {
            return new UriLaunchOutcome(false, ex.Message);
        }
        catch (Exception ex)
        {
            return new UriLaunchOutcome(false, $"Launch failed: {ex.Message}");
        }
    }
}
