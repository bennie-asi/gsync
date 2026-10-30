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
        Assert.DoesNotContain("%GAME_INSTALL_DIR%/GameA", content.PathTemplates);
        Assert.DoesNotContain(content.PathTemplates, path => path.Contains("xdgData", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("1234", definition.SourceHints["steam.id"]);
    }

    [Fact]
    public void Parse_TitlesDifferingOnlyInCase_HaveSeparateStableIdsAndStorageNamespaces()
    {
        var parser = new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance);
        const string yaml = "AFTERLIFE:\n  files: {}\nAfterlife:\n  files: {}\n";
        var definitions = parser.Parse(yaml);
        Assert.Equal(2, definitions.Select(item => item.GameId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(2, definitions.Select(item => GSYNC.Core.Utilities.GameIdentity.StorageNamespace(item.GameId)).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(definitions.Select(item => item.GameId), parser.Parse(yaml).Select(item => item.GameId));
    }

    [Fact]
    public void TryMapPath_MapsBuiltInVariables()
    {
        var mapped = LudusaviVariableMapper.TryMapPath("<winDocuments>/My Games/Game", out var path);

        Assert.True(mapped);
        Assert.Equal("%DOCUMENTS%/My Games/Game", path);
    }

    [Fact]
    public void Parse_OfficialInstallDirMap_IsMetadata_AndFileTagsArePreserved()
    {
        var parser = new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance);
        var definition = Assert.Single(parser.Parse("""
            An Example Game:
              installDir:
                AnExampleGame: {}
              files:
                <base>/saves/*.sav:
                  tags: [save]
                <base>/settings.json:
                  tags: [config]
                <base>/screenshots:
                  tags: [screenshot]
              steam:
                id: 123
            """));
        Assert.Equal(3, definition.ContentItems.Count);
        Assert.Contains(definition.ContentItems, item => item.Category == GSYNC.Core.Models.ContentCategory.Save && item.DefaultEnabled);
        Assert.Contains(definition.ContentItems, item => item.Category == GSYNC.Core.Models.ContentCategory.Config);
        Assert.Contains(definition.ContentItems, item => item.Category == GSYNC.Core.Models.ContentCategory.Extra && !item.DefaultEnabled);
        Assert.DoesNotContain(definition.ContentItems.SelectMany(item => item.PathTemplates), path => path.EndsWith("AnExampleGame"));
    }

    [Theory]
    [InlineData("<root>/<game>/saves", "%GAME_INSTALL_DIR%/saves")]
    [InlineData("<root>/userdata/<storeUserId>/<storeGameId>/remote", "%STEAM_ROOT%/userdata/%STORE_USER_ID%/%STORE_GAME_ID%/remote")]
    [InlineData("<winLocalAppDataLow>/Studio/Game", "%HOME%/AppData/LocalLow/Studio/Game")]
    public void TryMapPath_SupportsPortableWindowsAndStorePlaceholders(string path, string expected)
    {
        Assert.True(LudusaviVariableMapper.TryMapPath(path, out var mapped));
        Assert.Equal(expected, mapped);
    }

    [Fact]
    public void Parse_RejectsRecursiveAliases_AndDoesNotSilentlyDropUnsupportedWindowsSaves()
    {
        var parser = new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance);
        var exception = Assert.Throws<YamlDotNet.Core.YamlException>(() => parser.Parse("game: &self\n  ignored: *self\n"));
        Assert.IsType<InvalidDataException>(exception.InnerException);
        var definition = Assert.Single(parser.Parse("game:\n  files:\n    '<futureWindowsRoot>/save':\n      tags: [save]\n      when: [{os: windows}]\n"));
        Assert.Contains("<futureWindowsRoot>/save", Assert.Single(definition.ContentItems).PathTemplates);
    }
}
