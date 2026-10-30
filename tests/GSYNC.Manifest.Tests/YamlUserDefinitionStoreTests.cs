using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Manifest.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSYNC.Manifest.Tests;

public sealed class YamlUserDefinitionStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "gsync-userdef-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadDefinitionsAsync_ReadsWizardWrittenDefinition_WithInterfaceCollections()
    {
        Directory.CreateDirectory(_directory);

        // Mirrors the exact shape AddGameWizardViewModel.WriteUserDefinition produces.
        var yaml =
            "gameId: \"custom-elden-ring\"\r\n" +
            "displayName: \"Elden Ring\"\r\n" +
            "description: \"User-created content definition.\"\r\n" +
            "contentItems:\r\n" +
            "  - contentId: \"saves\"\r\n" +
            "    category: Save\r\n" +
            "    defaultEnabled: true\r\n" +
            "    pathTemplates:\r\n" +
            "      - \"C:/Users/Player/AppData/Roaming/EldenRing/76561197960271872\"\r\n";
        await File.WriteAllTextAsync(Path.Combine(_directory, "custom-elden-ring.yaml"), yaml);

        var store = new YamlUserDefinitionStore(_directory, NullLogger<YamlUserDefinitionStore>.Instance);

        var definitions = await store.LoadDefinitionsAsync(CancellationToken.None);

        var definition = Assert.Single(definitions);
        Assert.Equal("custom-elden-ring", definition.GameId);
        var item = Assert.Single(definition.ContentItems);
        Assert.Equal("saves", item.ContentId);
        Assert.Equal(ContentCategory.Save, item.Category);
        Assert.True(item.DefaultEnabled);
        Assert.Equal(
            "C:/Users/Player/AppData/Roaming/EldenRing/76561197960271872",
            Assert.Single(item.PathTemplates));
    }

    [Fact]
    public async Task Load_RejectsDuplicateKeys_InsteadOfSilentlyChangingGameIdentity()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "duplicate.yaml");
        await File.WriteAllTextAsync(file, "gameId: first\ngameId: second\ndisplayName: Game\n");
        var store = new YamlUserDefinitionStore(_directory, NullLogger<YamlUserDefinitionStore>.Instance);
        await Assert.ThrowsAsync<YamlDotNet.Core.YamlException>(() => store.LoadDefinitionsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# temporarily cleared while editing")]
    public async Task Load_RejectsEmptyOverrides_InsteadOfFallingBackToCommunityPaths(string yaml)
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "empty.yaml");
        await File.WriteAllTextAsync(file, yaml);
        var store = new YamlUserDefinitionStore(_directory, NullLogger<YamlUserDefinitionStore>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadDefinitionsAsync(CancellationToken.None));
        Assert.Equal(yaml, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Save_RoundTripsQuotedNamesBackslashesAndAllContentRules()
    {
        var store = new YamlUserDefinitionStore(_directory, NullLogger<YamlUserDefinitionStore>.Instance);
        var definition = new GameContentDefinition
        {
            GameId = "custom-quoted", DisplayName = "A \"quoted\" game\nsecond line", Description = "Unicode 存档: 'profile'",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "123" },
            ContentItems = new[] { new ContentItem { ContentId = "saves", Category = ContentCategory.Save,
                PathTemplates = new[] { "C:\\Saves\\O'Brien\\profile" }, IncludePatterns = new[] { "**/*.sav" },
                ExcludePatterns = new[] { "cache/**" }, Description = "all slots", DefaultEnabled = true } },
        };
        await store.SaveDefinitionAsync(definition, CancellationToken.None);
        var reloaded = Assert.Single(await store.LoadDefinitionsAsync(CancellationToken.None));
        Assert.Equal(definition.DisplayName, reloaded.DisplayName);
        Assert.Equal(definition.Description, reloaded.Description);
        Assert.Equal("123", reloaded.SourceHints["steam.id"]);
        var item = Assert.Single(reloaded.ContentItems);
        Assert.Equal(definition.ContentItems[0].PathTemplates, item.PathTemplates);
        Assert.Equal(new[] { "**/*.sav" }, item.IncludePatterns);
        Assert.Equal(new[] { "cache/**" }, item.ExcludePatterns);
    }

    [Fact]
    public async Task Save_UpdatesTheExistingLegacyFile_AndCreateOnlyNeverOverwritesIt()
    {
        Directory.CreateDirectory(_directory);
        var legacy = Path.Combine(_directory, "old-name.yaml");
        await File.WriteAllTextAsync(legacy, "gameId: game-a\ndisplayName: Before\ncontentItems: []\n");
        var store = new YamlUserDefinitionStore(_directory, NullLogger<YamlUserDefinitionStore>.Instance);
        var definition = new GameContentDefinition { GameId = "game-a", DisplayName = "Updated", ContentItems = Array.Empty<ContentItem>() };
        await store.SaveDefinitionAsync(definition, CancellationToken.None);
        Assert.Equal(new[] { legacy }, Directory.GetFiles(_directory));
        Assert.Equal("Updated", Assert.Single(await store.LoadDefinitionsAsync(CancellationToken.None)).DisplayName);
        var bytes = await File.ReadAllBytesAsync(legacy);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDefinitionAsync(definition, CancellationToken.None, overwrite: false));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(legacy));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
