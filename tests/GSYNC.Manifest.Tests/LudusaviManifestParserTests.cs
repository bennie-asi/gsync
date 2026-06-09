using GSYNC.Manifest.Ludusavi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSYNC.Manifest.Tests;

public sealed class LudusaviManifestParserTests
{
    [Fact]
    public void Parse_MapsKnownVariables_AndSkipsUnknownOnes()
    {
        const string yaml = """
            game-a:
              files:
                '<winAppData>/Studio/GameA/Saves': {}
                '<xdgData>/game-a': {}
              installDir: '<base>/GameA'
              steam:
                id: 1234
            """;

        var parser = new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance);

        var definitions = parser.Parse(yaml);

        var definition = Assert.Single(definitions);
        var content = Assert.Single(definition.ContentItems);
        Assert.Contains("%APPDATA%/Studio/GameA/Saves", content.PathTemplates);
        Assert.Contains("%GAME_INSTALL_DIR%/GameA", content.PathTemplates);
        Assert.DoesNotContain(content.PathTemplates, path => path.Contains("xdgData", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("1234", definition.SourceHints["steam.id"]);
    }

    [Fact]
    public void TryMapPath_MapsBuiltInVariables()
    {
        var mapped = LudusaviVariableMapper.TryMapPath("<winDocuments>/My Games/Game", out var path);

        Assert.True(mapped);
        Assert.Equal("%DOCUMENTS%/My Games/Game", path);
    }
}
