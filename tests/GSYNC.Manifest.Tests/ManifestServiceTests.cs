using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSYNC.Manifest.Tests;

public sealed class ManifestServiceTests
{
    [Fact]
    public async Task LegacyTitleOverride_AppliesOnlyToItsExactCanonicalGame_AndLegacyLookupsStillWork()
    {
        var parser = new GSYNC.Manifest.Ludusavi.LudusaviManifestParser(NullLogger<GSYNC.Manifest.Ludusavi.LudusaviManifestParser>.Instance);
        var community = parser.Parse("AFTERLIFE:\n  files: {}\nAfterlife:\n  files: {}\n");
        var legacy = new GameContentDefinition { GameId = "AFTERLIFE", DisplayName = "User Recipe" };
        var service = new ManifestService(new StubUserDefinitionStore(legacy), new StubOverrideStore(),
            new StubCommunityStore(community.ToArray()), parser, new StubUpdateSource(null), NullLogger<ManifestService>.Instance);
        var definitions = await service.GetDefinitionsAsync(CancellationToken.None);
        Assert.Equal(2, definitions.Count);
        Assert.Equal("User Recipe", definitions.Single(item => item.GameId == community[0].GameId).DisplayName);
        Assert.Equal("Afterlife", definitions.Single(item => item.GameId == community[1].GameId).DisplayName);
        Assert.Equal("User Recipe", (await service.GetDefinitionAsync(community[0].GameId, CancellationToken.None))!.DisplayName);
        Assert.Equal("Afterlife", (await service.GetDefinitionAsync(community[1].GameId, CancellationToken.None))!.DisplayName);
        Assert.Equal("AFTERLIFE", (await service.GetDefinitionAsync("AFTERLIFE", CancellationToken.None))!.GameId);
        Assert.Equal("Afterlife", (await service.GetDefinitionAsync("Afterlife", CancellationToken.None))!.DisplayName);
    }

    [Fact]
    public async Task OldCacheWithCaseCollisions_RemainsUsableOffline_WithoutMergingGames()
    {
        var first = new GameContentDefinition { GameId = "AFTERLIFE", DisplayName = "AFTERLIFE" };
        var second = new GameContentDefinition { GameId = "Afterlife", DisplayName = "Afterlife" };
        var user = new GameContentDefinition { GameId = "AFTERLIFE", DisplayName = "User" };
        var service = new ManifestService(new StubUserDefinitionStore(user), new StubOverrideStore(), new StubCommunityStore(first, second),
            new StubParser(Array.Empty<GameContentDefinition>()), new StubUpdateSource(null), NullLogger<ManifestService>.Instance);
        var definitions = await service.GetDefinitionsAsync(CancellationToken.None);
        Assert.Equal(2, definitions.Count);
        Assert.Equal(2, definitions.Select(item => item.GameId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("User", (await service.GetDefinitionAsync(GSYNC.Core.Utilities.GameIdentity.ForLudusaviTitle("AFTERLIFE"), CancellationToken.None))!.DisplayName);
        Assert.Equal("Afterlife", (await service.GetDefinitionAsync("Afterlife", CancellationToken.None))!.DisplayName);
    }

    [Fact]
    public async Task GetDefinitionsAsync_UsesPriority_UserThenOverrideThenCommunity()
    {
        var community = new GameContentDefinition
        {
            GameId = "game-a",
            DisplayName = "Community",
            ContentItems = Array.Empty<ContentItem>(),
        };

        var overrideDefinition = new GameContentDefinition
        {
            GameId = "game-a",
            DisplayName = "Override",
            ContentItems = Array.Empty<ContentItem>(),
        };

        var user = new GameContentDefinition
        {
            GameId = "game-a",
            DisplayName = "User",
            ContentItems = Array.Empty<ContentItem>(),
        };

        var service = new ManifestService(
            new StubUserDefinitionStore(user),
            new StubOverrideStore(overrideDefinition),
            new StubCommunityStore(community),
            new StubParser(Array.Empty<GameContentDefinition>()),
            new StubUpdateSource(null),
            NullLogger<ManifestService>.Instance);

        var definitions = await service.GetDefinitionsAsync(CancellationToken.None);

        var definition = Assert.Single(definitions);
        Assert.Equal("User", definition.DisplayName);
    }

    [Fact]
    public async Task RefreshCommunityDefinitionsAsync_SavesParsedDefinitions()
    {
        var communityStore = new StubCommunityStore();
        var parsed = new[]
        {
            new GameContentDefinition
            {
                GameId = "game-a",
                DisplayName = "Game A",
                ContentItems = Array.Empty<ContentItem>(),
            },
        };

        var service = new ManifestService(
            new StubUserDefinitionStore(),
            new StubOverrideStore(),
            communityStore,
            new StubParser(parsed),
            new StubUpdateSource("yaml"),
            NullLogger<ManifestService>.Instance);

        await service.RefreshCommunityDefinitionsAsync(CancellationToken.None);

        Assert.Single(communityStore.SavedDefinitions);
        Assert.Equal("game-a", communityStore.SavedDefinitions[0].GameId);
    }

    private sealed class StubUserDefinitionStore : IUserDefinitionStore
    {
        public Task SaveDefinitionAsync(GameContentDefinition definition, CancellationToken cancellationToken, bool overwrite = true) => throw new NotSupportedException();
        private readonly IReadOnlyList<GameContentDefinition> _definitions;

        public StubUserDefinitionStore(params GameContentDefinition[] definitions)
        {
            _definitions = definitions;
        }

        public Task<IReadOnlyList<GameContentDefinition>> LoadDefinitionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(_definitions);
    }

    private sealed class StubOverrideStore : IDefinitionOverrideStore
    {
        private readonly IReadOnlyList<GameContentDefinition> _definitions;

        public StubOverrideStore(params GameContentDefinition[] definitions)
        {
            _definitions = definitions;
        }

        public Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(_definitions);

        public Task SaveDefinitionsAsync(IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class StubCommunityStore : ICommunityDefinitionStore
    {
        private readonly IReadOnlyList<GameContentDefinition> _definitions;

        public StubCommunityStore(params GameContentDefinition[] definitions)
        {
            _definitions = definitions;
        }

        public List<GameContentDefinition> SavedDefinitions { get; } = new();

        public Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(_definitions);

        public Task SaveDefinitionsAsync(IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken)
        {
            SavedDefinitions.Clear();
            SavedDefinitions.AddRange(definitions);
            return Task.CompletedTask;
        }
    }

    private sealed class StubParser : ILudusaviManifestParser
    {
        private readonly IReadOnlyList<GameContentDefinition> _definitions;

        public StubParser(IReadOnlyList<GameContentDefinition> definitions)
        {
            _definitions = definitions;
        }

        public IReadOnlyList<GameContentDefinition> Parse(string yamlContent) => _definitions;
    }

    private sealed class StubUpdateSource : IManifestUpdateSource
    {
        private readonly string? _yaml;

        public StubUpdateSource(string? yaml)
        {
            _yaml = yaml;
        }

        public Task<string?> TryFetchLatestManifestAsync(CancellationToken cancellationToken)
            => Task.FromResult(_yaml);
    }
}
