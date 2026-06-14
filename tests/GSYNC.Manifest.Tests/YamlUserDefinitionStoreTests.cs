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

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
