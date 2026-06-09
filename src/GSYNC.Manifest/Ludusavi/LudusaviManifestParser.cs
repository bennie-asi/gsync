using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GSYNC.Manifest.Ludusavi;

public sealed class LudusaviManifestParser : ILudusaviManifestParser
{
    private readonly ILogger<LudusaviManifestParser> _logger;
    private readonly IDeserializer _deserializer;

    public LudusaviManifestParser(ILogger<LudusaviManifestParser> logger)
    {
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public IReadOnlyList<GameContentDefinition> Parse(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Array.Empty<GameContentDefinition>();
        }

        var document = _deserializer.Deserialize<LudusaviManifestDocument>(yamlContent) ?? new LudusaviManifestDocument();
        var definitions = new List<GameContentDefinition>();

        foreach (var pair in document)
        {
            var gameId = pair.Key;
            var entry = pair.Value;
            var pathTemplates = new List<string>();

            foreach (var path in entry.Files.Keys)
            {
                if (LudusaviVariableMapper.TryMapPath(path, out var mappedPath))
                {
                    pathTemplates.Add(NormalizePath(mappedPath));
                }
                else
                {
                    _logger.LogWarning("Skipping unsupported Ludusavi path template '{Path}' for game '{GameId}'.", path, gameId);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.InstallDir) && LudusaviVariableMapper.TryMapPath(entry.InstallDir, out var installDir))
            {
                pathTemplates.Add(NormalizePath(installDir));
            }

            var sourceHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(entry.Steam?.Id))
            {
                sourceHints["steam.id"] = entry.Steam.Id;
            }

            definitions.Add(new GameContentDefinition
            {
                GameId = gameId,
                DisplayName = gameId,
                SourceHints = sourceHints,
                ContentItems = pathTemplates.Count == 0
                    ? Array.Empty<ContentItem>()
                    : new[]
                    {
                        new ContentItem
                        {
                            ContentId = "default",
                            Category = ContentCategory.Save,
                            PathTemplates = pathTemplates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                            DefaultEnabled = true,
                        },
                    },
            });
        }

        return definitions;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
