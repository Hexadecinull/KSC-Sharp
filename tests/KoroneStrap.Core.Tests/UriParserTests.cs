using KSCSharp.Core;
using Xunit;

public class UriParserTests
{
    [Fact]
    public void Parse_With_LaunchMode_And_ClientVersion()
    {
        var input = "launchmode:Play+placeId:1234+clientversion:2018";
        var parsed = UriParser.Parse(input);
        Assert.Equal("2018", parsed.Year);
        Assert.Contains("--Play", parsed.Args);
        Assert.Contains("-placeId", parsed.Args);
        Assert.Contains("1234", parsed.Args);
    }

    [Fact]
    public void Parse_With_PlaceLauncherUrl_Decode()
    {
        var input = "placelauncherurl:https%3A%2F%2Fexample.com%2Fj";
        var parsed = UriParser.Parse(input);
        Assert.Contains("https://example.com/j", parsed.ArgsString);
    }

    [Fact]
    public void Parse_Defaults_Year_When_ClientVersion_Missing()
    {
        var parsed = UriParser.Parse("placeId:5555");
        Assert.Equal("2017L", parsed.Year);
    }

    [Theory]
    [InlineData("pekora-player://launchmode:Play", "launchmode:Play")]
    [InlineData("pekora-player:launchmode:Play", "launchmode:Play")]
    [InlineData("launchmode:Play", "launchmode:Play")]
    public void StripScheme_RemovesKnownPrefixes(string input, string expected)
    {
        Assert.Equal(expected, UriParser.StripScheme(input));
    }
}
