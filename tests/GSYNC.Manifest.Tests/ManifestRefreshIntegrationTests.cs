using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models;
using GSYNC.Core.Services;
using GSYNC.Core.Utilities;
using GSYNC.Data.Repositories;
using GSYNC.Data.Services;
using GSYNC.Manifest.Ludusavi;
using GSYNC.Manifest.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSYNC.Manifest.Tests;

public sealed class ManifestRefreshIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-refresh-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_PinsTheEffectiveRecipeOfExistingProfiles_BeforeReplacingCommunityPaths(bool legacyId)
    {
        var paths = new Paths(_root);
        var factory = new SqliteConnectionFactory($"Data Source={paths.GetDatabasePath()};Pooling=False");
        await new DatabaseInitializer(factory, paths).InitializeAsync(CancellationToken.None);
        var parser = new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance);
        var community = new CommunityDefinitionStore(factory);
        var old = Assert.Single(parser.Parse("Game A:\n  files:\n    '<winAppData>/Original/save': {}\n"));
        await community.SaveDefinitionsAsync(new[] { old }, CancellationToken.None);
        var games = new GameInstanceRepository(factory);
        var id = legacyId ? "Game A" : old.GameId;
        await games.UpsertAsync(new GameInstance { GameId = id, DisplayName = "Game A", SourceProviderId = "custom" }, CancellationToken.None);
        var user = new YamlUserDefinitionStore(paths.GetDefinitionsDirectory(), NullLogger<YamlUserDefinitionStore>.Instance);
        var service = new ManifestService(user, new DefinitionOverrideStore(factory), community, parser,
            new Update("Game A:\n  files:\n    '<winAppData>/Changed/save': {}\n"), NullLogger<ManifestService>.Instance, games);

        await service.RefreshCommunityDefinitionsAsync(CancellationToken.None);

        var pinned = Assert.Single(await user.LoadDefinitionsAsync(CancellationToken.None));
        Assert.Equal(id, pinned.GameId);
        Assert.Contains("%APPDATA%/Original/save", Assert.Single(pinned.ContentItems).PathTemplates);
        Assert.Contains("%APPDATA%/Changed/save", Assert.Single((await community.GetDefinitionAsync(old.GameId, CancellationToken.None))!.ContentItems).PathTemplates);
        Assert.Contains("%APPDATA%/Original/save", Assert.Single((await service.GetDefinitionAsync(id, CancellationToken.None))!.ContentItems).PathTemplates);
        Assert.Contains("%APPDATA%/Original/save", Assert.Single(Assert.Single(await service.GetDefinitionsAsync(CancellationToken.None)).ContentItems).PathTemplates);
        Assert.Equal(id, Assert.Single(await games.ListAsync(CancellationToken.None)).GameId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[invalid")]
    public async Task EmptyOrInvalidRefresh_PreservesTheExistingCache(string yaml)
    {
        var paths = new Paths(_root);
        var factory = new SqliteConnectionFactory($"Data Source={paths.GetDatabasePath()};Pooling=False");
        await new DatabaseInitializer(factory, paths).InitializeAsync(CancellationToken.None);
        var community = new CommunityDefinitionStore(factory);
        var parser = new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance);
        await community.SaveDefinitionsAsync(parser.Parse("Original: {}"), CancellationToken.None);
        var service = new ManifestService(new YamlUserDefinitionStore(paths.GetDefinitionsDirectory(), NullLogger<YamlUserDefinitionStore>.Instance),
            new DefinitionOverrideStore(factory), community, parser, new Update(yaml), NullLogger<ManifestService>.Instance);
        await Assert.ThrowsAnyAsync<Exception>(() => service.RefreshCommunityDefinitionsAsync(CancellationToken.None));
        Assert.Equal("Original", Assert.Single(await community.GetDefinitionsAsync(CancellationToken.None)).DisplayName);
        Assert.NotNull(await community.GetDefinitionAsync(GameIdentity.ForLudusaviTitle("Original"), CancellationToken.None));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class Update(string yaml) : IManifestUpdateSource
    {
        public Task<string?> TryFetchLatestManifestAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(yaml);
    }
    private sealed class Paths(string root) : IAppPathService
    {
        public string GetAppDataRoot() => root;
        public string GetDatabasePath() => Path.Combine(root, "data.db");
        public string GetDefinitionsDirectory() => Path.Combine(root, "definitions");
        public string GetLogsDirectory() => Path.Combine(root, "logs");
        public string GetManifestCachePath() => Path.Combine(root, "manifest.yaml");
        public string GetSnapshotsDirectory() => Path.Combine(root, "snapshots");
        public string GetUiSettingsPath() => Path.Combine(root, "ui.json");
    }
}
