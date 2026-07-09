namespace KSCSharp.Core.Models;

/// <summary>A selectable legacy client build, e.g. "2020" -> folder "2020L".</summary>
/// <param name="DisplayName">Label shown in the UI, e.g. "2020".</param>
/// <param name="FolderName">Year-folder name on disk, e.g. "2020L".</param>
/// <param name="Available">Whether koroneStrap currently supports launching this build.</param>
public record ClientVersion(string DisplayName, string FolderName, bool Available);
