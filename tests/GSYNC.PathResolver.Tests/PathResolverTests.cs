using GSYNC.Core.Utilities;
using Xunit;

namespace GSYNC.Core.PathResolver.Tests;

public sealed class PathResolverTests
{
    private readonly GSYNC.Core.Utilities.PathResolver _resolver = new();

    [Fact]
    public void Resolve_ReplacesKnownVariables()
    {
        var result = _resolver.Resolve("%HOME%/SavedGames", new Dictionary<string, string>
        {
            ["HOME"] = "C:/Users/Test",
        });

        Assert.Equal("C:/Users/Test/SavedGames", result);
    }

    [Fact]
    public void Resolve_LeavesUnknownVariablesUntouched()
    {
        var result = _resolver.Resolve("%UNKNOWN%/Saves", new Dictionary<string, string>());

        Assert.Equal("%UNKNOWN%/Saves", result);
    }

    [Fact]
    public void Resolve_UsesHigherPriorityScopesLast()
    {
        var system = new Dictionary<string, string>
        {
            ["APPDATA"] = "C:/Users/System/AppData/Roaming",
        };

        var source = new Dictionary<string, string>
        {
            ["APPDATA"] = "D:/Source/AppData",
        };

        var instance = new Dictionary<string, string>
        {
            ["APPDATA"] = "E:/Instance/AppData",
        };

        var userOverride = new Dictionary<string, string>
        {
            ["APPDATA"] = "F:/Override/AppData",
        };

        var result = _resolver.Resolve("%APPDATA%/Game", system, source, instance, userOverride);

        Assert.Equal("F:/Override/AppData/Game", result);
    }

    [Fact]
    public void Resolve_ReturnsEmptyStringForEmptyTemplate()
    {
        var result = _resolver.Resolve(string.Empty, new Dictionary<string, string>());

        Assert.Equal(string.Empty, result);
    }
}
