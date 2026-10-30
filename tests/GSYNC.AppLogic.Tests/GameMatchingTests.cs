using GSYNC.App.Infrastructure.Wizard;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class GameMatchingTests
{
    [Theory]
    [InlineData("steam-10", "Unrelated 10 second game", "Some Other Game", null)]
    [InlineData("epic-123", "Unrelated", "Other", "123")]
    public void PlatformIds_NeverMatchTitleSubstringsOrOtherProvidersHints(string gameId, string title, string name, string? hint)
    {
        var game = new DiscoveredGame { GameId = gameId, DisplayName = name, SourceProviderId = gameId.StartsWith("steam") ? "steam" : "epic", PlatformGameId = gameId.Split('-')[1] };
        var definition = Definition(title, title, hint);
        var match = new AddGameMatchService().Match(game, new[] { definition }, _ => Definition("fallback", "fallback"));
        Assert.True(match.IsFallback);
    }

    [Fact]
    public void AmbiguousOrContradictoryNameMatches_RequireManualConfiguration()
    {
        var game = new DiscoveredGame { GameId = "steam-222", SourceProviderId = "steam", PlatformGameId = "222", DisplayName = "Game A" };
        var service = new AddGameMatchService();
        Assert.True(service.Match(game, new[] { Definition("one", "Game-A"), Definition("two", "Game A") }, _ => Definition("fallback", "fallback")).IsFallback);
        Assert.True(service.Match(game, new[] { Definition("one", "Game A", "111") }, _ => Definition("fallback", "fallback")).IsFallback);
        Assert.True(service.Match(game, new[] { Definition("one", "Game A", "222"), Definition("two", "Game B", "222") }, _ => Definition("fallback", "fallback")).IsFallback);
    }

    private static GameContentDefinition Definition(string id, string title, string? hint = null) => new()
    {
        GameId = id, DisplayName = title, SourceHints = hint is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["steam.id"] = hint },
        ContentItems = new[] { new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { "C:/Saves" } } },
    };
}
