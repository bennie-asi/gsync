using System.Text.Json;
using GSYNC.Core.Models;
using GSYNC.Providers.Options;
using GSYNC.Providers.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class EpicDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-epic-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Scan_UsesInstalledManifests_SkipsInvalidEntries_AndFallsBackFromBlankCatalogId()
    {
        var manifests = Path.Combine(_root, "manifests");
        var install = Path.Combine(_root, "installed");
        Directory.CreateDirectory(manifests);
        Directory.CreateDirectory(install);
        await File.WriteAllTextAsync(Path.Combine(manifests, "one.item"), JsonSerializer.Serialize(new
        {
            DisplayName = "First Game", InstallLocation = install, CatalogItemId = "CATALOG-ID", AppName = "app-one",
        }));
        await File.WriteAllTextAsync(Path.Combine(manifests, "two.item"), JsonSerializer.Serialize(new
        {
            DisplayName = "Second Game", InstallLocation = install, CatalogItemId = " ", AppName = "App-Two",
        }));
        await File.WriteAllTextAsync(Path.Combine(manifests, "missing.item"), JsonSerializer.Serialize(new
        {
            DisplayName = "Missing Game", InstallLocation = Path.Combine(_root, "missing"), CatalogItemId = "missing",
        }));
        await File.WriteAllTextAsync(Path.Combine(manifests, "bad.item"), "{ broken json");
        var provider = new EpicSourceProvider(Options.Create(new EpicOptions { ManifestsDirectory = manifests }));

        var games = await provider.ScanAsync(CancellationToken.None);
        Assert.Equal(2, games.Count);
        Assert.Equal("CATALOG-ID", Assert.Single(games, game => game.GameId == "epic-catalog-id").PlatformGameId);
        var second = Assert.Single(games, game => game.GameId == "epic-app-two");
        Assert.Equal("App-Two", second.PlatformGameId);
        var variables = provider.ResolveVariables(new GameInstance { GameId = second.GameId, DisplayName = second.DisplayName,
            SourceProviderId = "epic", InstallDirectory = second.InstallDirectory, PlatformInstanceId = second.PlatformGameId });
        Assert.Equal(install, variables["GAME_INSTALL_DIR"]);
        Assert.Equal("App-Two", variables["STORE_GAME_ID"]);
    }

    [Fact]
    public async Task Scan_MissingManifestFolder_ReturnsNoGames()
    {
        var provider = new EpicSourceProvider(Options.Create(new EpicOptions { ManifestsDirectory = Path.Combine(_root, "missing") }));
        Assert.Empty(await provider.ScanAsync(CancellationToken.None));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
