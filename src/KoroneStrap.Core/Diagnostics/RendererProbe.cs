namespace KSCSharp.Core.Diagnostics;

public record RendererProbeResult(bool AnyEvidenceFound, IReadOnlyList<string> MatchingLines, string Summary);

/// <summary>
/// Scans a freshly-launched client's log for lines that plausibly indicate which graphics API
/// actually initialized.
///
/// Unlike GameJoinWatcher's [FLog::GameJoinLoadTime] parsing (confirmed against a real captured
/// Roblox client log), no exact, confirmed log line format for renderer selection was found -
/// only that the underlying GraphicsMode feature and FastFlag names are real and independently
/// documented in multiple places. Rather than pretend to parse a structured field that isn't
/// actually confirmed, this surfaces the raw matching lines verbatim so they can be verified by
/// eye. If a real sample log showing exactly how Korone (or the Roblox engine it's based on)
/// logs its renderer choice becomes available, this should be replaced with a proper structured
/// parser the same way GameJoinWatcher was built once a real example was found.
/// </summary>
public static class RendererProbe
{
    private static readonly string[] Keywords =
    {
        "Direct3D11", "Direct3D 11", "D3D11", "OpenGL", "Vulkan", "GraphicsMode", "RenderDevice", "GraphicsCreateDevice",
    };

    /// <summary>
    /// Reads the most recent log file and returns every line (from the tail end, most relevant
    /// to a just-completed launch) that mentions a renderer-related keyword.
    /// </summary>
    public static RendererProbeResult Probe(int maxLinesToScan = 4000)
    {
        var logPath = ServerLocator.FindLatestLogFile();
        if (logPath is null)
            return new RendererProbeResult(false, Array.Empty<string>(), "No client log file found - launch a client first.");

        List<string> matches;
        try
        {
            var allLines = File.ReadLines(logPath).ToList();
            var recent = allLines.Count > maxLinesToScan ? allLines.Skip(allLines.Count - maxLinesToScan) : allLines;
            matches = recent.Where(line => Keywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        catch (Exception ex)
        {
            return new RendererProbeResult(false, Array.Empty<string>(), $"Couldn't read the log: {ex.Message}");
        }

        if (matches.Count == 0)
        {
            return new RendererProbeResult(false, Array.Empty<string>(),
                "No renderer-related lines found in the recent log. Either the client hasn't logged its renderer choice, or it uses different wording than expected - this is genuinely unconfirmed territory, see the code comment.");
        }

        return new RendererProbeResult(true, matches, $"Found {matches.Count} line(s) mentioning a graphics API. Read them yourself to confirm which renderer actually loaded - this tool surfaces evidence, it doesn't claim to interpret it definitively.");
    }
}
