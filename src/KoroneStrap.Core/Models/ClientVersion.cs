namespace KSCSharp.Core.Models;

/// <summary>A selectable legacy client build, e.g. "2020" -> folder "2020L".</summary>
/// <param name="DisplayName">Label shown in the UI, e.g. "2020".</param>
/// <param name="FolderName">Year-folder name on disk, e.g. "2020L".</param>
/// <param name="Available">Whether koroneStrap currently supports launching this build.</param>
/// <param name="Experimental">
/// True for builds that are launchable through the same generic path as everything else,
/// but that upstream koroneStrap hasn't marked as fully verified (older client, more likely
/// to hit its own rendering/compatibility quirks - especially under Wine). Shown with a
/// badge in the UI rather than hidden.
/// </param>
public record ClientVersion(string DisplayName, string FolderName, bool Available, bool Experimental = false);
