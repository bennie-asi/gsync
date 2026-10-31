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

    [Fact]
    public void ManualCatalogSearch_DistinguishesExactNamesFromSuggestionsAndAmbiguity()
    {
        var service = new AddGameMatchService();
        var candidates = service.FindNameCandidates("Ｇａｍｅ  A", new[]
        {
            Definition("one", "Game-A"), Definition("two", "Game A"), Definition("three", "Game A 2"), Definition("four", "Other"),
        });
        Assert.Equal(3, candidates.Count);
        Assert.Equal(2, candidates.Count(candidate => candidate.IsExact));
        Assert.False(candidates.Single(candidate => candidate.Definition.GameId == "three").IsExact);
        Assert.Empty(service.FindNameCandidates("  ", new[] { Definition("one", "Game") }));
    }

    [Fact]
    public void Artwork_UsesValidatedIdsAndDoesNotGuessBetweenDifferentGames()
    {
        var game = new GameInstance { GameId = "custom", DisplayName = "Some Game", SourceProviderId = "custom" };
        var resolverResult = GameArtworkResolver.Resolve(game, null, new[] { Definition("one", "Some Game", "123") });
        Assert.Contains("/123/library_600x900.jpg", resolverResult.CoverUri);
        Assert.Null(GameArtworkResolver.Resolve(game, null, new[] { Definition("one", "Some Game", "123"), Definition("two", "Some Game", "456") }).CoverUri);
        Assert.Null(GameArtworkResolver.Resolve(game, Definition("bad", "Some Game", "../untrusted")).CoverUri);
    }

    [Fact]
    public async Task ProgramArtwork_ReadsEmbeddedIconAndFallsBackForMissingFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        var systemLibrary = Path.Combine(Environment.SystemDirectory, "shell32.dll");
        var icon = await ExecutableIconReader.ReadAsync(systemLibrary);
        Assert.NotNull(icon);
        Assert.True(icon!.Length > 22);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, icon[..8]);
        Assert.Null(await ExecutableIconReader.ReadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")));
    }

    [Fact]
    public void ProgramSelection_ReadsExecutableAndSteamShortcutWithoutLaunchingThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsync-program-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var exe = Path.Combine(root, "My Game.exe");
            File.WriteAllText(exe, "not an executable");
            var selected = GameProgramSelectionReader.Read(exe);
            Assert.Equal("My Game", selected.DisplayName);
            Assert.Equal(root, selected.InstallDirectory);
            Assert.Equal(exe, selected.ExecutablePath);
            if (OperatingSystem.IsWindows())
            {
                var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                dynamic link = ((dynamic)shell).CreateShortcut(Path.Combine(root, "My Shortcut.lnk"));
                link.TargetPath = exe;
                link.Save();
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                var linkSelection = GameProgramSelectionReader.Read(Path.Combine(root, "My Shortcut.lnk"));
                Assert.Equal("My Shortcut", linkSelection.DisplayName);
                Assert.Equal(exe, linkSelection.ExecutablePath);
                Assert.Equal(root, linkSelection.InstallDirectory);
            }
            var shortcut = Path.Combine(root, "Shortcut.url");
            File.WriteAllText(shortcut, "[InternetShortcut]\nURL=steam://rungameid/1245620\n");
            Assert.Equal("1245620", GameProgramSelectionReader.Read(shortcut).SteamId);
            File.WriteAllText(shortcut, "[InternetShortcut]\nURL=https://example.test/not-a-game\n");
            Assert.Null(GameProgramSelectionReader.Read(shortcut).SteamId);
        }
        finally { Directory.Delete(root, true); }
    }

    private static GameContentDefinition Definition(string id, string title, string? hint = null) => new()
    {
        GameId = id, DisplayName = title, SourceHints = hint is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["steam.id"] = hint },
        ContentItems = new[] { new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { "C:/Saves" } } },
    };
}
