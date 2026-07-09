using KSCSharp.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

public class FastFlagsManagerTests : IDisposable
{
    private readonly string _tmpFile = Path.GetTempFileName();

    [Fact]
    public void Save_And_Load_ReturnsSame()
    {
        var manager = new FastFlagsManager(_tmpFile);
        var data = new Dictionary<string, object>
        {
            ["FlagA"] = true,
            ["IntVal"] = 42,
            ["FloatVal"] = 3.14,
        };
        manager.Save(data);

        var loaded = manager.Load();
        Assert.Equal(true, loaded["FlagA"]);
        Assert.Equal(42L, loaded["IntVal"]);
        Assert.Equal(3.14, (double)loaded["FloatVal"], precision: 5);
    }

    [Fact]
    public void Load_MissingFile_CreatesEmptyCache()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ksc-sharp-test-{Guid.NewGuid():N}.json");
        try
        {
            var manager = new FastFlagsManager(path);
            var loaded = manager.Load();

            Assert.Empty(loaded);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("False", false)]
    [InlineData("144", 144)]
    [InlineData("3.5", 3.5)]
    [InlineData("hello", "hello")]
    public void AutoDetectValue_InfersExpectedType(string input, object expected)
    {
        var result = FastFlagsManager.AutoDetectValue(input);
        Assert.Equal(expected, result);
    }

    public void Dispose()
    {
        try { File.Delete(_tmpFile); } catch { }
        try { File.Delete(_tmpFile + ".bak"); } catch { }
    }
}
