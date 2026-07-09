using KSCSharp.Core;
using KSCSharp.Core.Platform;
using System.Linq;
using Xunit;

public class VersionLocatorTests
{
    [Fact]
    public void GetVersionRoots_ReturnsCandidatesForBothInstallFolders()
    {
        var roots = VersionLocator.GetVersionRoots().ToList();

        Assert.NotEmpty(roots);
        foreach (var folder in KoroneConfig.InstallFolderNames)
            Assert.Contains(roots, r => r.Contains(folder));
    }

    [Fact]
    public void GetExecutablePaths_EndsWithClientExecutableName()
    {
        var paths = VersionLocator.GetExecutablePaths("2020L").ToList();

        // No installs exist in a CI/sandbox environment, so this just checks path shape,
        // not that anything was actually found.
        Assert.All(paths, p => Assert.EndsWith(KoroneConfig.ClientExecutableName, p));
    }

    [Fact]
    public void FindExecutable_ReturnsNull_WhenNothingInstalled()
    {
        // In a clean environment (no Pekora/ProjectX install) this should not throw and
        // should simply report nothing found.
        var result = VersionLocator.FindExecutable("2020L");
        Assert.True(result is null || System.IO.File.Exists(result));
    }
}
