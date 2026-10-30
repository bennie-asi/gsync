using GSYNC.Core.Models;
using GSYNC.Providers.Options;
using GSYNC.Providers.Services;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class SteamDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-steam-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Scan_UsesAllLibraries_AndExactManifestIdsInsteadOfDirectorySubstrings()
    {
        var primary = Path.Combine(_root, "Steam");
        var secondary = Path.Combine(_root, "Other Library");
        Directory.CreateDirectory(Path.Combine(primary, "steamapps/common/Game"));
        Directory.CreateDirectory(Path.Combine(primary, "steamapps/common/Game 2"));
        Directory.CreateDirectory(Path.Combine(secondary, "steamapps/common/Third"));
        await File.WriteAllTextAsync(Path.Combine(primary, "steamapps/libraryfolders.vdf"),
            "\"libraryfolders\" { \"0\" { \"path\" \"" + secondary.Replace("\\", "\\\\") + "\" } }");
        // Deliberately put the longer folder name first. The old substring search assigned its ID to Game.
        await File.WriteAllTextAsync(Path.Combine(primary, "steamapps/appmanifest_10.acf"), "\"AppState\" { \"appid\" \"10\" \"installdir\" \"Game 2\" \"name\" \"Game Two\" }");
        await File.WriteAllTextAsync(Path.Combine(primary, "steamapps/appmanifest_20.acf"), "\"AppState\" { \"appid\" \"20\" \"installdir\" \"Game\" \"name\" \"Game One\" }");
        await File.WriteAllTextAsync(Path.Combine(secondary, "steamapps/appmanifest_30.acf"), "\"AppState\" { \"appid\" \"30\" \"installdir\" \"Third\" \"name\" \"Third Game\" }");
        var provider = new SteamSourceProvider(Microsoft.Extensions.Options.Options.Create(new SteamOptions { SteamRootPath = primary }));
        var games = await provider.ScanAsync(CancellationToken.None);
        Assert.Equal(3, games.Count);
        Assert.Equal("20", games.Single(game => game.DisplayName == "Game One").PlatformGameId);
        var third = games.Single(game => game.GameId == "steam-30");
        var variables = provider.ResolveVariables(new GameInstance { GameId = third.GameId, DisplayName = third.DisplayName,
            SourceProviderId = "steam", InstallDirectory = third.InstallDirectory, PlatformInstanceId = third.PlatformGameId, Variables = third.Variables });
        Assert.Equal(secondary, variables["STEAM_LIBRARY"]);
        Assert.Equal(primary, variables["STEAM_ROOT"]);
        Assert.Equal("30", variables["STORE_GAME_ID"]);
        Assert.Throws<InvalidDataException>(() => provider.ResolveVariables(new GameInstance
        {
            GameId = "legacy", DisplayName = "Game One", SourceProviderId = "steam", PlatformInstanceId = "10",
            InstallDirectory = Path.Combine(primary, "steamapps/common/Game"),
        }));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
